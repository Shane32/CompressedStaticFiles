using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Tests;

public class CompressedStaticFilesTests
{
    private readonly string _wwwrootPath;

    public CompressedStaticFilesTests()
    {
        // Use the wwwroot directory from the test project output
        _wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    }

    [Theory]
    [InlineData("br", "Test2", "br")]
    [InlineData("gzip", "Test3", "gzip")]
    [InlineData("br, gzip", "Test2", "br")]
    [InlineData("gzip, br", "Test2", "br")]
    [InlineData("deflate", "Test1", null)]
    [InlineData("", "Test1", null)]
    [InlineData("*", "Test1", null)]
    [InlineData("gzip;q=0.8, br;q=0.9", "Test2", "br")]
    [InlineData("gzip;q=0.9, br;q=0.8", "Test3", "gzip")]
    [InlineData("br;q=0.9, gzip;q=0.8", "Test2", "br")]
    [InlineData("br;q=0.8, gzip;q=0.9", "Test3", "gzip")]
    [InlineData("gzip;q=0.5, br;q=0.5", "Test2", "br")]
    [InlineData("br;q=0.5, gzip;q=0.5", "Test2", "br")]
    [InlineData("br;q=0", "Test1", null)]
    [InlineData("br;zz=bb", "Test2", "br")]
    [InlineData("br;zz=bb;q=0.8, gzip;zz=bb;q=0.9", "Test3", "gzip")]
    [InlineData("gzip;zz=bb;q=0.8, br;zz=bb;q=0.9", "Test2", "br")]
    public async Task UseCompressedStaticFiles_ReturnsCorrectFile(string acceptEncoding, string expectedContent, string? expectedEncoding)
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder => {
                webBuilder
                    .UseTestServer()
                    .Configure(app => {
                        app.UseCompressedStaticFiles(new CompressedStaticFileOptions {
                            FileSystemPath = _wwwrootPath
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/sample.txt");
        if (!string.IsNullOrEmpty(acceptEncoding)) {
            request.Headers.TryAddWithoutValidation("Accept-Encoding", acceptEncoding);
        }

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify encoding header first
        if (expectedEncoding != null) {
            response.Content.Headers.ContentEncoding.ShouldContain(expectedEncoding);
        } else {
            response.Content.Headers.ContentEncoding.ShouldBeEmpty();
        }

        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/plain");

        // Read and decompress the content if needed
        var contentBytes = await response.Content.ReadAsByteArrayAsync();
        string content;

        if (expectedEncoding == "br") {
            using var memoryStream = new MemoryStream(contentBytes);
            using var brotliStream = new BrotliStream(memoryStream, CompressionMode.Decompress);
            using var reader = new StreamReader(brotliStream);
            content = await reader.ReadToEndAsync();
        } else if (expectedEncoding == "gzip") {
            using var memoryStream = new MemoryStream(contentBytes);
            using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream);
            content = await reader.ReadToEndAsync();
        } else {
            content = await response.Content.ReadAsStringAsync();
        }

        content.ShouldBe(expectedContent);
    }

    [Fact]
    public async Task UseCompressedStaticFiles_DirectRequestToCompressedFile_Returns404()
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder => {
                webBuilder
                    .UseTestServer()
                    .Configure(app => {
                        app.UseCompressedStaticFiles(new CompressedStaticFileOptions {
                            FileSystemPath = _wwwrootPath
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act
        var brResponse = await client.GetAsync("/sample.txt.br");
        var gzResponse = await client.GetAsync("/sample.txt.gz");

        // Assert
        brResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        gzResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UseCompressedStaticFiles_NonExistentFile_Returns404()
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder => {
                webBuilder
                    .UseTestServer()
                    .Configure(app => {
                        app.UseCompressedStaticFiles(new CompressedStaticFileOptions {
                            FileSystemPath = _wwwrootPath
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/nonexistent.txt");
        request.Headers.Add("Accept-Encoding", "br, gzip");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("br", "Test2", "br")]
    [InlineData("gzip", "Test3", "gzip")]
    [InlineData("", "Test1", null)]
    public async Task UseCompressedStaticFiles_WithQueryString_ReturnsCorrectFile(string acceptEncoding, string expectedContent, string? expectedEncoding)
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder => {
                webBuilder
                    .UseTestServer()
                    .Configure(app => {
                        app.UseCompressedStaticFiles(new CompressedStaticFileOptions {
                            FileSystemPath = _wwwrootPath
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/sample.txt?ab=cd");
        if (!string.IsNullOrEmpty(acceptEncoding)) {
            request.Headers.TryAddWithoutValidation("Accept-Encoding", acceptEncoding);
        }

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify encoding header first
        if (expectedEncoding != null) {
            response.Content.Headers.ContentEncoding.ShouldContain(expectedEncoding);
        } else {
            response.Content.Headers.ContentEncoding.ShouldBeEmpty();
        }

        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/plain");

        // Read and decompress the content if needed
        var contentBytes = await response.Content.ReadAsByteArrayAsync();
        string content;

        if (expectedEncoding == "br") {
            using var memoryStream = new MemoryStream(contentBytes);
            using var brotliStream = new BrotliStream(memoryStream, CompressionMode.Decompress);
            using var reader = new StreamReader(brotliStream);
            content = await reader.ReadToEndAsync();
        } else if (expectedEncoding == "gzip") {
            using var memoryStream = new MemoryStream(contentBytes);
            using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream);
            content = await reader.ReadToEndAsync();
        } else {
            content = await response.Content.ReadAsStringAsync();
        }

        content.ShouldBe(expectedContent);
    }

    [Theory]
    [InlineData("/../wwwroot/sample.txt")]
    [InlineData("/../dontreadme.txt")]
    public async Task UseCompressedStaticFiles_PathTraversalAttempt_Returns404(string path)
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder => {
                webBuilder
                    .UseTestServer()
                    .Configure(app => {
                        app.UseCompressedStaticFiles(new CompressedStaticFileOptions {
                            FileSystemPath = _wwwrootPath
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Accept-Encoding", "br, gzip");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("image.png", "image/png", "br")]
    [InlineData("image.png", "image/png", "gzip")]
    [InlineData("image.png", "image/png", "br, gzip")]
    [InlineData("font.ttf", "application/x-font-ttf", "br")]
    [InlineData("font.ttf", "application/x-font-ttf", "gzip")]
    [InlineData("font.ttf", "application/x-font-ttf", "br, gzip")]
    public async Task UseCompressedStaticFiles_MissingCompressedFiles_ServesUncompressedAsset(string filename, string contentType, string acceptEncoding)
    {
        using var files = new TemporaryStaticFiles();
        var content = Encoding.UTF8.GetBytes("uncompressed asset");
        File.WriteAllBytes(Path.Combine(files.Path, filename), content);
        using var host = await StartFallbackHost(files.Path);
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/static/" + filename);
        request.Headers.Add("Accept-Encoding", acceptEncoding);

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(contentType);
        response.Content.Headers.ContentEncoding.ShouldBeEmpty();
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(content);
    }

    [Fact]
    public async Task UseCompressedStaticFiles_MissingPreferredEncoding_ServesGzip()
    {
        using var files = new TemporaryStaticFiles();
        var content = Encoding.UTF8.GetBytes("gzip fallback");
        File.WriteAllBytes(Path.Combine(files.Path, "sample.txt"), content);
        using (var output = File.Create(Path.Combine(files.Path, "sample.txt.gz"))) {
            using var gzip = new GZipStream(output, CompressionLevel.Optimal);
            gzip.Write(content);
        }
        using var host = await StartFallbackHost(files.Path);
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/static/sample.txt");
        request.Headers.Add("Accept-Encoding", "br, gzip");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentEncoding.ShouldContain("gzip");
        using var compressed = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var decompressor = new GZipStream(compressed, CompressionMode.Decompress);
        using var decoded = new MemoryStream();
        await decompressor.CopyToAsync(decoded);
        decoded.ToArray().ShouldBe(content);
    }

    [Theory]
    [InlineData("HEAD", HttpStatusCode.OK)]
    [InlineData("conditional", HttpStatusCode.NotModified)]
    [InlineData("range", HttpStatusCode.PartialContent)]
    public async Task UseCompressedStaticFiles_MissingCompressedFiles_PreservesHttpSemantics(string scenario, HttpStatusCode expectedStatus)
    {
        using var files = new TemporaryStaticFiles();
        var content = Encoding.UTF8.GetBytes("uncompressed fallback content");
        File.WriteAllBytes(Path.Combine(files.Path, "sample.txt"), content);
        using var host = await StartFallbackHost(files.Path);
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(scenario == "HEAD" ? HttpMethod.Head : HttpMethod.Get, "/static/sample.txt");
        request.Headers.Add("Accept-Encoding", "br, gzip");
        if (scenario == "conditional") {
            using var initialResponse = await client.GetAsync("/static/sample.txt");
            initialResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            request.Headers.IfNoneMatch.Add(initialResponse.Headers.ETag!);
        } else if (scenario == "range") {
            request.Headers.Range = new RangeHeaderValue(0, 3);
        }

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(expectedStatus);
        response.Content.Headers.ContentEncoding.ShouldBeEmpty();
        var body = await response.Content.ReadAsByteArrayAsync();
        if (scenario == "range") {
            body.ShouldBe(content[..4]);
            response.Content.Headers.ContentRange?.ToString().ShouldBe($"bytes 0-3/{content.Length}");
        } else {
            body.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task UseCompressedStaticFiles_AllRepresentationsMissing_Returns404()
    {
        using var files = new TemporaryStaticFiles();
        using var host = await StartFallbackHost(files.Path);
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/static/missing.png");
        request.Headers.Add("Accept-Encoding", "br, gzip");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(StatusCodes.Status200OK)]
    [InlineData(StatusCodes.Status202Accepted)]
    public async Task UseCompressedStaticFiles_AllRepresentationsMissing_PreservesStatusForNextMiddleware(int initialStatus)
    {
        using var files = new TemporaryStaticFiles();
        using var host = await StartFallbackHost(
            files.Path,
            before: app => app.Use(async (context, next) => {
                context.Response.StatusCode = initialStatus;
                await next(context);
            }),
            after: app => app.Run(async context => {
                context.Response.StatusCode.ShouldBe(initialStatus);
                await context.Response.WriteAsync("downstream fallback");
            }));
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/static/missing.png");
        request.Headers.Add("Accept-Encoding", "br, gzip");

        using var response = await client.SendAsync(request);

        ((int)response.StatusCode).ShouldBe(initialStatus);
        (await response.Content.ReadAsStringAsync()).ShouldBe("downstream fallback");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseCompressedStaticFiles_HeadWithNotFoundStatus_DoesNotInvokeNextMiddleware(bool setStatusInPrepareResponse)
    {
        using var files = new TemporaryStaticFiles();
        var content = Encoding.UTF8.GetBytes("handled HEAD response");
        File.WriteAllBytes(Path.Combine(files.Path, "sample.txt"), content);
        var nextCalled = false;
        var prepared = false;
        using var host = await StartFallbackHost(
            files.Path,
            before: app => app.Use(async (context, next) => {
                if (!setStatusInPrepareResponse)
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                await next(context);
            }),
            after: app => app.Run(context => {
                nextCalled = true;
                context.Response.StatusCode = StatusCodes.Status418ImATeapot;
                return Task.CompletedTask;
            }),
            configureOptions: options => options.OnPrepareResponse = context => {
                prepared = true;
                if (setStatusInPrepareResponse)
                    context.Context.Response.StatusCode = StatusCodes.Status404NotFound;
            });
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Head, "/static/sample.txt");
        request.Headers.Add("Accept-Encoding", "br, gzip");

        using var response = await client.SendAsync(request);

        prepared.ShouldBeTrue();
        nextCalled.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentLength.ShouldBe(content.Length);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    private static Task<IHost> StartFallbackHost(string directory, Action<IApplicationBuilder>? before = null, Action<IApplicationBuilder>? after = null, Action<CompressedStaticFileOptions>? configureOptions = null)
    {
        return new HostBuilder()
            .ConfigureWebHost(webBuilder => {
                webBuilder
                    .UseTestServer()
                    .Configure(app => {
                        before?.Invoke(app);
                        var options = new CompressedStaticFileOptions {
                            FileSystemPath = directory,
                            RequestPath = "/static",
                        };
                        configureOptions?.Invoke(options);
                        app.UseCompressedStaticFiles(options);
                        after?.Invoke(app);
                    });
            })
            .StartAsync();
    }

    private sealed class TemporaryStaticFiles : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CompressedStaticFilesTests-" + Guid.NewGuid().ToString("N"));

        public TemporaryStaticFiles()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
