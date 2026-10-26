namespace CalculatorPuzzle;

/// <summary>Which Steam achievement the search aims at. None is the normal solver.</summary>
public enum AchievementMode { None, School, JustOne }

/// <summary>What a finished achievement search tells the form: an input error, or the credits found (Just a one, cheapest first).</summary>
public sealed record AchievementDone(string? Error, IReadOnlyList<string> Credits);

public static class Achievements
{
    /// <summary>Max operator segments in a School mathematics line.</summary>
    public const int MaxSchoolOperators = 12;

    /// <summary>The four operators School mathematics needs, as mask bits: + 1, × 2, - 4, ÷ 8 (OperationsCounter order).</summary>
    public static int OpBit(Key op) => op switch { Key.Add => 1, Key.Multiply => 2, Key.Subtract => 4, Key.Divide => 8, _ => 0 };

    public const int AllFour = 15;

    /// <summary>True when every one of + - × ÷ is applied by an = in the line (the game sets the flag on the = that applies it).</summary>
    public static bool FourOps(IReadOnlyList<Press> presses)
    {
        int mask = 0;
        Key? pending = null;
        bool typed = false;
        foreach (var press in presses)
        {
            switch (press.Key)
            {
                case Key.Divide or Key.Mod or Key.Add or Key.Subtract or Key.Multiply or Key.Percent:
                    pending = press.Key;
                    typed = false;
                    break;
                case Key.RemorphA or Key.RemorphB:
                    // One key and no =: the operation ends at once and sets no flag.
                    pending = null;
                    break;
                case Key.Ten or Key.Digit or Key.Charge or Key.Quest:
                    if (pending != null)
                        typed = true;
                    break;
                case Key.Equals:
                    if (pending != null && typed)
                        mask |= OpBit(pending.Value);
                    pending = null;
                    break;
            }
        }
        return mask == AllFour;
    }

    // operators_used ids (Infos.id, from the scene files): digit keys 1-9 = 0, + 1, - 2, × 3, ÷ 4, mod 13, q 21, c 32, % 34.
    // 0 and 10 have no Infos, so pressing them leaves LastButId unchanged.

    /// <summary>
    /// The operators_used entry a line's last = sets (game LastButId): the last press before that = whose game button has an id
    /// (`digit` for the keys 1-9, `c`, `q`, `+`, `-`, `×`, `÷`, `mod`, `%`). Null when there is none (no = at all).
    /// </summary>
    public static string? Credit(IReadOnlyList<Press> presses)
    {
        int last = -1;
        for (int i = presses.Count - 1; i >= 0; i--)
            if (presses[i].Key == Key.Equals)
            {
                last = i;
                break;
            }
        for (int i = last - 1; i >= 0 && last >= 0; i--)
        {
            var press = presses[i];
            switch (press.Key)
            {
                case Key.Digit when press.Digit != 0:
                    return "digit";
                case Key.Charge:
                    return "c";
                case Key.Quest:
                    return "q";
                case Key.Divide or Key.Mod or Key.Add or Key.Subtract or Key.Multiply or Key.Percent:
                    return press.ToString();
            }
        }
        return null;
    }
}
