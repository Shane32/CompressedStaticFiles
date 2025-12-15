using System.IO.Compression;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
}
