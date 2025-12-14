using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Shane32.CompressedStaticFiles;

/// <summary>
/// A file provider that wraps a PhysicalFileProvider and appends a file extension
/// to all file paths (e.g., ".br" or ".gz" for compressed files).
/// </summary>
public class CompressedFileProvider : IFileProvider
{
    private readonly IFileProvider _innerProvider;
    private readonly string _extension;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompressedFileProvider"/> class.
    /// </summary>
    /// <param name="innerProvider">The underlying file provider to wrap.</param>
    /// <param name="extension">The file extension to append (e.g., ".br" or ".gz").</param>
    public CompressedFileProvider(IFileProvider innerProvider, string extension)
    {
        _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
        _extension = extension ?? throw new ArgumentNullException(nameof(extension));

        if (!_extension.StartsWith('.')) {
            _extension = "." + _extension;
        }
    }

    /// <inheritdoc />
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        // For directory listings, we don't append the extension
        return _innerProvider.GetDirectoryContents(subpath);
    }

    /// <inheritdoc />
    public IFileInfo GetFileInfo(string subpath)
    {
        // Append the extension to the file path
        var compressedPath = subpath + _extension;
        return _innerProvider.GetFileInfo(compressedPath);
    }

    /// <inheritdoc />
    public IChangeToken Watch(string filter)
    {
        // Watch the compressed file
        var compressedFilter = filter + _extension;
        return _innerProvider.Watch(compressedFilter);
    }
}
