namespace CalculatorPuzzle;

/// <summary>The goals a line may end at. The near-one passive (<c>passive_near1</c>) also wins a send within 1 of the goal.</summary>
public static class NearGoals
{
    public const string Near1Passive = "passive_near1";

    /// <summary>G alone, or G, G − 1, G + 1 with the near-one passive. Below 0 (below 1 with plusOne) is dropped.</summary>
    public static IReadOnlyList<long> Targets(long goal, bool near1, bool plusOne)
    {
        long low = plusOne ? 1 : 0;
        var targets = new List<long>(3);
        if (goal >= low) targets.Add(goal);
        if (near1)
        {
            if (goal - 1 >= low) targets.Add(goal - 1);
            if (goal + 1 >= low) targets.Add(goal + 1);
        }
        return targets;
    }
}
