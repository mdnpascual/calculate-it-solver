namespace CalculatorPuzzle;

/// <summary>
/// Just a one (Steam `UNIT`): an = that raises the number by exactly 1. The goal is ignored: lines run from S to S + 1 and have one =, the last press.
/// Reuses the arithmetic, division and modulo phases with that goal and an accept filter. Each line carries the operators_used entry it credits
/// (<see cref="Achievements.Credit"/>).
/// </summary>
public static class JustOneSearch
{
    /// <summary>The line's only = is its last press.</summary>
    public static bool OneEqualsAtEnd(IReadOnlyList<Press> presses) =>
        presses.Count > 0 && presses[^1].Key == Key.Equals && EngineeringCalculator.OneSend(presses);

    /// <summary>Error is an input error; Credits are the entries the lines credit, cheapest first.</summary>
    public sealed record Result(string? Error, int Arithmetic, int Division, int Modulo, IReadOnlyList<string> Credits);

    public static Result Search(long start, Buttons buttons, int maxCost, Action<ResultLine> onLine,
                                CancellationToken cancel, Action? onRejected = null)
    {
        if (start >= EngineeringCalculator.Max)
            return new Result("Just a one can't go above 999 999.", 0, 0, 0, []);
        long goal = start + 1;
        var kept = new List<ResultLine>();
        var gate = new object();

        bool Offer(ResultLine line)
        {
            if (line.Credit == null)
                return false;
            lock (gate)
                kept.Add(line);
            onLine(line);
            return true;
        }

        ResultLine Credited(ResultLine line) => line with { Credit = Achievements.Credit(line.Presses) };

        int divisionCap = maxCost;
        int arithmetic = 0, division = 0, modulo = 0;
        foreach (var line in ArithmeticSearch.Find(start, goal, buttons, maxCost, cancel, accept: OneEqualsAtEnd))
            if (Offer(Credited(ResultLine.From(line, buttons))))
            {
                arithmetic++;
                divisionCap = Math.Min(divisionCap, line.Cost + CostSlack);
            }

        string? error = DivisionShortcut.Search(start, goal, buttons, divisionCap,
            l =>
            {
                if (OneEqualsAtEnd(l.Presses) && Offer(Credited(ResultLine.From(l, buttons))))
                    Interlocked.Increment(ref division);
            }, cancel, onRejected: _ => onRejected?.Invoke());
        // Missing ÷ or operand keys only rule out the Division Shortcut; the other phases still run.
        _ = error;

        var seenText = new HashSet<string>();
        int cap = kept.Count > 0 ? Math.Min(maxCost, kept.Min(l => l.Cost) + CostSlack) : maxCost;
        var m = ModuloSearch.Find(start, goal, buttons, cap, cancel, accept: OneEqualsAtEnd);
        foreach (var line in m.Lines)
            if (Offer(Credited(ResultLine.From(line, buttons))))
            {
                modulo++;
                seenText.Add(string.Join(' ', line.Presses));
            }

        foreach (var line in ModLines(start, goal, buttons, maxCost, cancel))
            if (OneEqualsAtEnd(line) && !seenText.Contains(string.Join(' ', line)) && buttons.Fits(line)
                && Offer(Credited(ResultLine.From(new ArithmeticSearch.Line(Key.Mod, "", 0, goal, line, EngineeringCalculator.Cost(line), 0), buttons))))
                modulo++;

        var credits = kept.GroupBy(l => l.Credit!).OrderBy(g => g.Min(l => l.Cost)).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key).ToList();
        return new Result(null, arithmetic, division, modulo, credits);
    }

    const int CostSlack = 2;
    const long MaxOperand = 99_999;

    /// <summary>
    /// Plain `edits, mod, D, =` lines: X mod D = goal needs D &gt; goal and D to divide X − goal (any D &gt; X when X = goal).
    /// The Modulo Shortcut only prepends digits, so it can't give `X5 mod 10 =`.
    /// </summary>
    static IEnumerable<IReadOnlyList<Press>> ModLines(long start, long goal, Buttons buttons, int maxCost, CancellationToken cancel)
    {
        if (!buttons.Mod || !buttons.HasOperandKeys)
            yield break;
        string s = start.ToString(), g = goal.ToString();
        var planCache = new Dictionary<(string, string), IReadOnlyList<Press>?>();
        IReadOnlyList<Press>? Plan(string from, string to)
        {
            if (!planCache.TryGetValue((from, to), out var plan))
                planCache[(from, to)] = plan = LeftEdits.Plan(from, to, buttons);
            return plan;
        }
        Dictionary<string, IReadOnlyList<Press>>? reachable = null;
        if (buttons.Transforms.Count > 0)
            reachable = LeftSearch.Forward(s, buttons, LeftSearch.DefaultLimits(s, g, Math.Max(maxCost - 2, (maxCost + 1) / 2)), cancel);
        foreach (var (x, _, pre) in ArithmeticSearch.BuildXs(s, buttons, maxCost, Plan, reachable, cancel))
        {
            cancel.ThrowIfCancellationRequested();
            int budget = maxCost - pre.Count - 2;
            if (budget < 1 || x < goal)
                continue;
            var best = default(IReadOnlyList<Press>);
            if (x == goal)
            {
                if (DivisionShortcut.CheapestDivisor(goal + 1, MaxOperand, buttons, budget, minimum: 1) is { } found)
                    best = found.Presses;
            }
            else
            {
                long diff = x - goal;
                for (long d = 1; d * d <= diff; d++)
                {
                    if (diff % d != 0)
                        continue;
                    foreach (var divisor in new[] { d, diff / d })
                        if (divisor > goal && divisor <= MaxOperand
                            && DivisionShortcut.TypeOperand(divisor.ToString(), buttons) is { } typed
                            && typed.Count <= budget && (best == null || typed.Count < best.Count))
                            best = typed;
                }
            }
            if (best != null)
                yield return [.. pre, new Press(Key.Mod), .. best, new Press(Key.Equals)];
        }
    }

    /// <summary>
    /// Sorts in place: the cheapest line of each credit first (by <see cref="ResultLine.Ranking"/>), then the rest in that order.
    /// A comparer can't say "first of each group", so this is a function.
    /// </summary>
    public static void Order(List<ResultLine> lines)
    {
        lines.Sort(ResultLine.Ranking);
        var seen = new HashSet<string?>();
        var firsts = new List<ResultLine>();
        var rest = new List<ResultLine>();
        foreach (var line in lines)
            (seen.Add(line.Credit) ? firsts : rest).Add(line);
        lines.Clear();
        lines.AddRange(firsts);
        lines.AddRange(rest);
    }
}
