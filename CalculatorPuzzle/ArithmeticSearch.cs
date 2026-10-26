namespace CalculatorPuzzle;

/// <summary>
/// One-operator lines with +, -, × and %, and a->c / b->c / == / AX lines (an operator plus one key, no =): edit S into X, press the operator, type D, =, edit the
/// result Y into G. D is fixed by X and Y (Y − X, X − Y, Y / X). Also edits-only lines (S → G
/// with left edits alone). Every line is checked in the simulator and against the charges.
/// </summary>
public static class ArithmeticSearch
{
    public const int MaxLines = 100;

    // With left-side transforms, at most this many X (before the operator) and Y (after =) displays
    // come from the left-side search each.
    public const int MaxBfsX = 200;
    public const int MaxBfsY = 200;

    /// <summary>Op is null for an edits-only line; then Built = S, Operand = 0, Result = S.</summary>
    public record Line(Key? Op, string Built, long Operand, long Result,
                       IReadOnlyList<Press> Presses, int Cost, int Order);

    // A candidate is kept small (indexes into xs and ys) because there can be about a million.
    readonly record struct Candidate(int X, int Y, Key Op, long Operand, int Cost);

    // A % result stays exact in the game's float up to 2^24.
    const long MaxPercentResult = 1 << 24;

    /// <summary>Every one-press operand key with the number it types: digit keys, 10, the charge key (its value now), the quest key (its digit) and the mw key (its stored number).</summary>
    static List<(long Value, Press Press)> RemorphKeys(Buttons buttons)
    {
        var keys = new List<(long, Press)>();
        foreach (var d in buttons.DigitKeys.Order())
            keys.Add((d, new Press(Key.Digit, d)));
        if (buttons.Ten)
            keys.Add((10, new Press(Key.Ten)));
        if (buttons.ChargeStart is { } c && buttons.Charges.GetValueOrDefault("c", int.MaxValue) >= 1)
            keys.Add((c, new Press(Key.Charge, c)));
        if (buttons.QuestDigit is { } q && buttons.Charges.GetValueOrDefault("q", 1) >= 1)
            keys.Add((q, new Press(Key.Quest, q)));
        if (buttons.MemoryValue is { } m && buttons.Charges.GetValueOrDefault("m", int.MaxValue) >= 1)
            keys.Add((m, new Press(Key.Memory, m)));
        return keys;
    }

    static int DigitCount(long d)
    {
        int n = 1;
        for (; d >= 10; d /= 10)
            n++;
        return n;
    }

    /// <summary>The Y displays (pieces of G) with the edits that turn each into G; with a left-side search map, its cheapest extra Y too.</summary>
    internal static List<(long Value, IReadOnlyList<Press> Post)> BuildYs(
        string g, Buttons buttons, int maxCost, Func<string, string, IReadOnlyList<Press>?> plan,
        Dictionary<string, IReadOnlyList<Press>>? reaching)
    {
        var ys = new List<(long Value, IReadOnlyList<Press> Post)>();
        foreach (var piece in DivisionShortcut.Pieces(g, buttons))
            if (plan(piece, g) is { } post && 2 + post.Count <= maxCost && long.TryParse(piece, out var y))
                ys.Add((y, post));

        if (reaching != null)
        {
            var seenY = new Dictionary<long, int>();
            for (int i = 0; i < ys.Count; i++)
                if (!seenY.TryGetValue(ys[i].Value, out var at) || ys[at].Post.Count > ys[i].Post.Count)
                    seenY[ys[i].Value] = i;
            foreach (var (display, post) in reaching
                         .Where(r => r.Value.Count + 2 <= maxCost && (r.Key == "0" || r.Key[0] != '0'))
                         .Select(r => (r.Key, r.Value, Text: string.Join(' ', r.Value)))
                         .OrderBy(r => r.Value.Count).ThenBy(r => r.Text, StringComparer.Ordinal)
                         .Take(MaxBfsY).Select(r => (r.Key, r.Value)))
            {
                var y = long.Parse(display);
                if (seenY.TryGetValue(y, out var at))
                {
                    if (ys[at].Post.Count <= post.Count)
                        continue;
                    ys[at] = (y, post);
                }
                else
                {
                    seenY[y] = ys.Count;
                    ys.Add((y, post));
                }
            }
        }
        return ys;
    }

    /// <summary>The X displays (S with prefix, suffix and flip changes) with the edits that build each from S; with a left-side search map, its cheapest extra X too.</summary>
    internal static List<(long Value, string Built, IReadOnlyList<Press> Pre)> BuildXs(
        string s, Buttons buttons, int maxCost, Func<string, string, IReadOnlyList<Press>?> plan,
        Dictionary<string, IReadOnlyList<Press>>? reachable, CancellationToken cancel)
    {
        var xs = new List<(long Value, string Built, IReadOnlyList<Press> Pre)>();
        var prefixes = DivisionShortcut.Prefixes(buttons);
        var suffixes = DivisionShortcut.Suffixes();
        foreach (var core in DivisionShortcut.Cores(s, buttons))
            foreach (var t in suffixes)
            {
                cancel.ThrowIfCancellationRequested();
                foreach (var a in prefixes)
                {
                    var built = a + core + t;
                    if (!long.TryParse(built, out var x))
                        continue;
                    if (plan(s, built) is { } pre && pre.Count + 2 <= maxCost)
                        xs.Add((x, built, pre));
                }
            }

        if (reachable != null)
        {
            var seenX = new Dictionary<string, int>();
            for (int i = 0; i < xs.Count; i++)
                if (!seenX.TryGetValue(xs[i].Built, out var at) || xs[at].Pre.Count > xs[i].Pre.Count)
                    seenX[xs[i].Built] = i;
            foreach (var (display, pre) in reachable
                         .Where(r => r.Value.Count + 2 <= maxCost && long.TryParse(r.Key, out _))
                         .Select(r => (r.Key, r.Value, Text: string.Join(' ', r.Value)))
                         .OrderBy(r => r.Value.Count).ThenBy(r => r.Text, StringComparer.Ordinal)
                         .Take(MaxBfsX).Select(r => (r.Key, r.Value)))
            {
                if (seenX.TryGetValue(display, out var at))
                {
                    if (xs[at].Pre.Count <= pre.Count)
                        continue;
                    xs[at] = (xs[at].Value, display, pre);
                }
                else
                {
                    seenX[display] = xs.Count;
                    xs.Add((long.Parse(display), display, pre));
                }
            }
        }
        return xs;
    }

    /// <summary>
    /// Lines with cost at most maxCost, cheapest first (ties by press text), up to maxLines.
    /// Throws <see cref="OperationCanceledException"/> when the token is cancelled.
    /// accept drops a line before it is kept, so maxLines counts accepted lines only. onlyOps limits the
    /// operators (and remorph keys) tried and skips the edits-only lines, which have no operator.
    /// plusOne is boss rule 12 (every = adds 1): the edits aim at goal − 1, a mid-line = result is 1 higher, and every line is checked
    /// with <see cref="EngineeringCalculator.Sent"/>. S = goal − 1 gives the single bare = line.
    /// bareSend (the near-one passive): with S == goal and no plusOne, the single bare = line, which sends S itself. Without it S == goal gives no line.
    /// </summary>
    public static IReadOnlyList<Line> Find(long start, long goal, Buttons buttons, int maxCost,
                                           CancellationToken cancel = default, int maxLines = MaxLines,
                                           Func<IReadOnlyList<Press>, bool>? accept = null,
                                           IReadOnlyCollection<Key>? onlyOps = null, bool plusOne = false, bool bareSend = false)
    {
        cancel.ThrowIfCancellationRequested();
        if (bareSend && !plusOne && start == goal && start >= 0)
        {
            // The near-one passive: sending S itself is within 1 of the real goal.
            IReadOnlyList<Press> bare = [new Press(Key.Equals)];
            if (onlyOps != null || (accept != null && !accept(bare)) || !buttons.Fits(bare))
                return [];
            return [new Line(null, start.ToString(), 0, start, bare, EngineeringCalculator.Cost(bare), 0)];
        }
        // t is the display just before the send.
        long t = plusOne ? goal - 1 : goal;
        if (start < 0 || goal < (plusOne ? 1 : 0) || (!plusOne && start == goal))
            return [];
        var s = start.ToString();
        var g = t.ToString();

        var planCache = new Dictionary<(string, string), IReadOnlyList<Press>?>();
        IReadOnlyList<Press>? Plan(string from, string to)
        {
            if (!planCache.TryGetValue((from, to), out var plan))
                planCache[(from, to)] = plan = LeftEdits.Plan(from, to, buttons);
            return plan;
        }

        var lines = new List<Line>();
        var seen = new HashSet<string>();

        bool Keep(IReadOnlyList<Press> presses, Key? op, string built, long operand, long result)
        {
            if (accept != null && !accept(presses))
                return false;
            if (!seen.Add(string.Join(' ', presses)))
                return false;
            if (EngineeringCalculator.Sent(start, presses, buttons, plusOne) != goal || !buttons.Fits(presses))
                return false;
            lines.Add(new Line(op, built, operand, result, presses, EngineeringCalculator.Cost(presses), lines.Count));
            return true;
        }

        // Cheapest first, ties by press text; Order is the position in that list.
        IReadOnlyList<Line> Ordered() => lines
            .OrderBy(l => l.Cost).ThenBy(l => string.Join(' ', l.Presses), StringComparer.Ordinal)
            .Select((l, i) => l with { Order = i }).ToList();

        // With transforms the left-side search replaces LeftEdits; its two maps also give extra X and Y.
        Dictionary<string, IReadOnlyList<Press>>? reachable = null, reaching = null;
        if (plusOne && start == t)
        {
            // S = goal − 1: the send alone.
            if (onlyOps == null)
                Keep([new Press(Key.Equals)], null, s, 0, start);
            return Ordered();
        }
        bool transforms = buttons.Transforms.Count > 0;
        if (transforms)
        {
            var limits = LeftSearch.DefaultLimits(s, g, Math.Max(maxCost - 2, (maxCost + 1) / 2));
            reachable = LeftSearch.Forward(s, buttons, limits, cancel);
            reaching = LeftSearch.Backward(g, buttons, limits, cancel);
            if (onlyOps == null)
                foreach (var edits in LeftSearch.Join(start, g, buttons, reachable, reaching, maxCost, 10))
                    Keep(edits, null, s, 0, start);
        }
        else if (onlyOps == null && Plan(s, g) is { } direct && direct.Count <= maxCost)
            Keep(direct, null, s, 0, start);

        bool Allowed(Key key) => buttons.Has(key) && (onlyOps == null || onlyOps.Contains(key));
        var ops = new[] { Key.Add, Key.Subtract, Key.Multiply, Key.Percent }.Where(Allowed).ToList();
        var remorphs = new[] { Key.RemorphA, Key.RemorphB, Key.AllDigits, Key.Complement }.Where(Allowed).ToList();
        if ((ops.Count == 0 && remorphs.Count == 0) || !buttons.HasOperandKeys)
            return Ordered();

        var ys = BuildYs(g, buttons, maxCost, Plan, reaching);
        var xs = BuildXs(s, buttons, maxCost, Plan, reachable, cancel);

        var operandCache = new Dictionary<long, IReadOnlyList<Press>?>();
        // % operands come from the cheapest typeable D in a range, so they are kept apart from the others.
        var percentOperands = new Dictionary<long, IReadOnlyList<Press>>();
        var percentRanges = new Dictionary<(long, long, int), (long, IReadOnlyList<Press>)?>();
        var remorphKeys = RemorphKeys(buttons);
        var candidates = new List<Candidate>();
        for (int xi = 0; xi < xs.Count; xi++)
        {
            if (xi % 1024 == 0)
                cancel.ThrowIfCancellationRequested();
            var (x, _, pre) = xs[xi];
            for (int yi = 0; yi < ys.Count; yi++)
            {
                var (y, post) = ys[yi];
                int budget = maxCost - pre.Count - 1 - post.Count;
                if (budget < 1)
                    continue;
                // With edits after the =, that = adds 1, so the operation gives y − 1.
                long want = y - (plusOne && post.Count > 0 ? 1 : 0);
                if (want < 0)
                    continue;
                foreach (var op in ops)
                {
                    if (op == Key.Percent)
                    {
                        // floor(x·D / 100) = y  exactly when  ceil(100y / x) <= D <= floor((100y + 99) / x)
                        if (x < 1 || want > MaxPercentResult)
                            continue;
                        long low = (100 * want + x - 1) / x, high = (100 * want + 99) / x;
                        if (low > high || DigitCount(high) > buttons.OperandWidth * budget)
                            continue;
                        if (!percentRanges.TryGetValue((low, high, budget), out var found))
                            percentRanges[(low, high, budget)] = found =
                                DivisionShortcut.CheapestDivisor(low, high, buttons, budget, minimum: 1);
                        if (found is not { } best)
                            continue;
                        percentOperands[best.Item1] = best.Item2;
                        candidates.Add(new Candidate(xi, yi, op, best.Item1, pre.Count + 1 + best.Item2.Count + post.Count));
                        continue;
                    }
                    long d = op switch
                    {
                        Key.Add => want - x,
                        Key.Subtract => x - want,
                        _ => x >= 1 && want % x == 0 ? want / x : -1,
                    };
                    // × 0 reaches G = 0; × 1 does nothing.
                    if (op == Key.Multiply ? d < 0 || d == 1 : d < 1)
                        continue;
                    if (DigitCount(d) > buttons.OperandWidth * budget)
                        continue;
                    if (!operandCache.TryGetValue(d, out var typed))
                        operandCache[d] = typed = DivisionShortcut.TypeOperand(d.ToString(), buttons);
                    if (typed == null || typed.Count > budget)
                        continue;
                    candidates.Add(new Candidate(xi, yi, op, d, pre.Count + 1 + typed.Count + post.Count));
                }
            }
        }

        // a->c / b->c / == / AX: Y is fixed by X and the one key, so every X is tried with every key.
        if (remorphs.Count > 0)
        {
            var yByValue = new Dictionary<long, int>();
            for (int i = 0; i < ys.Count; i++)
                if (!yByValue.TryGetValue(ys[i].Value, out var at) || ys[at].Post.Count > ys[i].Post.Count)
                    yByValue[ys[i].Value] = i;
            var noPlan = new HashSet<long>();
            for (int xi = 0; xi < xs.Count; xi++)
            {
                if (xi % 1024 == 0)
                    cancel.ThrowIfCancellationRequested();
                var (x, built, pre) = xs[xi];
                foreach (var op in remorphs)
                    for (int ki = 0; ki < remorphKeys.Count; ki++)
                    {
                        long k = remorphKeys[ki].Value;
                        long y;
                        switch (op)
                        {
                            case Key.RemorphA: y = x % 10 + k * 10; break;
                            case Key.RemorphB: y = x / 10 * 10 + k; break;
                            case Key.AllDigits: y = long.Parse(EngineeringCalculator.AllDigitsOf(x, k.ToString())); break;
                            default:
                                // AX works on a display of fewer than 6 characters and the solver keeps results of at most 6.
                                if (built.Length >= EngineeringCalculator.MaxDisplay)
                                    continue;
                                y = long.Parse(EngineeringCalculator.ComplementOf(x, k.ToString()));
                                if (y.ToString().Length > EngineeringCalculator.MaxDisplay)
                                    continue;
                                break;
                        }
                        if (!yByValue.TryGetValue(y, out var yi))
                        {
                            if (noPlan.Contains(y) || pre.Count + 2 > maxCost)
                                continue;
                            if (Plan(y.ToString(), g) is not { } post || post.Count + pre.Count + 2 > maxCost)
                            {
                                noPlan.Add(y);
                                continue;
                            }
                            yByValue[y] = yi = ys.Count;
                            ys.Add((y, post));
                        }
                        int cost = pre.Count + 2 + ys[yi].Post.Count;
                        if (cost <= maxCost)
                            candidates.Add(new Candidate(xi, yi, op, ki, cost));
                    }
            }
        }

        // One cost level at a time: only the cheap levels are ever turned into presses.
        foreach (var level in candidates.GroupBy(c => c.Cost).OrderBy(l => l.Key))
        {
            cancel.ThrowIfCancellationRequested();
            var built = level.Select(c =>
            {
                var (_, text, pre) = xs[c.X];
                var (y, post) = ys[c.Y];
                IReadOnlyList<Press> presses = c.Op switch
                {
                    Key.RemorphA or Key.RemorphB or Key.AllDigits or Key.Complement =>
                        [.. pre, new Press(c.Op), remorphKeys[(int)c.Operand].Press, .. post],
                    Key.Percent => [.. pre, new Press(c.Op), .. percentOperands[c.Operand], new Press(Key.Equals), .. post],
                    _ => [.. pre, new Press(c.Op), .. operandCache[c.Operand]!, new Press(Key.Equals), .. post],
                };
                long operand = c.Op is Key.RemorphA or Key.RemorphB or Key.AllDigits or Key.Complement
                    ? remorphKeys[(int)c.Operand].Value : c.Operand;
                return (Candidate: c, Presses: presses, Text: string.Join(' ', presses), Built: text, Result: y, Operand: operand);
            }).OrderBy(b => b.Text, StringComparer.Ordinal);
            foreach (var b in built)
            {
                Keep(b.Presses, b.Candidate.Op, b.Built, b.Operand, b.Result);
                if (lines.Count >= maxLines)
                    return Ordered();
            }
        }
        return Ordered();
    }
}
