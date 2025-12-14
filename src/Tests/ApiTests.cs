using PublicApiGenerator;

namespace Tests;

public class ApiTests
{
    [Theory]
    [InlineData(typeof(CompressedStaticFileOptions))]
    public void PublicApi(Type type)
    {
        var assemblyName = type.Assembly.GetName().Name ?? "";
        var api = type.Assembly.GeneratePublicApi(new() { IncludeAssemblyAttributes = false, DenyNamespacePrefixes = [] }) + Environment.NewLine;
        api.ShouldMatchApproved(b => b.NoDiff().WithDiscriminator(assemblyName));
    }
}
