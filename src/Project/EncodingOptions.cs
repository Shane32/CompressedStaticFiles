namespace Shane32.CompressedStaticFiles;

/// <summary>
/// Represents a content encoding configuration with its file extension and priority.
/// </summary>
public class EncodingOptions
{
    /// <summary>
    /// The file extension for this encoding (e.g., ".br", ".gz", ".zz").
    /// </summary>
    public required string Extension { get; set; }

    /// <summary>
    /// Priority for this encoding when client quality values are equal.
    /// Lower values have higher priority. Default is 0.
    /// </summary>
    public int Priority { get; set; }
}
