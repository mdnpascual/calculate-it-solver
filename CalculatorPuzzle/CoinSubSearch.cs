namespace CalculatorPuzzle;

/// <summary>
/// The coinsub loop: when no line is found, press coinsub once more (−1 coin, +1 display; −2 coins in boss rule 10) and search again
/// from the new state. The presses are outside the 12-press cap, they only come first in a line.
/// </summary>
public static class CoinSubSearch
{
    public const string Token = "coinsub";

    /// <summary>The state after N coinsub presses from S: the display; the goal the game shows (G plus the presses that raised it, rule 14);
    /// the buttons the rest of the line runs with: no coinsub, context with the coins left, the goal shown, the blind press if still free
    /// and ChargedBefore = N.</summary>
    public sealed record State(int N, long Start, long Goal, Buttons Rest);

    /// <summary>Coins the next coinsub press needs to add 1: $1, + 1 in boss rule 10, + 1 when the diff_type ≥ 7 drain applies to it
    /// (chargedSoFar + 1 ≥ 3).</summary>
    public static int Needed(MoneyRules? rules, int chargedSoFar) =>
        1 + (rules?.Rule10 == true ? 1 : 0) + (rules?.DrainFromThird == true && chargedSoFar + 1 >= 3 ? 1 : 0);

    /// <summary>
    /// The state after n presses, or null when they can't be made: coinsub has fewer than n charges, or a press didn't raise the display
    /// (no coin left after its drains, or 999 999). For n = 0 it is the start state with coinsub taken out.
    /// </summary>
    public static State? After(long start, long goal, int n, Buttons buttons, int bossRule)
    {
        var context = buttons.Context ?? new LineContext(goal);
        if (n == 0)
            return new State(0, start, goal, buttons.Without(Token));
        if (buttons.Charges.GetValueOrDefault(Token) < n)
            return null;
        var presses = Enumerable.Repeat(new Press(Key.CoinSub), n).ToList();
        var end = new EngineeringCalculator.RunEnd();
        var log = EngineeringCalculator.Run(start, presses, buttons, false, end);
        if (log == null)
            return null;
        long shown = start;
        foreach (var entry in log)
        {
            if (!long.TryParse(entry, out var next) || next != shown + 1)
                return null;
            shown = next;
        }
        long newGoal = goal + (context.GoalStep ? end.GoalRaises : 0);
        var rest = buttons.Without(Token).WithMoney(end.Money)
            .WithContext(context with { Goal = newGoal, Money = end.Money, Blind = end.BlindLeft, ChargedBefore = context.ChargedBefore + n });
        return new State(n, shown, newGoal, rest);
    }

    static List<Press> Coins(int n) => Enumerable.Repeat(new Press(Key.CoinSub), n).ToList();

    /// <summary>
    /// A rest line with the n presses in front: cost + n, drained / spare / Missing on the whole line, Reaches kept. Null when the
    /// whole line doesn't fit the charges, or the simulator, run from S over the whole line, doesn't end at the line's goal.
    /// </summary>
    public static ResultLine? Wrap(ResultLine inner, State state, long start, Buttons buttons, string? required, int bossRule)
    {
        var presses = Coins(state.N);
        presses.AddRange(inner.Presses);
        if (!buttons.Fits(presses))
            return null;
        // The near-one passive's bare = sends the display as it is; the simulator has no bare = (outside rule 12), so the presses alone are checked.
        bool plusOne = bossRule == EngineeringCalculator.SendPlusOneRule;
        bool bare = !plusOne && inner.Presses is [{ Key: Key.Equals }];
        if (EngineeringCalculator.Sent(start, bare ? Coins(state.N) : presses, buttons, plusOne) != (inner.Reaches ?? state.Goal))
            return null;
        return inner with
        {
            Presses = presses,
            Cost = inner.Cost + state.N,
            Drained = buttons.Drained(presses),
            Spare = buttons.Spare(presses),
            Missing = required != null && !Buttons.Uses(presses, required),
            Required = required,
        };
    }

    /// <summary>The line that is only the n presses, when the display after them is already the goal (not in rule 12: a send adds 1 there).</summary>
    public static ResultLine? Alone(State state, long start, Buttons buttons, string? required, int bossRule = 0)
    {
        if (state.N < 1 || state.Start != state.Goal || bossRule == EngineeringCalculator.SendPlusOneRule)
            return null;
        var presses = Coins(state.N);
        if (!buttons.Fits(presses) || EngineeringCalculator.Sent(start, presses, buttons) != state.Goal)
            return null;
        var line = new ArithmeticSearch.Line(null, state.Start.ToString(), 0, state.Start, presses, state.N, 0);
        var result = ResultLine.From(line, buttons, required);
        return buttons.Context?.GoalStep == true ? result with { Reaches = state.Goal } : result;
    }
}
