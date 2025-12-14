var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseCompressedStaticFiles();

app.MapGet("/", () => """
    Compressed Static Files Sample
    
    Navigate to /sample.txt to test compression.
    
    Expected responses based on Accept-Encoding header:
    - With 'br': Returns "Test2" (Brotli compressed)
    - With 'gzip': Returns "Test3" (Gzip compressed)
    - Without compression support: Returns "Test1" (uncompressed)
    
    The middleware automatically selects the best compression format.
    """);

app.Run();
