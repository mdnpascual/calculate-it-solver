using System.Collections.Concurrent;

namespace CalculatorPuzzle;

/// <summary>
/// The Division Shortcut for the early-game Engineering Calculator.
///
/// Split G's digits as P ‖ q ‖ B. Edit S into X = A ‖ core ‖ T (core is S, or reverse(S) with
/// flip), press ÷ and D = "10" r times, then = so that floor(X / D) = q, and edit q into G.
///
///   floor(X / D) = q  exactly when  q·D &lt;= X &lt;= q·D + D - 1
///   A in [ ceil((q·D - L) / 10^m), floor((q·D + D - 1 - L) / 10^m) ]
///
/// where L is the value of core ‖ T and m is its digit count. Every line is checked by running
/// its presses through <see cref="EngineeringCalculator"/>.
/// </summary>
public static class DivisionShortcut
{
    const int MaxSuffixDigits = 2;
    const int MaxPrefixScan = 3000;
    const int MaxPrefixesPerCandidate = 50;

    public record Line(
        string Built,
        long Value,
        long Divisor,
        long Quotient,
        IReadOnlyList<Press> Presses,
        int Cost);

    public record Result(string? Error, IReadOnlyList<Line> Lines);

    /// <summary>The ranking order used by <see cref="Solve"/> and the UI.</summary>
    public static IComparer<Line> Ranking { get; } = Comparer<Line>.Create((x, y) =>
    {
        int c = x.Cost.CompareTo(y.Cost);
        if (c != 0) return c;
        c = NonFlipPresses(x).CompareTo(NonFlipPresses(y));
        if (c != 0) return c;
        c = x.Divisor.CompareTo(y.Divisor);
        if (c != 0) return c;
        return string.CompareOrdinal(string.Join(' ', x.Presses), string.Join(' ', y.Presses));
    });

    static int NonFlipPresses(Line l) => l.Presses.Count(p => p.Key != Key.Flip && p.Key != Key.Equals);

    public static Result Solve(long start, long goal, Buttons buttons, int maxPresses, int workers = 0, bool plusOne = false)
    {
        var lines = new List<Line>();
        var error = Search(start, goal, buttons, maxPresses, lines.Add, workers: workers, plusOne: plusOne);
        if (error != null)
            return new Result(error, []);
        if (plusOne ? goal < 1 || start == goal - 1 : start == goal)
            return new Result(null, []);
        if (lines.Count == 0)
            return new Result("No division line fits the available buttons.", []);
        lines.Sort(Ranking);
        return new Result(null, lines);
    }

    /// <summary>
    /// Reports each new, checked line through onLine as soon as it's found (no duplicates, any order).
    /// Lines that need more presses than a button's charges are passed to `onRejected` instead.
    /// `onLine` and `onRejected` are called under one lock, never on two threads at once. Returns an input error or null. Stops early when the token is cancelled and returns null.
    /// plusOne is boss rule 12 (every = adds 1): the quotient pieces come from goal − 1, and lines are checked with <see cref="EngineeringCalculator.Sent"/>.
    /// </summary>
    public static string? Search(long start, long goal, Buttons buttons, int maxPresses,
                                 Action<Line> onLine, CancellationToken cancel = default,
                                 int workers = 0, bool pin = true, Action<Line>? onRejected = null, bool plusOne = false)
    {
        if (start < 0 || goal < 0 || maxPresses < 1)
            return "S and G must be at least 0, and the press limit at least 1.";
        if (!buttons.Divide || !buttons.HasOperandKeys)
            return "The Division Shortcut needs ÷ and 10 or a digit key.";
        if (plusOne ? goal < 1 || start == goal - 1 : start == goal)
            return null;
        if (cancel.IsCancellationRequested)
            return null;
        try
        {
            SearchCore(start, goal, buttons, maxPresses, onLine, onRejected, cancel, workers, pin, plusOne);
        }
        catch (OperationCanceledException)
        {
        }
        return null;
    }

    static void SearchCore(long start, long goal, Buttons buttons, int maxPresses,
                           Action<Line> onLine, Action<Line>? onRejected, CancellationToken cancel, int workers, bool pin, bool plusOne)
    {
        var s = start.ToString();
        // The display just before the send.
        var g = (plusOne ? goal - 1 : goal).ToString();
        var prefixDigits = PrefixDigits(buttons);

        var cores = Cores(s, buttons);
        var quotients = Pieces(g, buttons);
        var suffixes = Suffixes();

        var planCache = new ConcurrentDictionary<(string, string), IReadOnlyList<Press>?>();
        IReadOnlyList<Press>? Plan(string from, string to) =>
            planCache.GetOrAdd((from, to), key => LeftEdits.Plan(key.Item1, key.Item2, buttons));

        var claimed = new ConcurrentDictionary<string, byte>();
        var gate = new object();

        void AddLine(IReadOnlyList<Press> pre, IReadOnlyList<Press> divide, IReadOnlyList<Press> post,
                     string built, long divisorValue, long q)
        {
            var presses = pre.Concat(divide).Concat(post).ToList();
            var cost = EngineeringCalculator.Cost(presses);
            if (cost > maxPresses)
                return;
            // Claiming before the check is safe: the check depends only on the presses.
            if (!claimed.TryAdd(string.Join(' ', presses), 0))
                return;
            if (EngineeringCalculator.Sent(start, presses, buttons, plusOne) != goal)
                return;
            var line = new Line(built, long.Parse(built), divisorValue, q, presses, cost);
            lock (gate)
            {
                cancel.ThrowIfCancellationRequested();
                if (buttons.Fits(presses))
                    onLine(line);
                else
                    onRejected?.Invoke(line);
                cancel.ThrowIfCancellationRequested();
            }
        }

        // Work items: Path B first (the heaviest), then Path A one item per (r, quotient).
        var items = new List<Action>();

        // Path B: X first, then the cheapest typeable D with floor(X / D) = q.
        if (buttons.DigitKeys.Count > 0 || buttons.ChargeStart != null || buttons.QuestDigit != null || buttons.MemoryValue != null)
        {
            var prefixes = Prefixes(buttons);
            var divisorCache = new ConcurrentDictionary<(long, long, int), (long, IReadOnlyList<Press>)?>();

            foreach (var qText in quotients)
                items.Add(() =>
                {
                    var post = Plan(qText, g);
                    if (post == null || 2 + post.Count > maxPresses)
                        return;
                    var q = long.Parse(qText);
                    // With edits after the =, that = adds 1, so the division gives q − 1.
                    long qd = q - (plusOne && post.Count > 0 ? 1 : 0);
                    if (qd < 1)
                        return;

                    foreach (var core in cores)
                    {
                        cancel.ThrowIfCancellationRequested();
                        foreach (var t in suffixes)
                            foreach (var a in prefixes)
                            {
                                var built = a + core + t;
                                if (!long.TryParse(built, out var x))
                                    continue;
                                var pre = Plan(s, built);
                                if (pre == null)
                                    continue;
                                int budget = maxPresses - pre.Count - 1 - post.Count;
                                if (budget < 1)
                                    continue;
                                long low = x / (qd + 1) + 1, high = x / qd;
                                if (low > high)
                                    continue;
                                var found = divisorCache.GetOrAdd((low, high, budget), key =>
                                    CheapestDivisor(key.Item1, key.Item2, buttons, key.Item3) is { } c
                                        ? (c.Divisor, c.Presses)
                                        : null);
                                if (found is not { } d)
                                    continue;
                                var divide = new List<Press> { new(Key.Divide) };
                                divide.AddRange(d.Item2);
                                divide.Add(new Press(Key.Equals));
                                AddLine(pre, divide, post, built, d.Item1, q);
                            }
                    }
                });
        }

        // Path A: D is "10" pressed r times. A comes from the formula.
        long divisor = 0;
        for (int r = 1; buttons.Ten && r <= Math.Min(maxPresses - 1, 3); r++)
        {
            try { divisor = checked(divisor * 100 + 10); }
            catch (OverflowException) { break; }
            int pressesOfTen = r;
            long d = divisor;
            var divide = new List<Press> { new(Key.Divide) };
            divide.AddRange(Enumerable.Repeat(new Press(Key.Ten), r));
            divide.Add(new Press(Key.Equals));

            foreach (var qText in quotients)
                items.Add(() =>
                {
                    var post = Plan(qText, g);
                    if (post == null || pressesOfTen + 1 + post.Count > maxPresses)
                        return;
                    var q = long.Parse(qText);
                    long qd = q - (plusOne && post.Count > 0 ? 1 : 0);
                    if (qd < 0)
                        return;

                    foreach (var core in cores)
                    {
                        cancel.ThrowIfCancellationRequested();
                        foreach (var t in suffixes)
                            foreach (var a in PrefixCandidates(qd, d, core + t, prefixDigits))
                            {
                                var built = a + core + t;
                                var pre = Plan(s, built);
                                if (pre != null)
                                    AddLine(pre, divide, post, built, d, q);
                            }
                    }
                });
        }

        WorkerPool.Run(items.Count, i => items[i](), cancel, workers, pin);
    }

    /// <summary>
    /// Range of prefixes A with floor((A ‖ tail) / divisor) = q, as digit strings ("" for none).
    /// Only digits in allowed are used. Shortest first.
    /// </summary>
    static IEnumerable<string> PrefixCandidates(long q, long divisor, string tail, IReadOnlySet<int> allowed)
    {
        var (low, high) = PrefixRange(q, divisor, tail);
        if (low > high)
            return [];
        if (low == 0)
            return [""];

        var found = new List<string>();
        long last = high - low >= MaxPrefixScan ? low + MaxPrefixScan - 1 : high;
        for (long a = low; a <= last; a++)
        {
            var text = a.ToString();
            if (text.All(c => allowed.Contains(c - '0')))
                found.Add(text);
        }
        return found.OrderBy(a => a.Length).ThenBy(a => a, StringComparer.Ordinal).Take(MaxPrefixesPerCandidate);
    }

    /// <summary>The [low, high] range of A from the formula, with low clamped to 0. Empty (1, 0) on overflow.</summary>
    public static (long Low, long High) PrefixRange(long q, long divisor, string tail)
    {
        try
        {
            long l = long.Parse(tail);
            long pow = checked((long)Math.Pow(10, tail.Length));
            long qd = checked(q * divisor);
            long top = checked(qd + divisor - 1 - l);
            if (top < 0)
                return (1, 0);
            long need = qd - l;
            long low = need <= 0 ? 0 : (need + pow - 1) / pow;
            return (low, top / pow);
        }
        catch (Exception e) when (e is OverflowException or FormatException)
        {
            return (1, 0);
        }
    }

    /// <summary>The charge key's value for its (used+1)-th press in one line, or null when it has none left.</summary>
    static int? ChargeValue(Buttons buttons, int used)
    {
        if (buttons.ChargeValue(used) is not { } value)
            return null;
        return used < buttons.Charges.GetValueOrDefault("c", int.MaxValue) ? value : null;
    }

    /// <summary>
    /// Fewest presses of the operand keys (10, the digit keys, the charge key, the quest key and the mw key) that spell
    /// `text`, or null. The charge key types ChargeStart, ChargeStart - ChargeStep, ... in order; the quest key
    /// types its digit once; the mw key types its stored number as often as wanted. Text starting with 0 is rejected, except "0" itself.
    /// </summary>
    public static IReadOnlyList<Press>? TypeOperand(string text, Buttons buttons)
    {
        if (text.Length == 0 || (text[0] == '0' && text != "0") || !text.All(char.IsAsciiDigit))
            return null;
        // best[i, c, q] = cheapest presses that spell text[..i] with c charge presses and q quest presses
        int maxCharge = buttons.ChargeStart is { } cs ? Math.Min((cs + buttons.ChargeStep - 1) / buttons.ChargeStep, buttons.Charges.GetValueOrDefault("c", int.MaxValue)) : 0;
        maxCharge = Math.Min(maxCharge, text.Length);
        bool quest = buttons.QuestDigit != null && buttons.Charges.GetValueOrDefault("q", 1) >= 1;
        var best = new List<Press>?[text.Length + 1, maxCharge + 1, 2];
        best[0, 0, 0] = [];
        for (int i = 0; i < text.Length; i++)
            for (int c = 0; c <= maxCharge; c++)
                for (int q = 0; q < 2; q++)
                {
                    if (best[i, c, q] is not { } here)
                        continue;
                    void Offer(int to, int c2, int q2, Press press)
                    {
                        if (best[to, c2, q2] == null || best[to, c2, q2]!.Count > here.Count + 1)
                            best[to, c2, q2] = [.. here, press];
                    }
                    // A key on an operand of MaxOperandTyping characters would restart it in the game.
                    if (i >= EngineeringCalculator.MaxOperandTyping)
                        continue;
                    int digit = text[i] - '0';
                    if (buttons.DigitKeys.Contains(digit))
                        Offer(i + 1, c, q, new Press(Key.Digit, digit));
                    if (buttons.Ten && i + 2 <= text.Length && text[i] == '1' && text[i + 1] == '0')
                        Offer(i + 2, c, q, new Press(Key.Ten));
                    if (quest && q == 0 && digit == buttons.QuestDigit)
                        Offer(i + 1, c, 1, new Press(Key.Quest, digit));
                    if (buttons.MemoryValue is { } stored && stored.ToString() is var storedText
                        && i + storedText.Length <= text.Length
                        && string.CompareOrdinal(text, i, storedText, 0, storedText.Length) == 0)
                        Offer(i + storedText.Length, c, q, new Press(Key.Memory, stored));
                    if (c < maxCharge && ChargeValue(buttons, c) is { } value
                        && string.CompareOrdinal(text, i, value.ToString(), 0, value.ToString().Length) == 0
                        && i + value.ToString().Length <= text.Length)
                        Offer(i + value.ToString().Length, c + 1, q, new Press(Key.Charge, value));
                }
        List<Press>? result = null;
        for (int c = 0; c <= maxCharge; c++)
            for (int q = 0; q < 2; q++)
                if (best[text.Length, c, q] is { } done && (result == null || done.Count < result.Count))
                    result = done;
        return result;
    }

    /// <summary>
    /// The cheapest typeable D with low &lt;= D &lt;= high and D &gt;= minimum (cost at most maxCost), or null.
    /// Ties go to the smaller D.
    /// </summary>
    public static (long Divisor, IReadOnlyList<Press> Presses)? CheapestDivisor(
        long low, long high, Buttons buttons, int maxCost, long minimum = 2)
    {
        low = Math.Max(low, minimum);
        if (low > high)
            return null;

        long bestD = 0;
        List<Press>? bestPresses = null;
        int bestCost = maxCost;
        var path = new List<Press>();
        int width = buttons.OperandWidth;
        bool quest = buttons.QuestDigit != null && buttons.Charges.GetValueOrDefault("q", 1) >= 1;

        int MinCost(int remaining) => (remaining + width - 1) / width;

        for (int length = low.ToString().Length; length <= high.ToString().Length
                                                                       && length <= EngineeringCalculator.MaxOperandTyping - 1 + width; length++)
        {
            if (MinCost(length) > bestCost)
                break;
            Dfs(0, 0, length, 0, 0);
        }
        return bestPresses == null ? null : (bestD, bestPresses);

        void Dfs(long prefix, int used, int length, int charges, int quests)
        {
            if (used == length)
            {
                if (prefix < low || prefix > high)
                    return;
                if (path.Count < bestCost || (path.Count == bestCost && (bestPresses == null || prefix < bestD)))
                {
                    bestCost = path.Count;
                    bestD = prefix;
                    bestPresses = [.. path];
                }
                return;
            }
            if (path.Count + MinCost(length - used) > bestCost || used >= EngineeringCalculator.MaxOperandTyping)
                return;

            foreach (var digit in buttons.DigitKeys.Order())
            {
                if (used == 0 && digit == 0)
                    continue;
                Step(prefix * 10 + digit, used + 1, length, new Press(Key.Digit, digit), charges, quests);
            }
            if (buttons.Ten && length - used >= 2)
                Step(prefix * 100 + 10, used + 2, length, new Press(Key.Ten), charges, quests);
            if (quest && quests == 0 && !(used == 0 && buttons.QuestDigit == 0))
                Step(prefix * 10 + buttons.QuestDigit!.Value, used + 1, length,
                     new Press(Key.Quest, buttons.QuestDigit!.Value), charges, 1);
            if (buttons.MemoryValue is { } stored && !(used == 0 && stored == 0))
            {
                int len = stored.ToString().Length;
                if (length - used >= len)
                {
                    long scaled = prefix;
                    for (int i = 0; i < len; i++)
                        scaled *= 10;
                    Step(scaled + stored, used + len, length, new Press(Key.Memory, stored), charges, quests);
                }
            }
            if (ChargeValue(buttons, charges) is { } value)
            {
                int len = value.ToString().Length;
                if (length - used >= len)
                {
                    long scaled = prefix;
                    for (int i = 0; i < len; i++)
                        scaled *= 10;
                    Step(scaled + value, used + len, length, new Press(Key.Charge, value), charges + 1, quests);
                }
            }
        }

        void Step(long prefix, int used, int length, Press press, int charges, int quests)
        {
            long scale = 1;
            for (int i = used; i < length; i++)
                scale *= 10;
            if (prefix * scale > high || (prefix + 1) * scale - 1 < low)
                return;
            path.Add(press);
            Dfs(prefix, used, length, charges, quests);
            path.RemoveAt(path.Count - 1);
        }
    }

    static IReadOnlySet<int> PrefixDigits(Buttons buttons) => buttons.Flip
        ? buttons.PrependDigits.Union(buttons.AppendDigits).ToHashSet()
        : buttons.PrependDigits;

    /// <summary>S, and reverse(S) when flip is available and it differs.</summary>
    internal static List<string> Cores(string s, Buttons buttons)
    {
        var cores = new List<string> { s };
        if (buttons.Flip && Reverse(s) != s)
            cores.Add(Reverse(s));
        return cores;
    }

    /// <summary>Every piece of G (and its reverse with flip) without a leading 0.</summary>
    internal static HashSet<string> Pieces(string g, Buttons buttons)
    {
        var pieces = new HashSet<string>();
        for (int i = 0; i < g.Length; i++)
            for (int len = 1; i + len <= g.Length; len++)
            {
                var q = g.Substring(i, len);
                pieces.Add(q);
                if (buttons.Flip)
                    pieces.Add(Reverse(q));
            }
        pieces.RemoveWhere(q => q[0] == '0' && g != "0");
        return pieces;
    }

    /// <summary>"" and every 1–2 digit string (suffixes; Plan rejects the ones the buttons can't add).</summary>
    internal static List<string> Suffixes()
    {
        var suffixes = new List<string> { "" };
        for (int len = 1; len <= MaxSuffixDigits; len++)
            for (int v = 0; v < (int)Math.Pow(10, len); v++)
                suffixes.Add(v.ToString().PadLeft(len, '0'));
        return suffixes;
    }

    /// <summary>"" and 1–2 digit prefixes from the prepend digits (and append digits with flip), no leading 0.</summary>
    internal static List<string> Prefixes(Buttons buttons)
    {
        var prefixDigits = PrefixDigits(buttons);
        var prefixes = new List<string> { "" };
        foreach (var d1 in prefixDigits.Where(d => d != 0).Order())
        {
            prefixes.Add(d1.ToString());
            foreach (var d2 in prefixDigits.Order())
                prefixes.Add($"{d1}{d2}");
        }
        return prefixes;
    }

    internal static string Reverse(string text) => new(text.Reverse().ToArray());
}
