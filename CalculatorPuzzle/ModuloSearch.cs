namespace CalculatorPuzzle;

/// <summary>
/// Searches Modulo Shortcut lines over every factor pair, A and k, and turns each valid formula into
/// presses (left edits, mod, operand, =) with a cost.
/// </summary>
public static class ModuloSearch
{
    public const int MaxLines = 100;
    public const int MaxA = 99;

    public record Line(long A, int K, long Value, long Modulus, IReadOnlyList<Press> Presses, int Cost, int Order);

    /// <summary>Lines found and the last A looked at (for the status).</summary>
    public record Result(string? Error, IReadOnlyList<Line> Lines, long LastA);

    /// <summary>Presses for one formula (left edits, mod, operand, =), or null when the buttons can't build it.</summary>
    public static IReadOnlyList<Press>? Presses(long start, int k, ModuloShortcut.Formula formula, Buttons buttons)
        => Presses(start, k, formula, buttons, new Dictionary<string, IReadOnlyList<Press>?>());

    static IReadOnlyList<Press>? Presses(long start, int k, ModuloShortcut.Formula formula, Buttons buttons,
                                         Dictionary<string, IReadOnlyList<Press>?> planCache)
    {
        if (!buttons.Mod)
            return null;
        var target = formula.Prepend + start.ToString().PadLeft(k, '0');
        if (!planCache.TryGetValue(target, out var pre))
            planCache[target] = pre = LeftEdits.Plan(start.ToString(), target, buttons);
        if (pre == null)
            return null;
        var operand = DivisionShortcut.TypeOperand(formula.Modulus.ToString(), buttons);
        if (operand == null)
            return null;
        return [.. pre, new Press(Key.Mod), .. operand, new Press(Key.Equals)];
    }

    /// <summary>
    /// With plusOne (boss rule 12) every line is checked with <see cref="EngineeringCalculator.Sent"/>.
    /// Keeps valid formulas with cost at most maxCost, up to maxLines, for A = 1..maxA. accept drops a line before it is kept, so maxLines counts accepted lines only. Throws
    /// <see cref="OperationCanceledException"/> when the token is cancelled.
    /// </summary>
    public static Result Find(long start, long goal, Buttons buttons, int maxCost,
                              CancellationToken cancel = default,
                              int maxLines = MaxLines, long maxA = MaxA,
                              Func<IReadOnlyList<Press>, bool>? accept = null, bool plusOne = false)
    {
        // plusOne is boss rule 12: the final = adds 1, so the formula aims at goal − 1.
        long target = plusOne ? goal - 1 : goal;
        if (start == target || (plusOne && goal < 1))
            return new Result(null, [], 0);
        if (!buttons.Mod || !buttons.HasOperandKeys)
            return new Result("The Modulo Shortcut needs mod and 10 or a digit key.", [], 0);

        int startLength = start.ToString().Length;
        var seen = new HashSet<string>();
        var lines = new List<Line>();
        var planCache = new Dictionary<string, IReadOnlyList<Press>?>();
        
        // Fewest presses any line with this k can take: padding, one prepended digit, mod, the operand.
        int LowerBound(int k) => (k - startLength) + 1 + 1 + OperandBound(k + 1);
        int OperandBound(int digits) => (digits + buttons.OperandWidth - 1) / buttons.OperandWidth;

        long a = 0;
        for (a = 1; a <= maxA; a++)
        {
            for (int k = ModuloShortcut.MinK(start); LowerBound(k) <= maxCost && k + 1 <= EngineeringCalculator.MaxDisplay; k++)
            {
                cancel.ThrowIfCancellationRequested();
                var r = ModuloShortcut.Solve(start, target, a, k);
                if (r.Error != null)
                {
                    if (r.Error.StartsWith("Numbers are too large"))
                        break;
                    continue;
                }
                foreach (var pair in r.Pairs)
                    foreach (var f in new[] { pair.First, pair.Second })
                    {
                        if (!f.Valid)
                            continue;
                        int quick = (k - startLength) + f.Prepend.ToString().Length + 1 + OperandBound(f.Modulus.ToString().Length);
                        if (quick > maxCost)
                            continue;
                        var presses = Presses(start, k, f, buttons, planCache);
                        if (presses == null)
                            continue;
                        int cost = EngineeringCalculator.Cost(presses);
                        if (cost > maxCost || (accept != null && !accept(presses)) || !seen.Add(string.Join(' ', presses)))
                            continue;
                        if (EngineeringCalculator.Sent(start, presses, buttons, plusOne) != goal || !buttons.Fits(presses))
                            continue;
                        lines.Add(new Line(a, k, f.Value, f.Modulus, presses, cost, lines.Count));
                        if (lines.Count == maxLines)
                            return new Result(null, lines, a);
                    }
            }
        }
        return new Result(null, lines, Math.Min(a, maxA));
    }
}
