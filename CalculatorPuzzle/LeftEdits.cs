namespace CalculatorPuzzle;

/// <summary>
/// Shortest left-side edit sequences. While building toward a target W, the display is always a
/// piece W[i..j) of W, or its reverse, so a breadth-first search over (i, j, reversed) finds the
/// fewest presses. dX can't extend a piece with a leading zero (the game drops it first); flip
/// drops zeros at both ends of the piece.
/// </summary>
public static class LeftEdits
{
    record State(int I, int J, bool Reversed);

    /// <summary>
    /// Fewest left-side presses (dX, Xd, flip) that turn the display `from` into `to`, where
    /// `to` contains `from` (or, with flip, its reverse). Null if the buttons can't do it.
    /// </summary>
    public static IReadOnlyList<Press>? Plan(string from, string to, Buttons buttons)
    {
        var parent = new Dictionary<State, (State? Prev, Press? Press)>();
        var queue = new Queue<State>();

        void Seed(string piece, bool reversed)
        {
            if (piece.Length == 0)
                return;
            for (int at = to.IndexOf(piece, StringComparison.Ordinal); at >= 0;
                 at = to.IndexOf(piece, at + 1, StringComparison.Ordinal))
            {
                var state = new State(at, at + piece.Length, reversed);
                if (parent.TryAdd(state, (null, null)))
                    queue.Enqueue(state);
            }
        }

        Seed(from, false);
        if (buttons.Flip)
            Seed(new string(from.Reverse().ToArray()), true);

        var goal = new State(0, to.Length, false);
        while (queue.Count > 0)
        {
            var s = queue.Dequeue();
            if (s == goal)
                break;

            void Go(State next, Press press)
            {
                if (parent.TryAdd(next, (s, press)))
                    queue.Enqueue(next);
            }

            // The digit joining the display's left or right end, in terms of W.
            // Not reversed: the left end is W[i-1] and the right end is W[j].
            // Reversed: the left end is W[j] and the right end is W[i-1].
            int? leftDigit = s.Reversed ? Digit(s.J) : Digit(s.I - 1);
            int? rightDigit = s.Reversed ? Digit(s.I - 1) : Digit(s.J);

            // Appends are tried first, so a prepend/append pair comes out as the append first (X1 8X).
            // Xd and dX do nothing on a display of MaxDisplay characters, so only a shorter piece can grow.
            bool canGrow = s.J - s.I < EngineeringCalculator.MaxDisplay;
            if (canGrow && rightDigit is { } r && buttons.AppendDigits.Contains(r))
                Go(s.Reversed ? s with { I = s.I - 1 } : s with { J = s.J + 1 }, new Press(Key.Append, r));
            // dX drops the display's leading zeros first, so it only extends a display without one (or "0").
            bool leadingZero = s.J - s.I > 1 && (s.Reversed ? to[s.J - 1] : to[s.I]) == '0';
            if (canGrow && leftDigit is { } l && !leadingZero && buttons.PrependDigits.Contains(l))
                Go(s.Reversed ? s with { J = s.J + 1 } : s with { I = s.I - 1 }, new Press(Key.Prepend, l));
            if (buttons.Flip)
                Go(Flipped(s), new Press(Key.Flip));
        }

        if (!parent.ContainsKey(goal))
            return null;

        var presses = new List<Press>();
        for (var at = goal; parent[at].Prev is { } prev; at = prev)
            presses.Add(parent[at].Press!);
        presses.Reverse();
        return presses;

        // flip reverses the number: zeros at both ends of the piece drop (one 0 stays for an all-zero piece).
        State Flipped(State s)
        {
            int i = s.I, j = s.J;
            while (j - i > 1 && to[i] == '0') i++;
            while (j - i > 1 && to[j - 1] == '0') j--;
            return new State(i, j, !s.Reversed);
        }

        int? Digit(int index) => index >= 0 && index < to.Length ? to[index] - '0' : null;
    }
}
