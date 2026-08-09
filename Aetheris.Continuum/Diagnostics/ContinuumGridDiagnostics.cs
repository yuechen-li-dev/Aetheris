using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Continuum.Lattice;

namespace Aetheris.Continuum.Diagnostics;

public static class ContinuumGridDiagnostics
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string ToJson(ContinuumGridResult result) => JsonSerializer.Serialize(new
    {
        regionId = result.RegionId.Value,
        lattice = new
        {
            result.Lattice.CountX,
            result.Lattice.CountY,
            result.Lattice.CountZ,
            result.Lattice.TotalCellCount,
            result.Lattice.CellSize,
        },
        counts = new
        {
            inside = result.InsideCellCount,
            outside = result.OutsideCellCount,
            cut = result.CutCellCount,
            geometrySamples = result.GeometrySampleCount,
        },
        result.EstimatedOccupiedVolume,
        cells = result.Cells.Select(cell => new { cell.Index, cell.Classification, cell.OccupancyEstimate }),
        cutCells = result.CutCells.Select(cell => new
        {
            cell.Index,
            cell.OccupancyEstimate,
            boundaryReferences = cell.BoundaryReferences,
        }),
    }, Options);
}
