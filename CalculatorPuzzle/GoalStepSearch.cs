namespace CalculatorPuzzle;

/// <summary>
/// Boss rule 14: each charged press raises the goal by 1, so a line with k charged presses must end at goal + k.
/// Tries k = 1, 2, … and keeps only lines with exactly k charged presses. Each k searches with near and median bound to goal + k
    /// (<see cref="Buttons.WithGoal"/>); the simulator checks every line with the goal at each press (goal + the charged presses so far).
/// </summary>
public static class GoalStepSearch
{
    public const int GoalPerChargeRule = 14;

    /// <summary>Lines found per kind (TwoOperator counts the two-operator phase), the last k searched, and the division input error if any.</summary>
    public sealed record Result(int Arithmetic, int Division, int Modulo, int LastStep, string? DivisionError, int TwoOperator = 0);

    /// <summary>Presses that spend a charge: every press except = and presses of a glass button.</summary>
    public static int Charged(IReadOnlyList<Press> presses, Buttons buttons) =>
        presses.Count(p => p.Key != Key.Equals && !buttons.Glass.Contains(p.ChargeKey));

    /// <summary>
    /// Searches k = 1..maxSteps and stops after the k where at least `wanted` lines are kept. Throws
    /// <see cref="OperationCanceledException"/> when the token is cancelled. onLine and onRejected can be called from worker threads.
    /// </summary>
    public static Result Search(long start, long goal, Buttons buttons,
                                RequiredPlan plan, string? required, Action<ResultLine> onLine,
                                CancellationToken cancel, int wanted, int maxSteps,
                                Action? onRejected = null, bool near1 = false)
    {
        int arithmetic = 0, twoOperator = 0, division = 0, modulo = 0, lastStep = 0;
        string? divisionError = null;
        bool divisionOn = plan.Division, moduloOn = plan.Modulo;

        var baseButtons = buttons;
        for (int k = 1; k <= maxSteps; k++)
        {
            cancel.ThrowIfCancellationRequested();
            lastStep = k;
            buttons = baseButtons.WithGoal(goal + k);
            // With the near-one passive a line may end at G + k − 1, G + k or G + k + 1.
            foreach (long target in NearGoals.Targets(goal + k, near1, plusOne: false))
            {
                if (target == start)
                    continue;
                // Each glass button can add one uncharged press.
                int cap = k + buttons.Glass.Count;
                Func<IReadOnlyList<Press>, bool> accept = p => Charged(p, buttons) == k && (plan.Accept?.Invoke(p) ?? true);

                if (plan.Arithmetic)
                    foreach (var line in ArithmeticSearch.Find(start, target, buttons, cap, cancel, accept: accept, onlyOps: plan.OnlyOps))
                    {
                        onLine(ResultLine.From(line, buttons, required) with { Reaches = target });
                        arithmetic++;
                    }

                if (plan.TwoOperator)
                    foreach (var line in TwoOperatorSearch.Find(start, target, buttons, cap, cancel, accept: accept,
                                 requiredOp: plan.RequiredOp, onRejected: onRejected))
                    {
                        onLine(ResultLine.From(line, buttons, required) with { Reaches = target });
                        twoOperator++;
                    }

                if (divisionOn)
                {
                    int found = 0;
                    var error = DivisionShortcut.Search(start, target, buttons, cap,
                        l =>
                        {
                            if (!accept(l.Presses))
                                return;
                            onLine(ResultLine.From(l, buttons, required) with { Reaches = target });
                            Interlocked.Increment(ref found);
                        }, cancel, onRejected: _ => onRejected?.Invoke());
                    division += found;
                    if (error != null)
                    {
                        // The input error doesn't change with k.
                        divisionError = error;
                        divisionOn = false;
                    }
                }

                if (moduloOn)
                {
                    var r = ModuloSearch.Find(start, target, buttons, cap, cancel, accept: accept);
                    foreach (var line in r.Lines)
                        onLine(ResultLine.From(line, buttons, required) with { Reaches = target });
                    modulo += r.Lines.Count;
                    if (r.Error != null)
                        moduloOn = false;
                }
            }
            if (arithmetic + twoOperator + division + modulo >= wanted)
                break;
        }
        return new Result(arithmetic, division, modulo, lastStep, divisionError, twoOperator);
    }
}
