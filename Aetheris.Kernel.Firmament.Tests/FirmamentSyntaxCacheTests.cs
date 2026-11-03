using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentSyntaxCacheTests
{
    [Fact]
    public void CacheSharesEqualTextButRetainsOrdinalDocumentAndSourceIdentity()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var source = File.ReadAllText(Path.Combine(directory.FullName, "fixtures", "Canonical", "Learn", "01-plate.firmament"));
        var first = FirmamentSyntaxParser.Parse(source, "CacheIdentity.firmament");
        Assert.Same(first, FirmamentSyntaxParser.Parse(new string(source.ToCharArray()), new string("CacheIdentity.firmament".ToCharArray())));
        Assert.NotSame(first, FirmamentSyntaxParser.Parse(source, "cacheidentity.firmament"));
        Assert.NotSame(first, FirmamentSyntaxParser.Parse(source + "\n", "CacheIdentity.firmament"));
    }
}
