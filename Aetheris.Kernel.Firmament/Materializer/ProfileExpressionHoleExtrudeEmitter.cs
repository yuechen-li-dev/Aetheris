using Aetheris.Kernel.Core.Brep;

namespace Aetheris.Kernel.Firmament.Materializer;

internal abstract record ProfileExpression2D;
internal sealed record ProfileRectangleExpr2D(double CenterX, double CenterY, double Width, double Height) : ProfileExpression2D;
internal sealed record ProfileCircleExpr2D(double CenterX, double CenterY, double Radius) : ProfileExpression2D;
internal sealed record ProfileCapsuleExpr2D(double CenterX, double CenterY, double Length, double Radius) : ProfileExpression2D;
internal sealed record ProfileUnsupportedPrimitiveExpr2D(string Name) : ProfileExpression2D;
internal sealed record ProfileDifferenceExpr2D(ProfileExpression2D Left, IReadOnlyList<ProfileExpression2D> Rights) : ProfileExpression2D;
internal sealed record ProfileUnionExpr2D(IReadOnlyList<ProfileExpression2D> Operands) : ProfileExpression2D;
internal sealed record ProfileIntersectionExpr2D(ProfileExpression2D Left, ProfileExpression2D Right) : ProfileExpression2D;

internal enum ProfileExpressionHoleExtrudeStatus { Succeeded, Rejected, Deferred, Failed }

internal sealed record ProfileExpressionHoleExtrudeRequest(ProfileExpression2D Expression, double Height);
internal sealed record ProfileExpressionHoleExtrudeResult(ProfileExpressionHoleExtrudeStatus Status, BrepBody? Body, IReadOnlyList<string> Diagnostics);

internal static class ProfileExpressionHoleExtrudeEmitter
{
    private const double Tol = 1e-6;

    public static ProfileExpressionHoleExtrudeResult TryEmit(ProfileExpressionHoleExtrudeRequest request)
    {
        var d = new List<string> { "v2-v3-profile-expression-frontdoor-attempted", "v2-v3-profile-expression-normalization-attempted" };
        if (!TryNormalize(request.Expression, d, out var outer, out var holes, out var rejectedReason, out var deferredReason))
        {
            var isDeferred = !string.IsNullOrWhiteSpace(deferredReason);
            var status = isDeferred ? ProfileExpressionHoleExtrudeStatus.Deferred : ProfileExpressionHoleExtrudeStatus.Rejected;
            d.Add($"v2-v3-profile-expression-{(isDeferred ? "deferred" : "rejected")}:{(isDeferred ? deferredReason : rejectedReason)}");
            return new(status, null, Distinct(d));
        }

        d.Add("v2-v3-profile-expression-normalized");
        if (!double.IsFinite(request.Height) || request.Height <= Tol)
        {
            d.Add("v2-v3-profile-expression-rejected:emitter-validation-failure");
            return new(ProfileExpressionHoleExtrudeStatus.Rejected, null, Distinct(d));
        }

        d.Add("v2-v3-profile-expression-adapted-to-hole-emitter");
        d.Add("v2-v3-profile-hole-extrude-attempted");
        var emit = ProfileHoleExtrudeEmitter.TryEmit(new ProfileHoleExtrudeRequest(outer.Width, outer.Height, request.Height, holes));
        d.AddRange(emit.Diagnostics);
        if (emit.Status != ProfileHoleExtrudeStatus.Succeeded || emit.Body is null)
        {
            d.Add("v2-v3-profile-hole-extrude-failed:emitter-validation-failure");
            return new(ProfileExpressionHoleExtrudeStatus.Failed, null, Distinct(d));
        }

        d.Add("v2-v3-profile-hole-extrude-succeeded");
        d.Add("v2-v3-no-3d-boolean-used");
        return new(ProfileExpressionHoleExtrudeStatus.Succeeded, emit.Body, Distinct(d));
    }

    private static bool TryNormalize(ProfileExpression2D expr, List<string> d, out ProfileRectangleExpr2D outer, out List<ProfileHoleLoop2D> holes, out string rejectedReason, out string deferredReason)
    {
        outer = new(0, 0, 0, 0); holes = []; rejectedReason = string.Empty; deferredReason = string.Empty;
        if (expr is not ProfileDifferenceExpr2D(var left, var rights)) { rejectedReason = "unsupported-operation"; return false; }
        if (left is not ProfileRectangleExpr2D r) { rejectedReason = "invalid-rectangle"; return false; }
        if (!ValidRectangle(r)) { rejectedReason = "invalid-rectangle"; return false; }
        if (rights.Count == 0) { rejectedReason = "unsupported-topology"; return false; }

        var circles = new List<ProfileCircleExpr2D>();
        foreach (var x in rights)
        {
            switch (x)
            {
                case ProfileCircleExpr2D c:
                    if (c.Radius <= Tol || !double.IsFinite(c.Radius)) { rejectedReason = "invalid-circle"; return false; }
                    if (!CircleInside(c, r)) { rejectedReason = "circle-outside-rectangle"; return false; }
                    if (TouchesBoundary(c, r)) { rejectedReason = "circle-touches-boundary"; return false; }
                    circles.Add(c);
                    break;
                case ProfileCapsuleExpr2D:
                    deferredReason = "unsupported-primitive";
                    return false;
                case ProfileRectangleExpr2D:
                    deferredReason = "unsupported-topology";
                    return false;
                case ProfileUnsupportedPrimitiveExpr2D:
                    rejectedReason = "unsupported-primitive";
                    return false;
                default:
                    rejectedReason = "unsupported-operation";
                    return false;
            }
        }

        for (var i = 0; i < circles.Count; i++)
        for (var j = i + 1; j < circles.Count; j++)
        {
            var dx = circles[i].CenterX - circles[j].CenterX;
            var dy = circles[i].CenterY - circles[j].CenterY;
            if ((dx * dx) + (dy * dy) <= Math.Pow(circles[i].Radius + circles[j].Radius, 2d) + Tol)
            { rejectedReason = "circles-overlap-touch"; return false; }
        }

        outer = new(0, 0, r.Width, r.Height);
        holes = circles.Select(c => new ProfileHoleLoop2D(c.CenterX - r.CenterX, c.CenterY - r.CenterY, c.Radius)).ToList();
        return true;
    }

    private static bool ValidRectangle(ProfileRectangleExpr2D r) => double.IsFinite(r.Width) && double.IsFinite(r.Height) && r.Width > Tol && r.Height > Tol;
    private static bool CircleInside(ProfileCircleExpr2D c, ProfileRectangleExpr2D r)
    {
        var x0 = r.CenterX - r.Width / 2d; var x1 = r.CenterX + r.Width / 2d; var y0 = r.CenterY - r.Height / 2d; var y1 = r.CenterY + r.Height / 2d;
        return c.CenterX - c.Radius >= x0 - Tol && c.CenterX + c.Radius <= x1 + Tol && c.CenterY - c.Radius >= y0 - Tol && c.CenterY + c.Radius <= y1 + Tol;
    }
    private static bool TouchesBoundary(ProfileCircleExpr2D c, ProfileRectangleExpr2D r)
    {
        var x0 = r.CenterX - r.Width / 2d; var x1 = r.CenterX + r.Width / 2d; var y0 = r.CenterY - r.Height / 2d; var y1 = r.CenterY + r.Height / 2d;
        return Math.Abs((c.CenterX - c.Radius) - x0) <= Tol || Math.Abs((c.CenterX + c.Radius) - x1) <= Tol || Math.Abs((c.CenterY - c.Radius) - y0) <= Tol || Math.Abs((c.CenterY + c.Radius) - y1) <= Tol;
    }
    private static string[] Distinct(List<string> d) => d.Distinct().OrderBy(x => x).ToArray();
}
