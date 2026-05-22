namespace Aetheris.Kernel.Core.Brep.Boolean;

public static class BooleanGuards
{
    public static bool RequireOperation(BooleanOperation actual, BooleanOperation expected, out string? reason)
    {
        if (actual == expected)
        {
            reason = null;
            return true;
        }

        reason = $"requires '{expected}' but received '{actual}'.";
        return false;
    }

    public static bool RequireOperation(BooleanOperation actual, BooleanOperation first, BooleanOperation second, out string? reason)
    {
        if (actual == first || actual == second)
        {
            reason = null;
            return true;
        }

        reason = $"requires '{first}' or '{second}' but received '{actual}'.";
        return false;
    }
}
