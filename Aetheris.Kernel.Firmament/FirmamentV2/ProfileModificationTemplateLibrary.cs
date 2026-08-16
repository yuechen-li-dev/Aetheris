using System.Reflection;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public static class ProfileModificationTemplateLibrary
{
    private const string ResourceName="Aetheris.Kernel.Firmament.Resources.ProfileModifications.firmament";
    public static string Source
    {
        get
        {
            using var stream=typeof(ProfileModificationTemplateLibrary).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"Embedded Firmament module '{ResourceName}' was not found.");
            using var reader=new StreamReader(stream);return reader.ReadToEnd();
        }
    }
}
