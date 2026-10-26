namespace CalculatorPuzzle;

/// <summary>
/// School mathematics (Steam `SCHOOL`): lines from S to G that apply +, -, × and ÷ at least once each, every one as `op operand =`.
/// Best-first search (Dijkstra on press cost) over (display, operators used so far), up to <see cref="Achievements.MaxSchoolOperators"/>
/// operator segments. The last segment is closed by formula, so its operand isn't limited to two keys.
/// Charges are kept per label (up to <see cref="MaxLabels"/> per state), and every full line is replayed in the simulator.
/// </summary>
public static class SchoolSearch
{
    public const int MaxLines = 100;
    public const int MaxStates = 300_000;
    public const int MaxLabels = 4;
    public const int CostSlack = 2;   // once a line costs c, states above c + CostSlack are not popped
    const int MaxMiddleOperandKeys = 2;
    const int MaxDisplayLength = 8;   // % gives up to 2^24
    const long MaxOperand = 99_999;   // five operand characters

    /// <summary>Operators is the number of op-operand-= segments.</summary>
    public sealed record Line(IReadOnlyList<Press> Presses, int Cost, int Operators);

    /// <summary>Error is set when the buttons can't do it at all (a missing operator, no operand keys).</summary>
    public sealed record Result(string? Error, IReadOnlyList<Line> Lines);

    sealed class Node(string display, int mask, int segs, int cost, int[] uses, Node? parent, Press[] via)
    {
        public readonly string Display = display;
        public readonly int Mask = mask, Segs = segs, Cost = cost;
        public readonly int[] Uses = uses;
        public readonly Node? Parent = parent;
        public readonly Press[] Via = via;
        public bool Dead;

        public List<Press> Path()
        {
            var parts = new List<Press[]>();
            for (var n = this; n != null; n = n.Parent)
                parts.Add(n.Via);
            parts.Reverse();
            return parts.SelectMany(p => p).ToList();
        }
    }

    sealed record Segment(Key Op, Press[] Presses, long Value, (int Index, int Count)[] Delta, int Cost, int Bit);

    static readonly Key[] SegmentOps = [Key.Add, Key.Subtract, Key.Multiply, Key.Divide, Key.Mod, Key.Percent];

    /// <summary>The four operators missing from the buttons, as tokens ("÷"), in + - × ÷ order.</summary>
    public static IReadOnlyList<string> Missing(Buttons buttons) =>
        new[] { Key.Add, Key.Subtract, Key.Multiply, Key.Divide }.Where(k => !buttons.Has(k)).Select(k => new Press(k).ToString()).ToList();

    public static Result Find(long start, long goal, Buttons buttons, int maxCost,
                              CancellationToken cancel = default, Action<Line>? onLine = null,
                              Action? onRejected = null, int maxLines = MaxLines)
    {
        cancel.ThrowIfCancellationRequested();
        var missing = Missing(buttons);
        if (missing.Count > 0)
            return new Result($"School mathematics needs + - × ÷ (missing: {string.Join(", ", missing)}).", []);
        if (!buttons.HasOperandKeys)
            return new Result("School mathematics needs 10 or a digit key.", []);
        if (start < 0 || goal < 0)
            return new Result(null, []);

        // Charge tokens get an index, so a label's uses are a small int array.
        var tokens = new Dictionary<string, int>();
        int Token(string t)
        {
            if (!tokens.TryGetValue(t, out var i))
                tokens[t] = i = tokens.Count;
            return i;
        }
        var leftActions = LeftSearch.Actions(buttons, backward: false);
        foreach (var a in leftActions)
            Token(a.Press.ChargeKey);
        foreach (var op in SegmentOps)
            Token(new Press(op).ChargeKey);
        foreach (var d in buttons.DigitKeys)
            Token(d.ToString());
        Token("10");
        Token("c");
        Token("q");
        int width = tokens.Count;
        var limits = new int[width];
        foreach (var (token, index) in tokens)
            limits[index] = buttons.Charges.GetValueOrDefault(token, int.MaxValue);

        (int, int)[] DeltaOf(IEnumerable<Press> presses) =>
            presses.GroupBy(p => Token(p.ChargeKey)).Select(g => (g.Key, g.Count())).ToArray();

        // Segments: op, an operand of up to two keys, =. c and q change value as they are used, so only the closing segment may use them.
        var operands = TwoOperatorSearch.Operands(buttons, MaxMiddleOperandKeys)
            .Where(o => o.Presses.All(p => p.Key is Key.Digit or Key.Ten)).ToList();
        var segments = new List<Segment>();
        foreach (var op in SegmentOps.Where(buttons.Has))
            foreach (var operand in operands)
            {
                Press[] presses = [new Press(op), .. operand.Presses];
                segments.Add(new Segment(op, presses, operand.Value, DeltaOf(presses), presses.Length, Achievements.OpBit(op)));
            }

        string s = start.ToString(), g = goal.ToString();
        var planCache = new Dictionary<(string, string), IReadOnlyList<Press>?>();
        IReadOnlyList<Press>? Plan(string from, string to)
        {
            if (!planCache.TryGetValue((from, to), out var plan))
                planCache[(from, to)] = plan = LeftEdits.Plan(from, to, buttons);
            return plan;
        }
        var ys = ArithmeticSearch.BuildYs(g, buttons, maxCost, Plan, null);

        var lines = new List<Line>();
        var seen = new HashSet<string>();
        int bestCost = int.MaxValue;
        bool full = false;

        void TryLine(IReadOnlyList<Press> presses)
        {
            if (full || !seen.Add(string.Join(' ', presses)))
                return;
            if (EngineeringCalculator.Sent(start, presses, buttons) != goal || !Achievements.FourOps(presses))
                return;
            if (!buttons.Fits(presses))
            {
                onRejected?.Invoke();
                return;
            }
            var line = new Line(presses, EngineeringCalculator.Cost(presses), presses.Count(p => p.Key == Key.Equals));
            lines.Add(line);
            bestCost = Math.Min(bestCost, line.Cost);
            onLine?.Invoke(line);
            full = lines.Count >= maxLines;
        }

        var labels = new Dictionary<(string, int), List<Node>>();
        var queue = new PriorityQueue<Node, int>();
        var scratch = new int[width];

        // Adds a label unless an existing one is at least as good; dominated labels are marked dead.
        void Push(string display, int mask, int segs, int cost, int[] baseUses, (int Index, int Count)[] delta,
                  Node parent, Press[] via)
        {
            Array.Copy(baseUses, scratch, width);
            foreach (var (i, n) in delta)
            {
                scratch[i] += n;
                if (scratch[i] > limits[i])
                    return;
            }
            var key = (display, mask);
            if (!labels.TryGetValue(key, out var list))
                labels[key] = list = [];
            foreach (var other in list)
                if (other.Cost <= cost && other.Segs <= segs && Covers(other.Uses, scratch))
                    return;
            list.RemoveAll(o =>
            {
                bool dominated = cost <= o.Cost && segs <= o.Segs && Covers(scratch, o.Uses);
                if (dominated)
                    o.Dead = true;
                return dominated;
            });
            if (list.Count >= MaxLabels)
                return;
            var node = new Node(display, mask, segs, cost, (int[])scratch.Clone(), parent, via);
            list.Add(node);
            queue.Enqueue(node, cost);
        }

        // a is no worse than b: every button used at most as often.
        static bool Covers(int[] a, int[] b)
        {
            for (int i = 0; i < a.Length; i++)
                if (a[i] > b[i])
                    return false;
            return true;
        }

        var root = new Node(s, 0, 0, 0, new int[width], null, []);
        labels[(s, 0)] = [root];
        queue.Enqueue(root, 0);

        var typedCache = new Dictionary<long, IReadOnlyList<Press>?>();
        var divisorCache = new Dictionary<(long, long, int), (long, IReadOnlyList<Press>)?>();
        int cOnly = Token("c"), qOnly = Token("q");
        int settled = 0, popped = 0;

        while (queue.TryDequeue(out var node, out int priority) && !full)
        {
            if (node.Dead)
                continue;
            if (priority > maxCost || (bestCost != int.MaxValue && priority > bestCost + CostSlack))
                break;
            if (++popped % 1000 == 0)
                cancel.ThrowIfCancellationRequested();
            if (++settled > MaxStates)
                break;

            long left = long.Parse(node.Display);
            if (node.Mask == Achievements.AllFour && node.Display == g)
                TryLine(node.Path());

            // Closing segment by formula: the operator that completes the mask (or any, when it is complete).
            if (node.Segs < Achievements.MaxSchoolOperators)
                foreach (var op in SegmentOps.Take(4).Where(o => buttons.Has(o) && (node.Mask | Achievements.OpBit(o)) == Achievements.AllFour))
                    foreach (var (y, post) in ys)
                    {
                        int budget = maxCost - node.Cost - 1 - post.Count;
                        if (budget < 1)
                            continue;
                        IReadOnlyList<Press>? operand = null;
                        switch (op)
                        {
                            case Key.Add when y >= left:
                                operand = Typed(y - left);
                                break;
                            case Key.Subtract when left >= y:
                                operand = Typed(y > 0 ? left - y : left);
                                break;
                            case Key.Multiply when left == 0 ? y == 0 : y % left == 0:
                                operand = Typed(left == 0 ? 0 : y / left);
                                break;
                            case Key.Divide:
                                long low = y == 0 ? left + 1 : left / (y + 1) + 1;
                                long high = y == 0 ? MaxOperand : Math.Min(left / y, MaxOperand);
                                var divisorKey = (low, high, budget);
                                if (!divisorCache.TryGetValue(divisorKey, out var found))
                                    divisorCache[divisorKey] = found = DivisionShortcut.CheapestDivisor(low, high, buttons, budget, minimum: 1);
                                operand = found?.Item2;
                                break;
                        }
                        if (operand == null || operand.Count > budget)
                            continue;
                        // c and q type their value for the first press only, so an earlier press of either would change it.
                        if (operand.Any(p => p.Key is Key.Charge or Key.Quest) && (node.Uses[cOnly] > 0 || node.Uses[qOnly] > 0))
                            continue;
                        IReadOnlyList<Press> presses = [.. node.Path(), new Press(op), .. operand, new Press(Key.Equals), .. post];
                        if (EngineeringCalculator.Cost(presses) <= maxCost)
                            TryLine(presses);
                        if (full)
                            break;
                    }
            if (full)
                break;

            int pushLimit = bestCost == int.MaxValue ? maxCost : Math.Min(maxCost, bestCost + CostSlack);
            if (node.Cost + 1 > pushLimit)
                continue;

            foreach (var action in leftActions)
            {
                int token = tokens[action.Press.ChargeKey];
                if (node.Uses[token] >= limits[token])
                    continue;
                foreach (var next in action.Next(node.Display))
                    if (next.Length <= MaxDisplayLength)
                        Push(next, node.Mask, node.Segs, node.Cost + 1, node.Uses, [(token, 1)], node, [action.Press]);
            }

            foreach (var segment in segments)
            {
                int cost = node.Cost + segment.Cost;
                int mask = node.Mask | segment.Bit;
                if (cost > pushLimit || segment.Op == Key.Divide && segment.Value == 0
                    || segment.Op == Key.Mod && segment.Value == 0)
                    continue;
                // Segments still to come: one per operator not yet used.
                int needed = 4 - System.Numerics.BitOperations.PopCount((uint)mask);
                if (node.Segs + 1 + needed > Achievements.MaxSchoolOperators)
                    continue;
                long? result = EngineeringCalculator.Apply(segment.Op, left, segment.Value, out bool invalid);
                if (invalid || result == null)
                    continue;
                var display = result.Value.ToString();
                if (display.Length > MaxDisplayLength)
                    continue;
                Push(display, mask, node.Segs + 1, cost, node.Uses, segment.Delta, node,
                    [.. segment.Presses, new Press(Key.Equals)]);
            }
        }

        IReadOnlyList<Press>? Typed(long d)
        {
            if (d < 0)
                return null;
            if (!typedCache.TryGetValue(d, out var typed))
                typedCache[d] = typed = DivisionShortcut.TypeOperand(d.ToString(), buttons);
            return typed;
        }

        cancel.ThrowIfCancellationRequested();
        return new Result(null, lines.OrderBy(l => l.Cost).ThenBy(l => string.Join(' ', l.Presses), StringComparer.Ordinal).ToList());
    }
}
