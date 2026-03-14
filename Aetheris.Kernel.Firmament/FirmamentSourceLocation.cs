namespace Aetheris.Kernel.Firmament;

public readonly record struct FirmamentSourcePosition
{
    public FirmamentSourcePosition(int line, int column)
    {
        if (line < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(line), "Line must be 1 or greater.");
        }

        if (column < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(column), "Column must be 1 or greater.");
        }

        Line = line;
        Column = column;
    }

    public int Line { get; }

    public int Column { get; }
}

public readonly record struct FirmamentSourceSpan
{
    public FirmamentSourceSpan(FirmamentSourcePosition start, FirmamentSourcePosition end)
    {
        Start = start;
        End = end;
    }

    public FirmamentSourcePosition Start { get; }

    public FirmamentSourcePosition End { get; }
}
