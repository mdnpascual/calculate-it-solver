using System.Text.RegularExpressions;

namespace CalculatorPuzzle;

/// <summary>
/// The in-game button labels. Solver tokens stay the internal names; Label turns a token into what a person reads and
/// Token turns a label (or a token) back into a token before parsing. GameHook.MelonLoader/ButtonSymbols.cs has a copy of Label.
/// </summary>
public static class ButtonLabels
{
    static readonly Dictionary<string, string> Fixed = new()
    {
        ["money"] = "$", ["flip"] = "reverse", ["round"] = "->)", ["sup"] = "combine",
        ["sqr"] = "X²", ["median"] = "_2_", ["25|69"] = "mirror", ["=="] = "equalize",
        ["<<"] = "X<<", [">>"] = "X>>", ["near"] = "+-10", ["reset"] = "x->0", ["to25"] = "x->25",
    };

    // Lowercase label -> token.
    static readonly Dictionary<string, string> Reverse =
        Fixed.ToDictionary(p => p.Value.ToLowerInvariant(), p => p.Key);

    /// <summary>The label for a token: c12 -> volt12, q8 -> unk8, m5 -> mem5, mw -> mem, the fixed table, else the token itself.</summary>
    public static string Label(string token)
    {
        if (Fixed.TryGetValue(token, out var label))
            return label;
        if (Regex.Match(token, @"^c(\d*)$") is { Success: true } c)
            return "volt" + c.Groups[1].Value;
        if (Regex.Match(token, @"^q(\d?)$") is { Success: true } q)
            return "unk" + q.Groups[1].Value;
        if (Regex.Match(token, @"^m(\d+)$") is { Success: true } m)
            return "mem" + m.Groups[1].Value;
        return token is "mw" or "m" ? "mem" : token;
    }

    /// <summary>The token for a label, case-insensitive. A token or unknown text comes back unchanged. A bare <c>mem</c> is the mw key (<c>m</c>).</summary>
    public static string Token(string text)
    {
        var lower = text.ToLowerInvariant();
        if (Reverse.TryGetValue(lower, out var token))
            return token;
        if (Regex.Match(lower, @"^volt(\d*)$") is { Success: true } c)
            return "c" + c.Groups[1].Value;
        if (Regex.Match(lower, @"^unk(\d?)$") is { Success: true } q)
            return "q" + q.Groups[1].Value;
        if (Regex.Match(lower, @"^mem(\d*)$") is { Success: true } m)
            return "m" + m.Groups[1].Value;
        return text;
    }

    /// <summary>Presses joined by " | ", each as its label, with ÷ shown as /.</summary>
    public static string Line(IEnumerable<string> presses) =>
        string.Join(" | ", presses.Select(p => Label(p))).Replace('÷', '/');
}
