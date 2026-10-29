namespace CalculatorPuzzle;

/// <summary>operator_swap: the display and the goal trade places, then the game sends. Only as a line's first press, at most once.</summary>
public static class SwapSearch
{
    public const string Token = "swap";

    /// <summary>One solve: start → goal. A flipped leg's lines get `swap` in front (cost + 1, Reaches = the puzzle's S).</summary>
    public sealed record Leg(long Start, long Goal, Buttons Buttons, RequiredPlan Plan, bool Flipped);

    /// <summary>True when swap has a charge.</summary>
    public static bool Has(Buttons buttons) => buttons.Charges.GetValueOrDefault(Token) > 0;

    /// <summary>
    /// The direct leg (unless boss rule 11 requires swap), then the flipped leg when swap has a charge, S ≠ G, the flipped start isn't
    /// already S (the 1-press line `swap` covers that) and the rule isn't 13 (the swap's send would miss and re-roll the goal).
    /// </summary>
    public static IReadOnlyList<Leg> Legs(long start, long goal, Buttons buttons, RequiredPlan plan,
                                          string? required, bool boss, int bossRule)
    {
        var legs = new List<Leg>();
        if (!(boss && required == Token))
            legs.Add(new Leg(start, goal, buttons, plan, false));
        if (Has(buttons) && start != goal && bossRule != EngineeringCalculator.RerollOnMissRule
            && FlippedStart(goal, buttons, bossRule) != start)
            legs.Add(new Leg(FlippedStart(goal, buttons, bossRule), start, Flip(buttons, start),
                required == Token ? RequiredPlan.For(null, boss) : plan, true));
        return legs;
    }

    /// <summary>F: the display after the swap. G, + 1 in rule 12 (Send), + 1 in rule 14 unless swap is glass or the blind passive's free press (SubUse runs first).</summary>
    public static long FlippedStart(long goal, Buttons buttons, int bossRule) =>
        goal + (bossRule == EngineeringCalculator.SendPlusOneRule ? 1 : 0)
             + (bossRule == GoalStepSearch.GoalPerChargeRule && !buttons.Glass.Contains(Token) && buttons.Context?.Blind != true ? 1 : 0);

    /// <summary>The buttons the flipped leg runs with: no swap, and the goal the game shows after the swap (S) for near, median and the context.
    /// A non-glass swap used the blind passive's free press, so the flipped leg runs without it.</summary>
    public static Buttons Flip(Buttons buttons, long newGoal)
    {
        var flipped = buttons.Without(Token);
        if (flipped.Context is { } context)
            flipped = flipped.WithContext(context with { Goal = newGoal, Blind = context.Blind && buttons.Glass.Contains(Token) });
        return flipped.WithGoal(newGoal);
    }

    /// <summary>True when presses[0] is swap, no other press is, the rest runs from F and sends S (a rule 14 line ends at S + its charged presses), and the whole line fits the charges.</summary>
    public static bool Check(long start, long goal, IReadOnlyList<Press> presses, Buttons buttons, int bossRule)
    {
        if (presses.Count == 0 || presses[0].Key != Key.Swap || presses.Skip(1).Any(p => p.Key == Key.Swap)
            || !Has(buttons) || !buttons.Fits(presses))
            return false;
        long first = FlippedStart(goal, buttons, bossRule);
        var rest = presses.Skip(1).ToList();
        var flipped = Flip(buttons, start);
        long target = start + (bossRule == GoalStepSearch.GoalPerChargeRule ? GoalStepSearch.Raises(rest, flipped) : 0);
        // The swap itself sent already, so a bare swap wins when the display equals the new goal.
        if (rest.Count == 0)
            return first == target;
        return EngineeringCalculator.Sent(first, rest, flipped, bossRule == EngineeringCalculator.SendPlusOneRule) == target;
    }

    /// <summary>The 1-press line `swap` for a flipped start that already equals S (null when it doesn't, or swap has no charge).</summary>
    public static ResultLine? SwapAlone(long start, long goal, Buttons buttons, string? required, int bossRule)
    {
        if (!Has(buttons) || FlippedStart(goal, buttons, bossRule) != start || start == goal
            || bossRule == EngineeringCalculator.RerollOnMissRule)
            return null;
        Press[] presses = [new Press(Key.Swap)];
        if (!Check(start, goal, presses, buttons, bossRule))
            return null;
        var line = new ArithmeticSearch.Line(null, start.ToString(), 0, start, presses, 1, 0);
        return ResultLine.From(line, buttons, required) with { Swapped = true, Reaches = start };
    }

    /// <summary>
    /// A flipped leg's line with swap in front: cost + 1, drained / spare / Missing on the whole line, Reaches = inner.Reaches ?? start.
    /// Null when the whole line doesn't fit the charges.
    /// </summary>
    public static ResultLine? Wrap(ResultLine inner, long start, Buttons buttons, string? required)
    {
        var presses = new List<Press> { new(Key.Swap) };
        presses.AddRange(inner.Presses);
        if (!buttons.Fits(presses))
            return null;
        return inner with
        {
            Presses = presses,
            Cost = inner.Cost + 1,
            Drained = buttons.Drained(presses),
            Spare = buttons.Spare(presses),
            Missing = required != null && !Buttons.Uses(presses, required),
            Required = required,
            Reaches = inner.Reaches ?? start,
            Swapped = true,
        };
    }
}
