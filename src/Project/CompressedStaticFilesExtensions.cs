using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Shane32.CompressedStaticFiles;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for <see cref="IApplicationBuilder"/> to add compressed static file middleware.
/// </summary>
public static class CompressedStaticFilesExtensions
{
    /// <summary>
    /// Enables serving compressed static files for the current request path.
    /// This middleware checks the Accept-Encoding header and serves pre-compressed files
    /// with .br (Brotli) or .gz (Gzip) extensions if available, prioritizing by quality values.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> instance.</param>
    /// <param name="options">Options for configuring the compressed static file middleware.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
    public static IApplicationBuilder UseCompressedStaticFiles(
        this IApplicationBuilder app,
        CompressedStaticFileOptions? options = null)
    {
        options ??= new CompressedStaticFileOptions();

        // Get the web root path from the hosting environment
        var env = app.ApplicationServices.GetService(typeof(IWebHostEnvironment)) as IWebHostEnvironment;
        var fileSystemPath = options.FileSystemPath ?? env?.WebRootPath
            ?? throw new InvalidOperationException($"Unable to determine the web root path. Please specify {nameof(CompressedStaticFileOptions.FileSystemPath)} in {nameof(CompressedStaticFileOptions)}.");

        // Create the base physical file provider
        var physicalFileProvider = new PhysicalFileProvider(fileSystemPath);

        // If no ContentTypeProvider is specified, create one with compressed extensions removed
        if (options.ContentTypeProvider == null) {
            var contentTypeProvider = new FileExtensionContentTypeProvider();

            // Remove all compressed extensions so they don't get mapped to content types
            foreach (var encodingOptions in options.Encodings.Values) {
                // Ensure extensions start with "."
                var normalizedExtension = encodingOptions.Extension.StartsWith('.') ? encodingOptions.Extension : "." + encodingOptions.Extension;
                contentTypeProvider.Mappings.Remove(normalizedExtension);
            }

            options.ContentTypeProvider = contentTypeProvider;
        }

        // Build pipelines for each supported encoding
        var encodingPipelines = new Dictionary<string, RequestDelegate>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in options.Encodings) {
            var encoding = kvp.Key;
            var encodingOptions = kvp.Value;
            var fileProvider = new CompressedFileProvider(physicalFileProvider, encodingOptions.Extension);
            var staticFileOptions = CreateStaticFileOptions(options, fileProvider, encoding);

            var builder = app.New();
            builder.UseStaticFiles(staticFileOptions);
            encodingPipelines[encoding] = builder.Build();
        }

        // Build uncompressed pipeline
        var uncompressedOptions = CreateStaticFileOptions(options, physicalFileProvider, null);
        var uncompressedBuilder = app.New();
        uncompressedBuilder.UseStaticFiles(uncompressedOptions);
        var uncompressedPipeline = uncompressedBuilder.Build();

        // Use a single middleware that tries encodings in quality order
        app.Use(next => {
            return async context => {
                // Short-circuit if the request path doesn't match the configured RequestPath
                if (!context.Request.Path.StartsWithSegments(options.RequestPath, out var remainingPath)) {
                    await next(context).ConfigureAwait(false);
                    return;
                }

                // Get all acceptable encodings ordered by quality (highest first)
                // When qualities are equal, prefer encodings with lower priority values
                var orderedEncodings = GetOrderedAcceptableEncodings(
                    context.Request.Headers.AcceptEncoding,
                    options.Encodings);

                // Try each encoding in order of client preference
                foreach (var item in orderedEncodings) {
                    var encoding = item.Encoding;
                    if (encodingPipelines.TryGetValue(encoding, out var pipeline)) {
                        // Try to serve the file with this encoding
                        await pipeline(context).ConfigureAwait(false);

                        // If a file was served (status code set), we're done
                        if (context.Response.HasStarted || context.Response.StatusCode != StatusCodes.Status404NotFound) {
                            return;
                        }
                    }
                }

                // If no compressed version was served, try uncompressed
                await uncompressedPipeline(context).ConfigureAwait(false);

                // If still not served, continue to next middleware
                if (!context.Response.HasStarted && context.Response.StatusCode == StatusCodes.Status404NotFound) {
                    await next(context).ConfigureAwait(false);
                }
            };
        });

        return app;
    }

    /// <summary>
    /// Creates StaticFileOptions from CompressedStaticFileOptions with the specified file provider.
    /// </summary>
    private static StaticFileOptions CreateStaticFileOptions(
        CompressedStaticFileOptions options,
        IFileProvider fileProvider,
        string? contentEncoding)
    {
        var staticFileOptions = new StaticFileOptions {
            FileProvider = fileProvider,
            RequestPath = options.RequestPath,
            ContentTypeProvider = options.ContentTypeProvider,
            DefaultContentType = options.DefaultContentType,
            ServeUnknownFileTypes = options.ServeUnknownFileTypes,
            OnPrepareResponse = options.OnPrepareResponse,
            OnPrepareResponseAsync = options.OnPrepareResponseAsync,
        };

        // If serving compressed files, add Content-Encoding header in OnPrepareResponse
        if (contentEncoding != null) {
            var originalOnPrepareResponse = options.OnPrepareResponse;
            staticFileOptions.OnPrepareResponse = ctx => {
                ctx.Context.Response.Headers.ContentEncoding = contentEncoding;
                originalOnPrepareResponse(ctx);
            };
        }

        return staticFileOptions;
    }

    /// <summary>
    /// Parses the Accept-Encoding header and returns a list of acceptable encodings
    /// ordered by quality value (highest first). Encodings with q=0 are excluded.
    /// Wildcard "*" is ignored. When qualities are equal, prefer encodings with lower priority values.
    /// </summary>
    private static List<(string Encoding, double Quality)> GetOrderedAcceptableEncodings(StringValues acceptEncoding, Dictionary<string, EncodingOptions> supportedEncodings)
    {
        var encodingList = new List<(string Encoding, double Quality)>();

        foreach (var headerValue in acceptEncoding) {
            if (string.IsNullOrWhiteSpace(headerValue))
                continue;

            // Split by comma to get individual encoding entries
            var encodings = headerValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var entry in encodings) {
                // Split by semicolon to separate encoding from quality value
                var parts = entry.Split(';', StringSplitOptions.TrimEntries);
                var encodingName = parts[0];

                // Skip wildcard - we don't handle it (safest to return uncompressed)
                if (encodingName == "*")
                    continue;

                // Parse quality value if present, default is 1.0
                double quality = 1.0;
                for (int i = 1; i < parts.Length; i++) {
                    var part = parts[i];
                    if (part.StartsWith("q=", StringComparison.OrdinalIgnoreCase)) {
                        var qValue = part[2..];
                        if (double.TryParse(qValue, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var parsedQuality)) {
                            quality = parsedQuality;
                        }
                        break;
                    }
                }

                // Skip encodings with quality 0 (explicitly not acceptable)
                if (quality <= 0)
                    continue;

                // Check if encoding already exists and update quality if higher
                var existingIndex = encodingList.FindIndex(e => e.Encoding.Equals(encodingName, StringComparison.OrdinalIgnoreCase));
                if (existingIndex >= 0) {
                    if (quality > encodingList[existingIndex].Quality) {
                        encodingList[existingIndex] = (encodingName, quality);
                    }
                } else {
                    encodingList.Add((encodingName, quality));
                }
            }
        }

        // Sort by quality (descending), then by priority (ascending) for ties
        // Note: this is an unstable sort, but that's acceptable here
        encodingList.Sort((a, b) => {
            var qualityComparison = b.Quality.CompareTo(a.Quality);
            if (qualityComparison != 0)
                return qualityComparison;

            var aPriority = supportedEncodings.TryGetValue(a.Encoding, out var aOpts) ? aOpts.Priority : int.MaxValue;
            var bPriority = supportedEncodings.TryGetValue(b.Encoding, out var bOpts) ? bOpts.Priority : int.MaxValue;
            return aPriority.CompareTo(bPriority);
        });

        return encodingList;
    }
}
