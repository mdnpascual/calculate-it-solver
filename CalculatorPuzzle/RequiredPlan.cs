namespace CalculatorPuzzle;

/// <summary>
/// What a search runs when a boss round (rule 11) requires a button: only lines that press it count.
/// An operator button needs only the phase that uses it, plus the two-operator phase, where any operator can be one of the two.
/// Market Demand and no required button run everything.
/// </summary>
public sealed record RequiredPlan(Func<IReadOnlyList<Press>, bool>? Accept, IReadOnlyCollection<Key>? OnlyOps,
                                  bool Arithmetic, bool Division, bool Modulo,
                                  bool TwoOperator = true, Key? RequiredOp = null)
{
    // RequiredOp: the operator every two-operator pair must contain (null = no limit).

    /// <summary>The plan for a usable required token (null = none, or one the solver can't use) in boss mode or not.</summary>
    public static RequiredPlan For(string? token, bool boss)
    {
        if (!boss || token == null)
            return new RequiredPlan(null, null, true, true, true);
        var op = Buttons.OperatorKey(token);
        Func<IReadOnlyList<Press>, bool> accept = p => Buttons.Uses(p, token);
        return op switch
        {
            null => new RequiredPlan(accept, null, true, true, true),
            Key.Divide => new RequiredPlan(accept, null, false, true, false, true, Key.Divide),
            Key.Mod => new RequiredPlan(accept, null, false, false, true, true, Key.Mod),
            _ => new RequiredPlan(accept, [op.Value], true, false, false, true, op),
        };
    }

    /// <summary>Boss rule 13: keep only lines whose only = is the last press; two-operator lines always have a middle =.</summary>
    public RequiredPlan OneSend()
    {
        var before = Accept;
        return this with
        {
            Accept = before == null ? EngineeringCalculator.OneSend : p => before(p) && EngineeringCalculator.OneSend(p),
            TwoOperator = false,
        };
    }
}
