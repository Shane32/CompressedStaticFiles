using System.IO.Compression;

namespace ConsoleApp;

internal class App
{
    public async Task RunAsync()
    {
        // Create wwwroot directory if it doesn't exist
        var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        Directory.CreateDirectory(wwwrootPath);

        // Write uncompressed file
        var samplePath = Path.Combine(wwwrootPath, "sample.txt");
        await File.WriteAllTextAsync(samplePath, "Test1");
        Console.WriteLine($"Created: {samplePath}");

        // Write Brotli compressed file
        var brPath = Path.Combine(wwwrootPath, "sample.txt.br");
        await using (var fileStream = File.Create(brPath))
        await using (var brotliStream = new BrotliStream(fileStream, CompressionLevel.Optimal)) {
            var bytes = Encoding.UTF8.GetBytes("Test2");
            await brotliStream.WriteAsync(bytes);
        }
        Console.WriteLine($"Created: {brPath}");

        // Write Gzip compressed file
        var gzPath = Path.Combine(wwwrootPath, "sample.txt.gz");
        await using (var fileStream = File.Create(gzPath))
        await using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal)) {
            var bytes = Encoding.UTF8.GetBytes("Test3");
            await gzipStream.WriteAsync(bytes);
        }
        Console.WriteLine($"Created: {gzPath}");

        Console.WriteLine("\nTest files created successfully!");
    }
}
