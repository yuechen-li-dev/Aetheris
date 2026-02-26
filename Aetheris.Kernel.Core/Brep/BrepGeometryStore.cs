using Aetheris.Kernel.Core.Geometry;

namespace Aetheris.Kernel.Core.Brep;

/// <summary>
/// In-memory geometry definition store keyed by typed geometry IDs.
/// </summary>
public sealed class BrepGeometryStore
{
    private readonly Dictionary<CurveGeometryId, CurveGeometry> _curves = [];
    private readonly Dictionary<SurfaceGeometryId, SurfaceGeometry> _surfaces = [];

    public IEnumerable<KeyValuePair<CurveGeometryId, CurveGeometry>> Curves => _curves;

    public IEnumerable<KeyValuePair<SurfaceGeometryId, SurfaceGeometry>> Surfaces => _surfaces;

    public void AddCurve(CurveGeometryId id, CurveGeometry curve) => _curves.Add(id, curve);

    public void AddSurface(SurfaceGeometryId id, SurfaceGeometry surface) => _surfaces.Add(id, surface);

    public bool TryGetCurve(CurveGeometryId id, out CurveGeometry? curve) => _curves.TryGetValue(id, out curve);

    public bool TryGetSurface(SurfaceGeometryId id, out SurfaceGeometry? surface) => _surfaces.TryGetValue(id, out surface);

    public CurveGeometry GetCurve(CurveGeometryId id) => _curves[id];

    public SurfaceGeometry GetSurface(SurfaceGeometryId id) => _surfaces[id];
}
