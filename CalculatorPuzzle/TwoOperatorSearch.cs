namespace CalculatorPuzzle;

/// <summary>
/// Two-operator lines: edit S into X1, op1 D1 =, the middle value M, op2 D2 =, edit the result Y2 into G (a->c / b->c / == / AX take one key and no =).
/// Meet in the middle on M: the forward half keeps the cheapest route S → M per cost, the backward half asks, for every
/// (Y2, op2, D2), which M give Y2. Every line is checked in the simulator and against the charges.
/// </summary>
public static class TwoOperatorSearch
{
    public const int MaxLines = 100;
    public const int MaxOperandPresses = 3;   // per segment
    public const int MaxMiddle = 200_000;     // distinct M values kept from the forward half

    const int MaxModSteps = 1_000;
    const int MaxCandidatesPerCost = 1_000_000;
    const long MaxPercentResult = 1 << 24;

    /// <summary>Op1 D1 gives Middle; Op2 D2 gives Result (before the post edits).</summary>
    public record Line(Key Op1, long Operand1, long Middle, Key Op2, long Operand2, long Result,
                       IReadOnlyList<Press> Presses, int Cost, int Order);

    internal readonly record struct Operand(long Value, IReadOnlyList<Press> Presses, string Text);

    readonly record struct Route(int X, Key Op, int D);

    // Backward half: Y2 index, operator and operand index.
    readonly record struct Back(int Y, Key Op, int D);

    readonly record struct Candidate(long M, int Table, int Level, Back Back);

    /// <summary>The forward half: for each press cost, the routes to each M whose cheapest route has that cost.</summary>
    sealed class Table(int levels)
    {
        public readonly Dictionary<long, Route>[] Routes = Enumerable.Range(0, levels).Select(_ => new Dictionary<long, Route>()).ToArray();
        readonly Dictionary<long, int> best = [];
        public long[][] Sorted = [];
        public List<long>[][] ByLastDigit = [];
        public long MaxKey;
        public int Count => best.Count;

        public void Add(long m, int cost, Route route)
        {
            if (best.TryGetValue(m, out var at))
            {
                if (at <= cost)
                    return;
                Routes[at].Remove(m);
            }
            best[m] = cost;
            Routes[cost][m] = route;
        }

        public void Index()
        {
            Sorted = Routes.Select(r => r.Keys.Order().ToArray()).ToArray();
            ByLastDigit = Sorted.Select(keys =>
            {
                var digits = Enumerable.Range(0, 10).Select(_ => new List<long>()).ToArray();
                foreach (var k in keys)
                    digits[k % 10].Add(k);
                return digits;
            }).ToArray();
            MaxKey = Sorted.Where(k => k.Length > 0).Select(k => k[^1]).DefaultIfEmpty(0).Max();
        }
    }

    /// <summary>Every operand of 1..MaxOperandPresses operand-key presses, the cheapest sequence per number, cheapest first (ties by press text).</summary>
    internal static List<Operand> Operands(Buttons buttons, int maxPresses = MaxOperandPresses)
    {
        var best = new Dictionary<long, Operand>();
        var path = new List<Press>();

        void Offer(string text, Press press, int charges, bool quest)
        {
            // A key on an operand of MaxOperandTyping characters would restart it in the game.
            if (text.Length >= EngineeringCalculator.MaxOperandTyping)
                return;
            // A button can't be pressed more often than its charges within one operand.
            if (path.Count(p => p.ChargeKey == press.ChargeKey) >= buttons.Charges.GetValueOrDefault(press.ChargeKey, int.MaxValue))
                return;
            text += press.Key switch { Key.Ten => "10", _ => press.Digit.ToString() };
            // A leading zero would only type the same number with more presses.
            if (text.Length > 1 && text[0] == '0')
                return;
            path.Add(press);
            long value = long.Parse(text);
            var candidate = new Operand(value, path.ToArray(), string.Join(' ', path));
            if (!best.TryGetValue(value, out var current) || candidate.Presses.Count < current.Presses.Count
                || (candidate.Presses.Count == current.Presses.Count && string.CompareOrdinal(candidate.Text, current.Text) < 0))
                best[value] = candidate;
            if (path.Count < maxPresses)
                Next(text, press.Key == Key.Charge ? charges + 1 : charges, press.Key == Key.Quest || quest);
            path.RemoveAt(path.Count - 1);
        }

        void Next(string text, int charges, bool quest)
        {
            foreach (var d in buttons.DigitKeys.Order())
                Offer(text, new Press(Key.Digit, d), charges, quest);
            if (buttons.Ten)
                Offer(text, new Press(Key.Ten), charges, quest);
            if (buttons.ChargeValue(charges) is { } v)
                Offer(text, new Press(Key.Charge, v), charges, quest);
            if (buttons.QuestDigit is { } q && !quest)
                Offer(text, new Press(Key.Quest, q), charges, quest);
            if (buttons.MemoryValue is { } m)
                Offer(text, new Press(Key.Memory, m), charges, quest);
        }

        Next("", 0, false);
        return best.Values.OrderBy(o => o.Presses.Count).ThenBy(o => o.Text, StringComparer.Ordinal).ToList();
    }

    /// <summary>An operator that takes one key and no = (a->c, b->c, ==, AX).</summary>
    static bool IsRemorph(Key op) => op is Key.RemorphA or Key.RemorphB or Key.AllDigits or Key.Complement;

    /// <summary>What the one-key operator makes of x (display `built`) and the key typing d, or null when the game or the solver doesn't allow it.</summary>
    static long? OneKey(Key op, long x, string built, long d)
    {
        switch (op)
        {
            case Key.RemorphA: return x % 10 + d * 10;
            case Key.RemorphB: return x / 10 * 10 + d;
            case Key.AllDigits: return long.Parse(EngineeringCalculator.AllDigitsOf(x, d.ToString()));
            default:
                if (built.Length >= EngineeringCalculator.MaxDisplay)
                    return null;
                var made = long.Parse(EngineeringCalculator.ComplementOf(x, d.ToString()));
                return made.ToString().Length > EngineeringCalculator.MaxDisplay ? null : made;
        }
    }

    /// <summary>
    /// Lines with cost at most maxCost, cheapest first (ties by press text), up to maxLines.
    /// Throws <see cref="OperationCanceledException"/> when the token is cancelled.
    /// accept drops a line before it is kept, so maxLines counts accepted lines only. requiredOp limits the pairs to those
    /// with that operator as op1 or op2. onRejected is called for each line that passes accept and the simulator but not the charges.
    /// plusOne is boss rule 12 (every = adds 1): M is the first result + 1, op2 aims at Y2 − 1 when edits follow its =, and lines are
    /// checked with <see cref="EngineeringCalculator.Sent"/>.
    /// </summary>
    public static IReadOnlyList<Line> Find(long start, long goal, Buttons buttons, int maxCost,
                                           CancellationToken cancel = default, int maxLines = MaxLines,
                                           Func<IReadOnlyList<Press>, bool>? accept = null,
                                           Key? requiredOp = null, Action? onRejected = null, bool plusOne = false)
    {
        cancel.ThrowIfCancellationRequested();
        // Two segments are at least 2 presses each.
        if ((!plusOne && start == goal) || start < 0 || goal < (plusOne ? 1 : 0) || maxCost < 4 || !buttons.HasOperandKeys)
            return [];
        var ops = new[] { Key.Add, Key.Subtract, Key.Multiply, Key.Divide, Key.Mod, Key.Percent, Key.RemorphA, Key.RemorphB,
                          Key.AllDigits, Key.Complement }
            .Where(buttons.Has).ToList();
        if (ops.Count == 0)
            return [];
        var s = start.ToString();
        // The display just before the send.
        var g = (plusOne ? goal - 1 : goal).ToString();

        var planCache = new Dictionary<(string, string), IReadOnlyList<Press>?>();
        IReadOnlyList<Press>? Plan(string from, string to)
        {
            if (!planCache.TryGetValue((from, to), out var plan))
                planCache[(from, to)] = plan = LeftEdits.Plan(from, to, buttons);
            return plan;
        }

        // The edits around the two segments leave maxCost - 2 for each side (the other segment needs at least 2).
        int side = maxCost - 2;
        Dictionary<string, IReadOnlyList<Press>>? reachable = null, reaching = null;
        if (buttons.Transforms.Count > 0)
        {
            var limits = LeftSearch.DefaultLimits(s, g, Math.Max(side - 2, (side + 1) / 2));
            reachable = LeftSearch.Forward(s, buttons, limits, cancel);
            reaching = LeftSearch.Backward(g, buttons, limits, cancel);
        }
        var ys = ArithmeticSearch.BuildYs(g, buttons, side, Plan, reaching);
        var xs = ArithmeticSearch.BuildXs(s, buttons, side, Plan, reachable, cancel);
        if (xs.Count == 0 || ys.Count == 0)
            return [];
        var operands = Operands(buttons);

        // ---- Forward half: S → M ----
        // Table 0 holds every route; table 1 (only with requiredOp) holds the routes whose op1 is requiredOp.
        var tables = new List<Table> { new(side + 1) };
        if (requiredOp != null)
            tables.Add(new Table(side + 1));
        var xOrder = Enumerable.Range(0, xs.Count)
            .OrderBy(i => xs[i].Pre.Count).ThenBy(i => xs[i].Built, StringComparer.Ordinal).ToList();
        for (int n = 0; n < xOrder.Count && tables[0].Count < MaxMiddle; n++)
        {
            if (n % 64 == 0)
                cancel.ThrowIfCancellationRequested();
            int xi = xOrder[n];
            var (x, built, pre) = xs[xi];
            foreach (var op in ops)
                for (int di = 0; di < operands.Count; di++)
                {
                    int cost = pre.Count + 1 + operands[di].Presses.Count;
                    if (cost > side || (IsRemorph(op) && operands[di].Presses.Count > 1))
                        break;
                    long d = operands[di].Value;
                    long? m;
                    if (IsRemorph(op))
                        m = OneKey(op, x, built, d);
                    else if (d == 0 && op is Key.Add or Key.Subtract or Key.Divide or Key.Mod or Key.Percent
                             || d == 1 && op is Key.Multiply or Key.Divide)
                        continue;
                    else
                        m = EngineeringCalculator.Apply(op, x, d, out _);
                    if (m is not { } middle || middle == x)
                        continue;
                    // The first = adds 1 (a->c / b->c / == / AX have no =).
                    if (plusOne && !IsRemorph(op))
                        middle++;
                    var route = new Route(xi, op, di);
                    tables[0].Add(middle, cost, route);
                    if (requiredOp == op)
                        tables[1].Add(middle, cost, route);
                }
        }
        foreach (var table in tables)
            table.Index();
        if (tables[0].Count == 0)
            return [];

        // ---- Backward half: the (Y2, op2, D2) items, grouped by their press cost ----
        var backs = new List<Back>[side + 1];
        for (int b = 0; b <= side; b++)
            backs[b] = [];
        for (int yi = 0; yi < ys.Count; yi++)
            foreach (var op in ops)
                for (int di = 0; di < operands.Count; di++)
                {
                    int cost = ys[yi].Post.Count + 1 + operands[di].Presses.Count;
                    if (cost > side || (IsRemorph(op) && operands[di].Presses.Count > 1))
                        break;
                    long d = operands[di].Value;
                    if (d == 0 && op is Key.Add or Key.Subtract or Key.Divide or Key.Mod or Key.Percent or Key.Complement
                        || d == 1 && op is Key.Multiply or Key.Divide)
                        continue;
                    backs[cost].Add(new Back(yi, op, di));
                }

        var lines = new List<Line>();
        var seen = new HashSet<string>();

        bool Keep(IReadOnlyList<Press> presses, Candidate c, long operand1)
        {
            if (accept != null && !accept(presses))
                return false;
            if (!seen.Add(string.Join(' ', presses)))
                return false;
            if (EngineeringCalculator.Sent(start, presses, buttons, plusOne) != goal)
                return false;
            if (!buttons.Fits(presses))
            {
                onRejected?.Invoke();
                return false;
            }
            var route = tables[c.Table].Routes[c.Level][c.M];
            lines.Add(new Line(route.Op, operand1, c.M, c.Back.Op, operands[c.Back.D].Value, ys[c.Back.Y].Value,
                               presses, EngineeringCalculator.Cost(presses), lines.Count));
            return true;
        }

        IReadOnlyList<Line> Ordered() => lines
            .OrderBy(l => l.Cost).ThenBy(l => string.Join(' ', l.Presses), StringComparer.Ordinal)
            .Select((l, i) => l with { Order = i }).ToList();

        // One total cost at a time, cheapest first: only the cheap levels are ever turned into presses.
        var candidates = new List<Candidate>();
        for (int total = 4; total <= maxCost; total++)
        {
            cancel.ThrowIfCancellationRequested();
            candidates.Clear();
            for (int b = 2; b <= total - 2 && b <= side; b++)
            {
                int level = total - b;
                if (level > side)
                    continue;
                int n = 0;
                foreach (var back in backs[b])
                {
                    if (++n % 1024 == 0)
                        cancel.ThrowIfCancellationRequested();
                    // With requiredOp the pair needs it somewhere: as op2 (any route) or as op1 (table 1).
                    int t = requiredOp == null || back.Op == requiredOp ? 0 : 1;
                    var (y, post) = ys[back.Y];
                    // Edits after op2's = mean that = adds 1, so op2 gives y − 1.
                    if (plusOne && !IsRemorph(back.Op) && post.Count > 0)
                        y--;
                    if (y < 0)
                        continue;
                    Lookup(tables[t], t, level, y, back, operands[back.D].Value, candidates);
                    if (candidates.Count >= MaxCandidatesPerCost)
                        break;
                }
            }
            foreach (var group in candidates
                         .Select(c => (Candidate: c, Presses: BuildPresses(c), Operand1: operands[tables[c.Table].Routes[c.Level][c.M].D].Value))
                         .Select(p => (p.Candidate, p.Presses, p.Operand1, Text: string.Join(' ', p.Presses)))
                         .OrderBy(p => p.Text, StringComparer.Ordinal))
            {
                Keep(group.Presses, group.Candidate, group.Operand1);
                if (lines.Count >= maxLines)
                    return Ordered();
            }
        }
        return Ordered();

        IReadOnlyList<Press> BuildPresses(Candidate c)
        {
            var route = tables[c.Table].Routes[c.Level][c.M];
            var op1 = operands[route.D];
            var op2 = operands[c.Back.D];
            var presses = new List<Press>(xs[route.X].Pre);
            presses.Add(new Press(route.Op));
            presses.AddRange(op1.Presses);
            if (!IsRemorph(route.Op))
                presses.Add(new Press(Key.Equals));
            presses.Add(new Press(c.Back.Op));
            presses.AddRange(op2.Presses);
            if (!IsRemorph(c.Back.Op))
                presses.Add(new Press(Key.Equals));
            presses.AddRange(ys[c.Back.Y].Post);
            return presses;
        }
    }

    /// <summary>Adds a candidate for every M in the table's level that op2 with operand d turns into y.</summary>
    static void Lookup(Table table, int t, int level, long y, Back back, long d, List<Candidate> output)
    {
        var routes = table.Routes[level];
        if (routes.Count == 0)
            return;
        var sorted = table.Sorted[level];

        void Point(long m)
        {
            if (m != y && routes.ContainsKey(m))
                output.Add(new Candidate(m, t, level, back));
        }

        void Range(long low, long high)
        {
            low = Math.Max(low, 0);
            if (low > high)
                return;
            int at = Array.BinarySearch(sorted, low);
            if (at < 0)
                at = ~at;
            for (; at < sorted.Length && sorted[at] <= high && output.Count < MaxCandidatesPerCost; at++)
                if (sorted[at] != y)
                    output.Add(new Candidate(sorted[at], t, level, back));
        }

        switch (back.Op)
        {
            case Key.Add:
                if (y >= d)
                    Point(y - d);
                break;
            case Key.Subtract:
                if (y == 0)
                    Range(0, d);
                else
                    Point(y + d);
                break;
            case Key.Multiply:
                if (d == 0)
                {
                    if (y == 0)
                        Range(0, long.MaxValue);
                }
                else if (y % d == 0)
                    Point(y / d);
                break;
            case Key.Divide:
                if (y <= table.MaxKey / d)
                    Range(y * d, y * d + d - 1);
                break;
            case Key.Mod:
                if (d > y)
                    for (long m = y, steps = 0; m <= table.MaxKey && steps <= MaxModSteps; m += d, steps++)
                        Point(m);
                break;
            case Key.Percent:
                if (y <= MaxPercentResult)
                    Range((100 * y + d - 1) / d, (100 * y + 99) / d);
                break;
            case Key.RemorphA:
                {
                    // (M mod 10) + 10·d = y
                    long r = y - d * 10;
                    if (r is >= 0 and <= 9)
                        foreach (var m in table.ByLastDigit[level][r])
                            if (m != y)
                                output.Add(new Candidate(m, t, level, back));
                    break;
                }
            case Key.RemorphB:
                // (M / 10)·10 + d = y
                if (y % 10 == d)
                    Range(y - d, y - d + 9);
                break;
            case Key.AllDigits:
                {
                    // Every digit of M becomes d's first digit: y is that digit repeated as many times as M has digits.
                    char first = d.ToString()[0];
                    var text = y.ToString();
                    if (first == '0')
                    {
                        if (y == 0)
                            Range(0, table.MaxKey);
                    }
                    else if (text.All(c => c == first))
                        Range(text.Length == 1 ? 0 : (long)Math.Pow(10, text.Length - 1), (long)Math.Pow(10, text.Length) - 1);
                    break;
                }
            case Key.Complement:
                {
                    // y = d followed by M, where M is a number of n digits (n = 1..5: the display has fewer than 6 characters).
                    int n = y.ToString().Length - d.ToString().Length;
                    if (n is >= 1 and <= 5)
                    {
                        long pow = (long)Math.Pow(10, n), m = y % pow;
                        if (y / pow == d && (n == 1 || m >= pow / 10))
                            Point(m);
                    }
                    break;
                }
        }
    }
}
