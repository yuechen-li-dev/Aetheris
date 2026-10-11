namespace Aetheris.Kernel.Firmament.FirmamentV2;

internal sealed class FirmamentBlockReader
{
    private readonly string source;
    private readonly Action<string, string> Fail;
    private readonly string errorCode;
    private readonly (string Text, string Shape, int Start, int Length)[] tokens;
    private int position;

    public FirmamentBlockReader(string source, Action<string, string> fail, string errorCode = "garment-syntax")
    {
        this.source = source;
        Fail = fail;
        this.errorCode = errorCode;
        tokens = FirmamentLanguageAnalysisService.Lex(source).Where(token => token.Shape != "comment")
            .Select(token => (token.Text, token.Shape, token.Start, token.Length)).ToArray();
    }

    public bool End => position >= tokens.Length;
    public string Peek => End ? "<end>" : tokens[position].Text;
    public string Take()
    {
        if (End)
        {
            Fail(errorCode, "Unexpected end of source block.");
        }
        return tokens[position++].Text;
    }
    public bool Optional(string text)
    {
        if (Peek != text)
        {
            return false;
        }
        position++;
        return true;
    }
    public void Expect(string text)
    {
        if (!Optional(text))
        {
            Fail(errorCode, "Expected '" + text + "', found '" + Peek + "'.");
        }
    }
    public string Identifier()
    {
        if (End || tokens[position].Shape != "identifier")
        {
            Fail(errorCode, "Expected an identifier, found " + Peek);
        }
        return Take();
    }
    public string Block()
    {
        Expect("{");
        int start = tokens[position - 1].Start + 1;
        int depth = 1;
        while (!End)
        {
            var token = tokens[position++];
            if (token.Text == "{")
            {
                depth++;
            }
            if (token.Text == "}")
            {
                depth--;
            }
            if (depth == 0)
            {
                return source[start..token.Start];
            }
        }
        Fail(errorCode, "Unclosed source block.");
        return "";
    }
    public void SkipDeclaration()
    {
        while (!End && Peek != "{")
        {
            Take();
        }
        Block();
        Optional(";");
    }
    public string Value()
    {
        if (End)
        {
            Fail(errorCode, "Missing field value.");
        }
        int start = tokens[position].Start;
        int end = start;
        int depth = 0;
        while (!End)
        {
            var token = tokens[position];
            if (depth == 0 && (token.Text == ";" || (position + 1 < tokens.Length && tokens[position + 1].Text == ":")))
            {
                break;
            }
            if (depth == 0 && end > start && token.Text is "Panel" or "Fabric" or "Interface" or "Drape" or "Fitting")
            {
                break;
            }
            if (token.Text is "[" or "(")
            {
                depth++;
            }
            if (token.Text is "]" or ")")
            {
                depth--;
            }
            if (depth < 0)
            {
                Fail(errorCode, "Unbalanced field value.");
            }
            end = token.Start + token.Length;
            position++;
        }
        if (depth != 0 || end == start)
        {
            Fail(errorCode, "Missing or unbalanced field value.");
        }
        Optional(";");
        return source[start..end].Trim();
    }
}
