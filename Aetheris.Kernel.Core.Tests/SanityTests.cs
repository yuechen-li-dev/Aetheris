using Aetheris.Kernel.Core;

namespace Aetheris.Kernel.Core.Tests;

public sealed class SanityTests
{
    [Fact]
    public void KernelCorePlaceholderTypeExists()
    {
        var placeholderType = typeof(KernelPlaceholder);

        Assert.Equal("KernelPlaceholder", placeholderType.Name);
    }
}
