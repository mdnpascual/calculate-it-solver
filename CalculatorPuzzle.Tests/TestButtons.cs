namespace CalculatorPuzzle.Tests;

/// <summary>Parses button lists for tests: every token without ":n" gets ":9" (charges are required).</summary>
static class TestButtons
{
    public static (Buttons? Buttons, string? Error) Parse(string text, long? last = null, long? goal = null, long? money = null) =>
        Buttons.Parse(string.Join(",", text.Split(',').Select(raw =>
        {
            var token = raw.Trim();
            return token.Length == 0 || token == "=" || token.Contains(':') ? raw : raw + ":9";
        })), last, goal, money);
}
