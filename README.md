# Shane32.CompressedStaticFiles

[![NuGet](https://img.shields.io/nuget/v/Shane32.CompressedStaticFiles.svg)](https://www.nuget.org/packages/Shane32.CompressedStaticFiles)

ASP.NET Core middleware that automatically serves pre-compressed static files (Brotli and Gzip) when available, falling back to uncompressed files.

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Add compressed static files middleware
app.UseCompressedStaticFiles();

app.Run();
```

## Compression of Static Assets

.NET 10+ automatically compresses static assets during publish. For earlier versions or when copying a project separately after build (such as a Vite React project), you'll need to compress files manually.

### Vite Plugin Example

For Vite projects, use the `vite-plugin-compression` plugin:

```bash
npm install vite-plugin-compression --save-dev
```

```javascript
// vite.config.js
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import compression from 'vite-plugin-compression'

export default defineConfig({
  plugins: [
    react(),
    // Generate .gz files
    compression({
      algorithm: 'gzip',
      ext: '.gz'
    }),
    // Generate .br files
    compression({
      algorithm: 'brotliCompress',
      ext: '.br'
    })
  ]
})
```

## Configuration Options

The `CompressedStaticFileOptions` class provides the following configuration options:

| Property | Type | Description |
|----------|------|-------------|
| `RequestPath` | `PathString` | [See SharedOptionsBase.RequestPath](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.staticfiles.infrastructure.sharedoptionsbase.requestpath) |
| `ContentTypeProvider` | `IContentTypeProvider` | [See StaticFileOptions.ContentTypeProvider](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.staticfileoptions.contenttypeprovider) |
| `DefaultContentType` | `string?` | [See StaticFileOptions.DefaultContentType](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.staticfileoptions.defaultcontenttype) |
| `ServeUnknownFileTypes` | `bool` | [See StaticFileOptions.ServeUnknownFileTypes](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.staticfileoptions.serveunknownfiletypes) |
| `OnPrepareResponse` | `Action<StaticFileResponseContext>?` | [See StaticFileOptions.OnPrepareResponse](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.staticfileoptions.onprepareresponse) |
| `OnPrepareResponseAsync` | `Func<StaticFileResponseContext, Task>?` | [See StaticFileOptions.OnPrepareResponseAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.staticfileoptions.onprepareresponseasync) |
| `FileSystemPath` | `string?` | The physical file system path to serve files from. If not specified, the web root path will be used. |
| `Encodings` | `Dictionary<string, EncodingOptions>` | Dictionary mapping Content-Encoding values to their configuration (file extension and priority). Default includes Brotli (`"br"` with priority 0) and Gzip (`"gzip"` with priority 1). |

### EncodingOptions

The `EncodingOptions` class configures each supported encoding:

| Property | Type | Description |
|----------|------|-------------|
| `Extension` | `string` | The file extension for this encoding (e.g., `".br"`, `".gz"`). |
| `Priority` | `int` | Priority for this encoding when client quality values are equal. Lower values have higher priority. Default is 0. |

## Advanced Examples

### Custom Directory with Cache Headers

This example demonstrates serving compressed static files from a custom directory with a specific request path and cache headers:

```csharp
app.UseCompressedStaticFiles(new CompressedStaticFileOptions
{
    RequestPath = "/static",
    FileSystemPath = Path.Combine(builder.Environment.WebRootPath, "static"),
    OnPrepareResponse = ctx =>
    {
        // Add cache headers for long-term caching
        ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
    },
});
```

### Custom Encodings

This example shows how to add support for additional compression formats or customize priorities:

```csharp
app.UseCompressedStaticFiles(new CompressedStaticFileOptions
{
    Encodings = new Dictionary<string, EncodingOptions>(StringComparer.OrdinalIgnoreCase)
    {
        // Brotli with highest priority
        { "br", new EncodingOptions { Extension = ".br", Priority = 0 } },
        // Gzip with medium priority
        { "gzip", new EncodingOptions { Extension = ".gz", Priority = 1 } },
        // Deflate with lowest priority (if you have .zz files)
        { "deflate", new EncodingOptions { Extension = ".zz", Priority = 2 } }
    }
});
```

## How It Works

The middleware intelligently serves compressed files based on the client's `Accept-Encoding` header:

1. **Quality-based selection**: The middleware parses quality values (q-values) from the `Accept-Encoding` header and prioritizes encodings with higher quality values
2. **Priority tie-breaking**: When multiple encodings have the same quality value, the middleware uses the `Priority` property to determine which encoding to serve first
3. **Fallback behavior**: If no compressed version exists or the client doesn't support any configured encodings, it serves the uncompressed file
4. **Wildcard handling**: The wildcard `*` in `Accept-Encoding` is ignored for safety, resulting in uncompressed file delivery

The middleware automatically sets the `Content-Encoding` header when serving compressed files.

### Example Scenarios

- `Accept-Encoding: br, gzip` → Serves Brotli (both have quality 1.0, Brotli has priority 0)
- `Accept-Encoding: gzip;q=0.9, br;q=0.8` → Serves Gzip (higher quality value)
- `Accept-Encoding: br;q=0` → Serves uncompressed (q=0 means not acceptable)
- `Accept-Encoding: *` → Serves uncompressed (wildcard is ignored)

### Content Type Provider

When no `ContentTypeProvider` is specified, the middleware creates a default `FileExtensionContentTypeProvider` with all configured compressed extensions removed from the mappings. This ensures that:

- Compressed files (e.g., `script.js.br`, `style.css.gz`) are not directly served as separate content types
- The content type is determined from the original file name (e.g., `script.js.br` is served with the content type for `.js` files)
- Direct requests to compressed files (e.g., `/script.js.br`) will return 404 Not Found, preventing clients from bypassing the compression negotiation

If you provide a custom `ContentTypeProvider`, you should ensure it does not include mappings for your compressed file extensions.

## License

This project is licensed under the MIT License.

## Credits

Glory to Jehovah, Lord of Lords and King of Kings, creator of Heaven and Earth, who through his Son Jesus Christ,
has reedemed me to become a child of God. -Shane32
