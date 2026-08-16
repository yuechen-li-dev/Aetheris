namespace Aetheris.Kernel.StandardLibrary.Materials.Database;

internal static class MaterialSeedData
{
    private const string Room = "Nominal room-temperature data (approximately 20-25 °C); strength values are minimum or typical as identified per property.";
    private const double RoomK = 293.15;

    public static IReadOnlyList<MaterialEntity> Create() =>
    [
        Material("aluminum/5052-h32", "Standard.Materials.Aluminum.5052_H32", "Aluminum", "5052", "5052", "H32", "AA 5052", "Aluminum 5052-H32",
            P(MaterialPropertyKind.Density, 2680, "kg/m^3", "PROTO-5052-H32", "https://www.protolabs.com/media/iw0mfa0k/datasheet-sm-aluminum5052-h32.pdf"),
            P(MaterialPropertyKind.YoungsModulus, 70.3e9, "Pa", "PROTO-5052-H32", "https://www.protolabs.com/media/iw0mfa0k/datasheet-sm-aluminum5052-h32.pdf"),
            P(MaterialPropertyKind.PoissonsRatio, .33, "1", "MATWEB-5052-H32", "https://www.matweb.com/search/datasheet.aspx?matguid=96d768abc51e4157a1b8f95856c49028", notes: "Industry-reference nominal."),
            P(MaterialPropertyKind.YieldStrength, 193e6, "Pa", "PROTO-5052-H32", "https://www.protolabs.com/media/iw0mfa0k/datasheet-sm-aluminum5052-h32.pdf"),
            P(MaterialPropertyKind.UltimateTensileStrength, 228e6, "Pa", "PROTO-5052-H32", "https://www.protolabs.com/media/iw0mfa0k/datasheet-sm-aluminum5052-h32.pdf"),
            P(MaterialPropertyKind.ThermalConductivity, 138, "W/(m*K)", "PROTO-5052-H32", "https://www.protolabs.com/media/iw0mfa0k/datasheet-sm-aluminum5052-h32.pdf")),

        Material("aluminum/6061-t6", "Standard.Materials.Aluminum.6061_T6", "Aluminum", "6061", "6061", "T6", "EN AW-6061 / AA 6061", "Aluminum 6061-T6",
            P(MaterialPropertyKind.Density, 2700, "kg/m^3", "TK-6061-2018", "https://ucpcdn.thyssenkrupp.com/_legacy/UCPthyssenkruppBAMXUK/assets.files/material-data-sheets/aluminium/aluminium-6061.pdf"),
            P(MaterialPropertyKind.YoungsModulus, 70e9, "Pa", "TK-6061-2018", "https://ucpcdn.thyssenkrupp.com/_legacy/UCPthyssenkruppBAMXUK/assets.files/material-data-sheets/aluminium/aluminium-6061.pdf"),
            P(MaterialPropertyKind.PoissonsRatio, .33, "1", "MATWEB-6061-T6", "https://www.matweb.com/search/datasheet.aspx?matguid=1b8c06d0ca7c456694c7777d9e10be5b", notes: "Industry-reference nominal."),
            P(MaterialPropertyKind.ShearModulus, 26.3e9, "Pa", "TK-6061-2018", "https://ucpcdn.thyssenkrupp.com/_legacy/UCPthyssenkruppBAMXUK/assets.files/material-data-sheets/aluminium/aluminium-6061.pdf", notes: "Independently tabulated; not derived by Aetheris."),
            P(MaterialPropertyKind.YieldStrength, 240e6, "Pa", "HYDRO-6061", "https://www.hydro.com/Document/Index?id=560718&name=Alloy+6061.pdf", MaterialPropertyAuthority.StandardMinimum),
            P(MaterialPropertyKind.UltimateTensileStrength, 260e6, "Pa", "HYDRO-6061", "https://www.hydro.com/Document/Index?id=560718&name=Alloy+6061.pdf", MaterialPropertyAuthority.StandardMinimum),
            P(MaterialPropertyKind.ThermalConductivity, 180, "W/(m*K)", "HYDRO-6061", "https://www.hydro.com/Document/Index?id=560718&name=Alloy+6061.pdf")),

        Material("steel/astm-a36", "Standard.Materials.Steel.ASTM_A36", "Steel", "A36", "A36", null, "ASTM A36/A36M", "ASTM A36 structural steel",
            P(MaterialPropertyKind.Density, 7850, "kg/m^3", "MATWEB-A36", "https://www.matweb.com/search/DataSheet.aspx?MatGUID=afc003f4fb40465fa3df05129f0e88e6", notes: "Industry-reference nominal."),
            P(MaterialPropertyKind.YoungsModulus, 200e9, "Pa", "AISC-360", "https://www.aisc.org/standards/aisc-360/", MaterialPropertyAuthority.IndustryReferenceNominal, "Structural-steel elastic constant used by AISC."),
            P(MaterialPropertyKind.PoissonsRatio, .30, "1", "AISC-360", "https://www.aisc.org/standards/aisc-360/", MaterialPropertyAuthority.IndustryReferenceNominal, "Structural-steel elastic constant used by AISC."),
            P(MaterialPropertyKind.YieldStrength, 250e6, "Pa", "ASTM-A36", "https://www.astm.org/a0036_a0036m-19.html", MaterialPropertyAuthority.StandardMinimum, "Minimum for common plate/shapes within the bounded X1 condition."),
            P(MaterialPropertyKind.UltimateTensileStrength, 400e6, "Pa", "ASTM-A36", "https://www.astm.org/a0036_a0036m-19.html", MaterialPropertyAuthority.StandardMinimum, "Lower bound of specified tensile-strength range.")),

        Material("stainless-steel/304-annealed", "Standard.Materials.StainlessSteel.304_Annealed", "StainlessSteel", "304", "S30400", "Annealed", "ASTM A240 / EN 1.4301", "304 stainless steel, annealed",
            P(MaterialPropertyKind.Density, 7900, "kg/m^3", "OUTOKUMPU-CORE", "https://www.outokumpu.com/en/products/product-ranges/-/media/files/products/core/outokumpu-core-range-datasheet.pdf"),
            P(MaterialPropertyKind.YoungsModulus, 200e9, "Pa", "OUTOKUMPU-CORE", "https://www.outokumpu.com/en/products/product-ranges/-/media/files/products/core/outokumpu-core-range-datasheet.pdf"),
            P(MaterialPropertyKind.PoissonsRatio, .30, "1", "MATWEB-304", "https://www.matweb.com/search/datasheet.aspx?matguid=abc4415b0f8b490387e3c922237098da", notes: "Industry-reference nominal."),
            P(MaterialPropertyKind.YieldStrength, 170e6, "Pa", "OUTOKUMPU-CORE", "https://www.outokumpu.com/en/products/product-ranges/-/media/files/products/core/outokumpu-core-range-datasheet.pdf", MaterialPropertyAuthority.StandardMinimum, "ASTM A240 0.2% proof-strength minimum for listed flat-product forms."),
            P(MaterialPropertyKind.UltimateTensileStrength, 485e6, "Pa", "OUTOKUMPU-CORE", "https://www.outokumpu.com/en/products/product-ranges/-/media/files/products/core/outokumpu-core-range-datasheet.pdf", MaterialPropertyAuthority.StandardMinimum, "ASTM A240 minimum."),
            P(MaterialPropertyKind.ThermalConductivity, 15, "W/(m*K)", "OUTOKUMPU-CORE", "https://www.outokumpu.com/en/products/product-ranges/-/media/files/products/core/outokumpu-core-range-datasheet.pdf"),
            P(MaterialPropertyKind.SpecificHeat, 500, "J/(kg*K)", "OUTOKUMPU-CORE", "https://www.outokumpu.com/en/products/product-ranges/-/media/files/products/core/outokumpu-core-range-datasheet.pdf"),
            P(MaterialPropertyKind.CoefficientOfThermalExpansion, 16e-6, "1/K", "OUTOKUMPU-CORE", "https://www.outokumpu.com/en/products/product-ranges/-/media/files/products/core/outokumpu-core-range-datasheet.pdf", notes: "Mean coefficient from 20 to 100 °C."))
    ];

    private static MaterialEntity Material(string stableId, string path, string family, string designation, string? grade, string? temper, string? standard, string displayName, params MaterialPropertyEntity[] properties)
    {
        var material = new MaterialEntity { CatalogId = "standard", StableId = stableId, FirmamentPath = path, Family = family, Designation = designation, Grade = grade, Temper = temper, Standard = standard, DisplayName = displayName, ConstitutiveClass = MaterialConstitutiveClass.LinearElasticIsotropic, ReferenceCondition = Room };
        foreach (var property in properties) material.Properties.Add(property);
        return material;
    }

    private static MaterialPropertyEntity P(MaterialPropertyKind kind, double value, string unit, string sourceId, string uri, MaterialPropertyAuthority authority = MaterialPropertyAuthority.ManufacturerTypical, string? notes = null) =>
        new() { Kind = kind, ValueSi = value, UnitSymbol = unit, SourceId = sourceId, SourceUri = uri, Authority = authority, Condition = Room, ReferenceTemperatureKelvin = RoomK, Notes = notes };
}
