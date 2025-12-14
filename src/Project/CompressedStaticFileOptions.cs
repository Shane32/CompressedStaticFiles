using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;

namespace Shane32.CompressedStaticFiles;

/// <summary>
/// Options for serving compressed static files.
/// </summary>
public class CompressedStaticFileOptions
{
    private static readonly StaticFileOptions _defaultStaticFileOptions = new StaticFileOptions();

    /// <summary>
    /// The relative request path that maps to static resources.
    /// </summary>
    public PathString RequestPath { get; set; }

    /// <summary>
    /// The content type provider used to determine the content type of files.
    /// </summary>
    public IContentTypeProvider ContentTypeProvider { get; set; } = _defaultStaticFileOptions.ContentTypeProvider;

    /// <summary>
    /// The default content type for a request if the ContentTypeProvider cannot determine one.
    /// </summary>
    public string? DefaultContentType { get; set; }

    /// <summary>
    /// Indicates whether to serve unknown file types.
    /// </summary>
    public bool ServeUnknownFileTypes { get; set; }

    /// <summary>
    /// Called after the status code and headers have been set, but before the body has been written.
    /// This can be used to add or change the response headers.
    /// </summary>
    public Action<StaticFileResponseContext> OnPrepareResponse { get; set; } = _defaultStaticFileOptions.OnPrepareResponse;

    /// <summary>
    /// Called after the status code and headers have been set, but before the body has been written.
    /// This can be used to add or change the response headers.
    /// </summary>
    public Func<StaticFileResponseContext, Task> OnPrepareResponseAsync { get; set; } = _defaultStaticFileOptions.OnPrepareResponseAsync;

    /// <summary>
    /// The physical file system path to serve files from.
    /// If not specified, the web root path will be used.
    /// </summary>
    public string? FileSystemPath { get; set; }

    /// <summary>
    /// Dictionary mapping Content-Encoding values to their configuration (file extension and priority).
    /// When client quality values are equal, encodings with lower priority values are preferred.
    /// For example: { "br" => new EncodingOptions { Extension = ".br", Priority = 0 } }
    /// Default includes Brotli (priority 0) and Gzip (priority 1).
    /// </summary>
    public Dictionary<string, EncodingOptions> Encodings { get; set; } = new Dictionary<string, EncodingOptions>(StringComparer.OrdinalIgnoreCase) {
        { "br", new EncodingOptions { Extension = ".br", Priority = 0 } },
        { "gzip", new EncodingOptions { Extension = ".gz", Priority = 1 } }
    };
}
