using Aetheris.Piping;
using Aetheris.PlasticShell;
using Aetheris.SheetMetal;
using Aetheris.Surfacing;

namespace Aetheris.Modules.BuiltIn;

/// <summary>The sole M0 composition root. Explicit code registration keeps discovery deterministic and auditable.</summary>
public static class BuiltInModules
{
    private static readonly Lazy<AetherisModuleCatalog> CatalogValue = new(() => AetherisModuleCatalog.Create(
        [CoreModule.Definition, SurfacingModule.Definition, PipingModule.Definition, SheetMetalModule.Definition, PlasticShellModule.Definition]));
    public static AetherisModuleCatalog Catalog => CatalogValue.Value;
}
