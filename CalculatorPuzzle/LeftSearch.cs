namespace CalculatorPuzzle;

/// <summary>
/// Fewest left-side presses between displays, with every left-side button (dX, Xd, flip, reset
/// and the transforms). Displays are strings: Xd and digit maps keep leading zeros; dX, flip and the
/// number transforms read the number and drop them. No path uses a button
/// more often than its charges. States are keyed by display only: when the shortest route to a
/// display spends a scarce button, a longer route that saves it is not kept.
/// </summary>
public static class LeftSearch
{
    public record Limits(int MaxPresses, int MaxDigits, int MaxStates);

    /// <summary>MaxDigits = min(8, max(len S, len G) + 3) (8: % gives up to 2^24), MaxStates = 100 000.</summary>
    public static Limits DefaultLimits(string s, string g, int maxPresses) =>
        new(maxPresses, Math.Min(8, Math.Max(s.Length, g.Length) + 3), 100_000);

    /// <summary>Every display reachable from `from`, with the fewest presses to get there.</summary>
    public static Dictionary<string, IReadOnlyList<Press>> Forward(string from, Buttons buttons, Limits limits,
                                                                    CancellationToken cancel = default) =>
        Search(from, buttons, limits, false, cancel);

    /// <summary>Every display from which `to` is reachable, with the fewest presses from there to `to`.</summary>
    public static Dictionary<string, IReadOnlyList<Press>> Backward(string to, Buttons buttons, Limits limits,
                                                                     CancellationToken cancel = default) =>
        Search(to, buttons, limits, true, cancel);

    /// <summary>Up to maxLines distinct shortest-first press lists from `start` to `to`, each checked in the simulator and with Fits.</summary>
    public static IReadOnlyList<IReadOnlyList<Press>> Connect(long start, string to, Buttons buttons, Limits limits,
                                                               int maxLines, CancellationToken cancel = default)
    {
        int half = (limits.MaxPresses + 1) / 2;
        var f = Forward(start.ToString(), buttons, limits with { MaxPresses = half }, cancel);
        var b = Backward(to, buttons, limits with { MaxPresses = limits.MaxPresses - half }, cancel);
        return Join(start, to, buttons, f, b, limits.MaxPresses, maxLines);
    }

    /// <summary>
    /// Lines made of a forward path and a backward path that meet on the same display, cheapest
    /// first, at most maxCost presses, checked in the simulator and with Fits.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<Press>> Join(long start, string to, Buttons buttons,
                                                             Dictionary<string, IReadOnlyList<Press>> f,
                                                             Dictionary<string, IReadOnlyList<Press>> b,
                                                             int maxCost, int maxLines)
    {
        var candidates = new List<IReadOnlyList<Press>>();
        foreach (var (display, front) in f)
        {
            if (b.TryGetValue(display, out var back) && front.Count + back.Count <= maxCost)
                candidates.Add([.. front, .. back]);
            // Backward lists plain numbers only, so a display with leading zeros also meets the entry
            // for its number; the simulator check below drops the joins where the next press reads the text.
            if (display.Length > 1 && display[0] == '0'
                && b.TryGetValue(display.TrimStart('0') is { Length: > 0 } plain ? plain : "0", out var byValue)
                && byValue.Count > 0 && front.Count + byValue.Count <= maxCost)
                candidates.Add([.. front, .. byValue]);
        }
        if (LeftEdits.Plan(start.ToString(), to, buttons) is { } plan && plan.Count <= maxCost)
            candidates.Add(plan);

        var seen = new HashSet<string>();
        var lines = new List<IReadOnlyList<Press>>();
        foreach (var (presses, _) in candidates.Select(c => (c, string.Join(' ', c)))
                     .OrderBy(c => c.Item1.Count).ThenBy(c => c.Item2, StringComparer.Ordinal))
        {
            if (presses.Count == 0 || !seen.Add(string.Join(' ', presses)))
                continue;
            var log = EngineeringCalculator.Run(start, presses, buttons);
            if (log == null || log[^1] != to || !buttons.Fits(presses))
                continue;
            lines.Add(presses);
            if (lines.Count >= maxLines)
                break;
        }
        return lines;
    }

    /// <summary>One left-side button: its press, charge limit and how it moves a display.</summary>
    internal sealed record Action(Press Press, int Charges, Func<string, IEnumerable<string>> Next, bool Ascending = false);

    /// <summary>
    /// Every display that flip turns into w, as far as the backward search lists them. flip's result is a
    /// number that doesn't end in 0 (except "0"), so w must be plain and not end in 0. Only reverse(w) is
    /// listed: reverse(w) followed by 0s also flips to w, but listing those fills the state limit (Old
    /// Calculator benchmark 0.4 s → 16 s), so flips that drop zeros are found by the forward half only.
    /// Like the number transforms, no leading-zero displays are listed; Join matches those by value.
    /// </summary>
    static IEnumerable<string> FlipPreimages(string w)
    {
        if (!IsPlain(w) || (w != "0" && w[^1] == '0'))
            yield break;
        yield return w == "0" ? "0" : new string(w.Reverse().ToArray());
    }

    /// <summary>No leading zero ("0" itself is plain).</summary>
    static bool IsPlain(string w) => w.Length == 1 || (w.Length > 1 && w[0] != '0');

    internal static List<Action> Actions(Buttons buttons, bool backward)
    {
        var actions = new List<Action>();
        void Add(Press press, Func<string, IEnumerable<string>> next, bool ascending = false) =>
            actions.Add(new Action(press, buttons.Charges.GetValueOrDefault(press.ToString(), int.MaxValue), next, ascending));

        foreach (var d in buttons.AppendDigits.Order())
        {
            var c = (char)('0' + d);
            if (backward)
                Add(new Press(Key.Append, d), w => w.Length >= 2 && w.Length <= EngineeringCalculator.MaxDisplay && w[^1] == c ? [w[..^1]] : []);
            else
                Add(new Press(Key.Append, d), w => w.Length < EngineeringCalculator.MaxDisplay ? [w + c] : []);
        }
        foreach (var d in buttons.PrependDigits.Order())
        {
            var c = (char)('0' + d);
            if (backward)
                Add(new Press(Key.Prepend, d), w => w.Length >= 2 && w.Length <= EngineeringCalculator.MaxDisplay && w[0] == c && IsPlain(w[1..]) ? [w[1..]] : []);
            else
                Add(new Press(Key.Prepend, d), w => w.Length < EngineeringCalculator.MaxDisplay ? [EngineeringCalculator.Prepend(w, d)] : []);
        }
        if (buttons.Flip)
            Add(new Press(Key.Flip), backward ? FlipPreimages : w => [EngineeringCalculator.Flip(w)]);
        if (buttons.Reset && !backward)
            Add(new Press(Key.Reset), _ => ["0"]);
        foreach (var t in buttons.Transforms)
        {
            if (backward)
                Add(Press.Of(t), t.Preimages, t.Ascending);
            else
                Add(Press.Of(t), w => t.Apply(w) is { } next ? [next] : []);
        }
        return actions;
    }

    static Dictionary<string, IReadOnlyList<Press>> Search(string origin, Buttons buttons, Limits limits,
                                                            bool backward, CancellationToken cancel)
    {
        var actions = Actions(buttons, backward);
        var paths = new Dictionary<string, Press[]> { [origin] = [] };
        var queue = new Queue<string>();
        queue.Enqueue(origin);
        int dequeued = 0;

        while (queue.Count > 0 && paths.Count < limits.MaxStates)
        {
            if (++dequeued % 4096 == 0)
                cancel.ThrowIfCancellationRequested();
            var w = queue.Dequeue();
            var path = paths[w];
            if (path.Length >= limits.MaxPresses)
                continue;

            foreach (var action in actions)
            {
                // The button can still be pressed: fewer presses so far than its charges.
                if (action.Charges <= path.Length && path.Count(p => p == action.Press) >= action.Charges)
                    continue;
                foreach (var next in action.Next(w))
                {
                    if (next.Length > limits.MaxDigits)
                    {
                        if (action.Ascending)
                            break;
                        continue;
                    }
                    if (paths.ContainsKey(next))
                        continue;
                    paths[next] = backward ? [action.Press, .. path] : [.. path, action.Press];
                    queue.Enqueue(next);
                    if (paths.Count >= limits.MaxStates)
                        break;
                }
                if (paths.Count >= limits.MaxStates)
                    break;
            }
        }
        return paths.ToDictionary(p => p.Key, p => (IReadOnlyList<Press>)p.Value);
    }
}
