using Aetheris.Geometry;

namespace Aetheris.Web.Runtime;

public static partial class Program
{
    private sealed partial class WebModelSession
    {
        private IReadOnlyList<MeasurementTarget> _measurementTargets = [];
        private IReadOnlyList<MeasurementTarget> _pendingMeasurementTargets = [];

        public object Measure(WebRequest request)
        {
            if (_dirty || request.BuildRevision != Revision)
                throw new WebRuntimeException("measurement-stale-revision", "Build the current source successfully before measuring.");
            if (_measurementTargets.Count == 0)
                throw new WebRuntimeException("measurement-geometry-unavailable", "This build has no retained BRep measurement geometry. Scene and mesh-only measurement is not yet supported.");
            if (request.Measurement is null) throw new WebRuntimeException("measurement-request-invalid", "A measurement request is required.");
            try { return new { revision = Revision, measurement = BrepMeasurementQuery.Measure(_measurementTargets, request.Measurement) }; }
            catch (MeasurementException error) { throw new WebRuntimeException(error.Code, error.Message); }
        }
    }
}
