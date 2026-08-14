using System.Reflection;

namespace Aetheris.SheetMetal;

public static class SheetMetalTemplateLibrary
{
    private const string ResourceName="Aetheris.SheetMetal.Firmament.SheetMetalProductFamilies.firmament";

    public static string Source
    {
        get
        {
            using var stream=typeof(SheetMetalTemplateLibrary).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"Embedded Firmament module '{ResourceName}' was not found.");
            using var reader=new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
