namespace Aetheris.CLI.Tests;

/// <summary>Runs the local 3DM qualification only when its untracked source fixture is present.</summary>
public sealed class LocalThreeDmFactAttribute : FactAttribute
{
    public LocalThreeDmFactAttribute()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            directory = directory.Parent;

        if (directory is null || !File.Exists(Path.Combine(directory.FullName, "testdata", "3DM", "cartesian-product-metres.3dm")))
            Skip = "Local Cartesian 3DM fixture is absent.";
    }
}
