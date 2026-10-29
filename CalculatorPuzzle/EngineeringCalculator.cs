using System.Text.RegularExpressions;

namespace CalculatorPuzzle;

public enum Key { Prepend, Append, Ten, Digit, Charge, Quest, Reset, Flip, Transform, Divide, Mod, Add, Subtract, Multiply,
                  Percent, RemorphA, RemorphB, AllDigits, Complement, Memory, Swap, CoinSub, Equals }

/// <summary>One button press. Digit is used by Prepend, Append, Digit and Quest; for Charge and Memory it is the value typed (it can be 10 or more).</summary>
public record Press(Key Key, int Digit = -1, LeftTransform? Transform = null)
{
    public static Press Of(LeftTransform t) => new(Key.Transform, Transform: t);

    public override string ToString() => Key switch
    {
        Key.Prepend => $"{Digit}X",
        Key.Append => $"X{Digit}",
        Key.Ten => "10",
        Key.Digit => Digit.ToString(),
        Key.Charge => $"c{Digit}",
        Key.Quest => $"q{Digit}",
        Key.Reset => "reset",
        Key.Flip => "flip",
        Key.Transform => Transform!.Token,
        Key.Divide => "÷",
        Key.Mod => "mod",
        Key.Add => "+",
        Key.Subtract => "-",
        Key.Multiply => "×",
        Key.Percent => "%",
        Key.RemorphA => "a->c",
        Key.RemorphB => "b->c",
        Key.AllDigits => "==",
        Key.Complement => "AX",
        Key.Memory => $"m{Digit}",
        Key.Swap => "swap",
        Key.CoinSub => CoinSubSearch.Token,
        _ => "=",
    };

    /// <summary>The key into <see cref="Buttons.Charges"/>: every charge press counts under "c", every quest press under "q", every mw press under "m".</summary>
    public string ChargeKey => Key switch { Key.Charge => "c", Key.Quest => "q", Key.Memory => "m", _ => ToString() };

    /// <summary>A key that types on the right side only (after an operator).</summary>
    public static bool IsOperandKey(Key key) => key is Key.Ten or Key.Digit or Key.Charge or Key.Quest or Key.Memory;
}

/// <summary>The early-game Engineering Calculator buttons a puzzle offers. "=" is always available.</summary>
public record Buttons(IReadOnlySet<int> PrependDigits, IReadOnlySet<int> AppendDigits,
                      IReadOnlySet<int> DigitKeys,
                      bool Ten, bool Reset, bool Divide, bool Mod, bool Flip,
                      IReadOnlyDictionary<string, int>? ChargeMap = null,
                      bool Add = false, bool Subtract = false, bool Multiply = false,
                      IReadOnlyCollection<LeftTransform>? TransformList = null,
                      int? ChargeStart = null, int? QuestDigit = null,
                      bool Percent = false, bool RemorphA = false, bool RemorphB = false,
                      bool LastIgnored = false,
                      IReadOnlySet<string>? GlassKeys = null,
                      int ChargeStep = 1,
                      bool AllDigits = false, bool Complement = false, int? MemoryValue = null,
                      bool GoalIgnored = false, bool MoneyIgnored = false)
{
    // ChargeStart: the charge key's value now (null = no charge key). QuestDigit: the quest key's digit (null = none).
    // LastIgnored: `last` had charges but no Last value was given. GoalIgnored / MoneyIgnored: the same for near and median (no goal), money (no amount).
    // AllDigits: the `==` button. Complement: the `AX` button. MemoryValue: the number the mw button types (null = none).

    /// <summary>The game state the simulator runs lines in (goal and money at each press). Null = transforms run as bound.</summary>
    public LineContext? Context { get; init; }

    /// <summary>A copy with this button's charges set to 0 (the swap search solves the flipped leg without `swap`).</summary>
    public Buttons Without(string token) =>
        Copy(Charges.ToDictionary(c => c.Key, c => c.Key == token ? 0 : c.Value));

    /// <summary>A copy with the money transform bound to another amount (the coinsub loop searches on with the coins left).</summary>
    public Buttons WithMoney(long money) =>
        Copy(transforms: Transforms.Select(t => t.Reads == Reads.Money ? t.Bind!(money) : t).ToList());

    /// <summary>A copy that runs lines in this context.</summary>
    public Buttons WithContext(LineContext? context) => Copy() with { Context = context };

    /// <summary>A copy with near and median bound to another goal (boss rule 14 searches goal + k).</summary>
    public Buttons WithGoal(long goal) =>
        Copy(transforms: Transforms.Select(t => t.Reads == Reads.Goal ? t.Bind!(goal) : t).ToList());

    Buttons Copy(IReadOnlyDictionary<string, int>? charges = null, IReadOnlyCollection<LeftTransform>? transforms = null,
                 int? chargeStep = null) =>
        new(PrependDigits, AppendDigits, DigitKeys, Ten, Reset, Divide, Mod, Flip, charges ?? Charges,
            Add, Subtract, Multiply, transforms ?? Transforms, ChargeStart, QuestDigit, Percent, RemorphA, RemorphB,
            LastIgnored, Glass, chargeStep ?? ChargeStep, AllDigits, Complement, MemoryValue, GoalIgnored, MoneyIgnored)
        { Context = Context };

    /// <summary>The charge key's value on its (used+1)-th press in one line, or null when it would be below 1.</summary>
    public int? ChargeValue(int used) =>
        ChargeStart is { } start && start - ChargeStep * used >= 1 ? start - ChargeStep * used : null;

    /// <summary>
    /// Boss rule 6: each press costs 2 charges (down to 0), so n charges give ceil(n / 2) presses.
    /// Glass buttons keep their one press. The charge key types start, start - 2, ...
    /// Call once on freshly parsed buttons: a second call would halve again.
    /// </summary>
    public Buttons TakeTwo() =>
        Copy(Charges.ToDictionary(c => c.Key, c => Glass.Contains(c.Key) ? c.Value : (c.Value + 1) / 2), chargeStep: 2);

    /// <summary>True when something can type an operand: 10, a digit key, the charge key, the quest key or the mw key.</summary>
    public bool HasOperandKeys => Ten || DigitKeys.Count > 0 || ChargeStart != null || QuestDigit != null || MemoryValue != null;

    /// <summary>The most digits one operand-key press can type.</summary>
    public int OperandWidth => Math.Max(Math.Max(Ten ? 2 : 1, ChargeStart is { } c ? c.ToString().Length : 1),
                                        MemoryValue is { } m ? m.ToString().Length : 1);

    /// <summary>Charge keys of glass buttons (written "X8:1g"): they break after one press and don't raise the goal in boss rule 14.</summary>
    public IReadOnlySet<string> Glass { get; } = GlassKeys ?? new HashSet<string>();

    /// <summary>The left-side transform buttons, in the order given.</summary>
    public IReadOnlyList<LeftTransform> Transforms { get; } = TransformList?.ToList() ?? [];

    /// <summary>Presses left per button, keyed like <see cref="Press.ChargeKey"/> ("X1", "4X", "10", "÷", "c", "q", ...). A missing key means unlimited.</summary>
    public IReadOnlyDictionary<string, int> Charges { get; } = ChargeMap ?? new Dictionary<string, int>();

    /// <summary>True when the buttons include this operator key (÷, mod, +, -, ×).</summary>
    public bool Has(Key op) => op switch
    {
        Key.Divide => Divide,
        Key.Mod => Mod,
        Key.Add => Add,
        Key.Subtract => Subtract,
        Key.Multiply => Multiply,
        Key.Percent => Percent,
        Key.RemorphA => RemorphA,
        Key.RemorphB => RemorphB,
        Key.AllDigits => AllDigits,
        Key.Complement => Complement,
        Key.CoinSub => Charges.GetValueOrDefault(CoinSubSearch.Token) > 0,
        _ => false,
    };

    /// <summary>True when no button is pressed more often than its charges allow. = is never counted.</summary>
    public bool Fits(IReadOnlyList<Press> presses)
    {
        if (Charges.Count == 0)
            return true;
        var used = new Dictionary<string, int>();
        foreach (var press in presses)
        {
            if (press.Key == Key.Equals)
                continue;
            var token = press.ChargeKey;
            used[token] = used.GetValueOrDefault(token) + 1;
        }
        return used.All(u => !Charges.TryGetValue(u.Key, out var limit) || u.Value <= limit);
    }

    /// <summary>Sum of the charges of the distinct buttons pressed (= not counted). A button with no charge entry adds 0.</summary>
    public int Spare(IReadOnlyList<Press> presses) =>
        presses.Where(p => p.Key != Key.Equals).Select(p => p.ChargeKey).Distinct()
               .Sum(token => Charges.GetValueOrDefault(token));

    /// <summary>Distinct buttons pressed as often as their charges (left at 0). Buttons without a charge entry never count.
    /// With the blind passive (Context.Blind) the line's first non-glass press uses no charge.</summary>
    public int Drained(IReadOnlyList<Press> presses)
    {
        string? free = Context?.Blind == true
            ? presses.FirstOrDefault(p => p.Key != Key.Equals && !Glass.Contains(p.ChargeKey))?.ChargeKey
            : null;
        return presses.Where(p => p.Key != Key.Equals).GroupBy(p => p.ChargeKey)
                      .Count(g => Charges.TryGetValue(g.Key, out var n) && g.Count() - (g.Key == free ? 1 : 0) >= n);
    }

    /// <summary>The canonical token for one button ("/" → "÷", "x1" → "X1"), or null when it isn't a button.</summary>
    public static string? Token(string text)
    {
        if (Regex.Replace(text, @"\s+", "").Length == 0 || text.Contains(',') || text.Contains(':'))
            return null;
        // Bare words whose Parse needs a digit or a Last value.
        var bare = ButtonLabels.Token(Regex.Replace(text, @"\s+", "")).ToLowerInvariant();
        if (bare is "c" or "q" or "last" or "near" or "median" or "money" or "m")
            return bare;
        var (buttons, error) = Parse(bare + ":1");
        return error == null && buttons!.Charges.Count == 1 ? buttons.Charges.Keys.Single() : null;
    }

    /// <summary>The operator key for a token (÷ mod + - × % a->c b->c == AX), or null for every other button.</summary>
    public static Key? OperatorKey(string token) => token switch
    {
        "÷" => Key.Divide,
        "mod" => Key.Mod,
        "+" => Key.Add,
        "-" => Key.Subtract,
        "×" => Key.Multiply,
        "%" => Key.Percent,
        "a->c" => Key.RemorphA,
        "b->c" => Key.RemorphB,
        "==" => Key.AllDigits,
        "AX" => Key.Complement,
        _ => null,
    };

    /// <summary>True when the presses include the button with this token (= is never a button press).</summary>
    public static bool Uses(IReadOnlyList<Press> presses, string token) =>
        presses.Any(p => p.Key != Key.Equals && p.ChargeKey == token);

    static readonly int[] DigitRange =[0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    public static Buttons All { get; } = new(
        new HashSet<int>(DigitRange), new HashSet<int>(DigitRange), new HashSet<int>(DigitRange), true, true, true, true, true, Add: true, Subtract: true, Multiply: true);

    /// <summary>
    /// Parses a comma-separated list such as "4X, 8X, X4, 10, ÷". Case and spaces are ignored,
    /// "/" means ÷, "*" and "x" mean ×, "−" means -, and "=" is accepted and ignored. Every other token must end in ":n" (presses left,
    /// a whole number >= 0, or "ng" for a glass button, clamped to 1); 0 leaves the button out. Returns an error naming the first bad token.
    /// The charge key is "c:n" (value n, n presses) or "cV:n" (value V now, n presses); the quest key is "qD:n" (digit D now).
    /// "last" needs the Last value; without it the button is left out and LastIgnored is set. "near" and "median" need the goal
    /// (GoalIgnored) and "money" needs the money amount (MoneyIgnored) the same way. "==" and "AX" are buttons of their own; the mw
    /// key is "mD:n" (D = the number it types, one mw button at most).
    /// </summary>
    public static (Buttons? Buttons, string? Error) Parse(string text, long? last = null, long? goal = null, long? money = null)
    {
        var prepend = new HashSet<int>();
        var append = new HashSet<int>();
        var digitKeys = new HashSet<int>();
        bool ten = false, reset = false, divide = false, mod = false, flip = false;
        bool add = false, subtract = false, multiply = false;
        var charges = new Dictionary<string, int>();
        var transforms = new List<LeftTransform>();
        int? chargeStart = null, questDigit = null;
        bool percent = false, remorphA = false, remorphB = false, lastIgnored = false;
        bool allDigits = false, complement = false, goalIgnored = false, moneyIgnored = false;
        int? memoryValue = null;
        var glass = new HashSet<string>();

        foreach (var raw in text.Split(','))
        {
            var token = Regex.Replace(raw, @"\s+", "").ToLowerInvariant();
            if (token.Length == 0)
                continue;
            int? limit = null;
            bool isGlass = false;
            if (token.Contains(':'))
            {
                var parts = token.Split(':');
                if (parts.Length != 2 || !Regex.IsMatch(parts[1], @"^\d{1,9}g?$"))
                    return (null, $"Bad charge count in \"{raw.Trim()}\"");
                token = parts[0];
                isGlass = parts[1].EndsWith('g');
                limit = int.Parse(parts[1].TrimEnd('g'));
                // Glass breaks after one press.
                if (isGlass) limit = Math.Min(limit.Value, 1);
            }
            token = ButtonLabels.Token(token).Replace('−', '-');
            token = token switch { "−" => "-", "*" or "x" => "×", _ => token };
            if (Regex.Match(token, @"^(\d)x$") is { Success: true } pre)
                prepend.Add(pre.Groups[1].Value[0] - '0');
            else if (Regex.Match(token, @"^x(\d)$") is { Success: true } app)
                append.Add(app.Groups[1].Value[0] - '0');
            else if (Regex.IsMatch(token, @"^\d$")) digitKeys.Add(token[0] - '0');
            else if (token == "10") ten = true;
            else if (token == "reset") reset = true;
            else if (token is "÷" or "/") divide = true;
            else if (token == "mod") mod = true;
            else if (token == "flip") flip = true;
            else if (token == "+") add = true;
            else if (token == "-") subtract = true;
            else if (token == "×") multiply = true;
            else if (token == "=") { }
            else if (token == "%") percent = true;
            else if (token == "a->c") remorphA = true;
            else if (token == "b->c") remorphB = true;
            else if (token == "==") allDigits = true;
            else if (token == "ax") complement = true;
            else if (token == SwapSearch.Token || token == CoinSubSearch.Token) { }
            else if (token is "near" or "median" or "money")
            {
                if (BoundTransform(token, goal, money) is { } bound && !transforms.Contains(bound))
                    transforms.Add(bound);
            }
            else if (Regex.Match(token, @"^m(\d+)$") is { Success: true } memory)
            {
                var digits = memory.Groups[1].Value;
                if (digits.Length > 6 || (digits.Length > 1 && digits[0] == '0'))
                    return (null, $"mem key \"{raw.Trim()}\" needs a number from 0 to 999999 with no leading zero");
                if (limit is > 0)
                {
                    if (memoryValue != null)
                        return (null, "Only one mem button (memD) is allowed");
                    memoryValue = int.Parse(digits);
                }
            }
            else if (token == "last")
            {
                if (last is { } lastValue)
                {
                    var lastTransform = LeftTransforms.Last(lastValue);
                    if (!transforms.Contains(lastTransform))
                        transforms.Add(lastTransform);
                }
            }
            else if (Regex.Match(token, @"^c(\d{1,9})?$") is { Success: true } charge)
            {
                // "c:n" starts at value n; "cV:n" starts at value V.
                if (charge.Groups[1].Success && int.Parse(charge.Groups[1].Value) == 0)
                    return (null, $"Volt key \"{raw.Trim()}\" needs a value of at least 1");
                if (limit is > 0 and var n0)
                {
                    int value = charge.Groups[1].Success ? int.Parse(charge.Groups[1].Value) : n0;
                    if (n0 > value)
                        return (null, $"Volt key volt{value} can't have more than {value} presses");
                    chargeStart = value;
                }
            }
            else if (token == "q")
                return (null, "Unknown key needs its digit (write unkD:n)");
            else if (Regex.Match(token, @"^q(\d)$") is { Success: true } quest)
            {
                if (limit is > 0)
                    questDigit = quest.Groups[1].Value[0] - '0';
            }
            else if (LeftTransforms.Parse(token) is { } transform)
            {
                if (!transforms.Contains(transform))
                    transforms.Add(transform);
            }
            else return (null, $"Unknown button \"{raw.Trim()}\"");

            if (token == "=")
                continue;
            if (limit is not { } n)
                return (null, $"Missing charge count in \"{raw.Trim()}\" (write {raw.Trim()}:n)");
            var key = Regex.IsMatch(token, @"^(\dx|x\d)$") ? token.ToUpperInvariant()
                : Regex.IsMatch(token, @"^c(\d{1,9})?$") ? "c"
                : Regex.IsMatch(token, @"^q\d$") ? "q"
                : Regex.IsMatch(token, @"^m\d+$") ? "m"
                : token == "ax" ? "AX"
                : token == "/" ? "÷"
                : LeftTransforms.Parse(token) is { } named ? named.Token : token;
            if (n > 0)
            {
                if (token == "last" && last == null)
                {
                    lastIgnored = true;
                    continue;
                }
                if (token is "near" or "median" && goal == null)
                {
                    goalIgnored = true;
                    continue;
                }
                if (token == "money" && money == null)
                {
                    moneyIgnored = true;
                    continue;
                }
                charges[key] = n;
                if (isGlass) glass.Add(key);
                continue;
            }
            // 0 charges: take the button back out of the sets.
            if (Regex.Match(token, @"^(\d)x$") is { Success: true } p0) prepend.Remove(p0.Groups[1].Value[0] - '0');
            else if (Regex.Match(token, @"^x(\d)$") is { Success: true } a0) append.Remove(a0.Groups[1].Value[0] - '0');
            else if (Regex.IsMatch(token, @"^\d$")) digitKeys.Remove(token[0] - '0');
            else if (token == "10") ten = false;
            else if (token == "reset") reset = false;
            else if (token is "÷" or "/") divide = false;
            else if (token == "mod") mod = false;
            else if (token == "flip") flip = false;
            else if (token == "+") add = false;
            else if (token == "-") subtract = false;
            else if (token == "×") multiply = false;
            else if (token == "%") percent = false;
            else if (token == "a->c") remorphA = false;
            else if (token == "b->c") remorphB = false;
            else if (token == "==") allDigits = false;
            else if (token == "ax") complement = false;
            else if (token is "near" or "median" or "money") transforms.RemoveAll(t => t.Token == token);
            else if (Regex.IsMatch(token, @"^m\d+$")) memoryValue = null;
            else if (token == "last") { if (last is { } l0) transforms.Remove(LeftTransforms.Last(l0)); }
            else if (Regex.IsMatch(token, @"^c(\d{1,9})?$")) chargeStart = null;
            else if (Regex.IsMatch(token, @"^q\d$")) questDigit = null;
            else if (LeftTransforms.Parse(token) is { } removed) transforms.Remove(removed);
        }

        return (new Buttons(prepend, append, digitKeys, ten, reset, divide, mod, flip, charges, add, subtract, multiply, transforms,
            chargeStart, questDigit, percent, remorphA, remorphB, lastIgnored, glass, 1,
            allDigits, complement, memoryValue, goalIgnored, moneyIgnored), null);
    }

    /// <summary>The near, median or money transform bound to the goal or money amount, or null when that value wasn't given.</summary>
    static LeftTransform? BoundTransform(string token, long? goal, long? money) => token switch
    {
        "near" => goal is { } g1 ? LeftTransforms.Near(g1) : null,
        "median" => goal is { } g2 ? LeftTransforms.Median(g2) : null,
        _ => money is { } m ? LeftTransforms.Money(m) : null,
    };
}

/// <summary>The game state a line runs in, for buttons that read it: the goal (near, median; plus the charged presses so far in boss rule 14) and the money.
/// Blind: the round's first non-glass press is free (passive_blind): no goal raise, no rule-10 coin.
/// ChargedBefore: charged presses already made this round (the coinsub loop), for the diff_type &gt;= 7 drain.</summary>
public sealed record LineContext(long Goal, bool GoalStep = false, long Money = 0, MoneyRules? MoneyRules = null, bool Blind = false,
                                 int ChargedBefore = 0);

/// <summary>How money changes during a line. DrainFromThird: diff_type &gt;= 7. Rule10: boss rule 10. Passives: the coin passives present.</summary>
public sealed record MoneyRules(bool DrainFromThird, bool Rule10, IReadOnlyCollection<string> Passives);

/// <summary>A press simulator for the early-game buttons.</summary>
public static class EngineeringCalculator
{
    /// <summary>
    /// Runs presses from start and returns the entry after each press (the operand while the right
    /// side is active). Returns null if a press is invalid: a button missing from `buttons` (when
    /// given), a left-side-only button (flip, reset, a transform such as X++ or sqr) on the right side, `10` or a digit key on the
    /// left side, ÷ or mod by 0, = with nothing pending, Xd or dX on a display of <see cref="MaxDisplay"/> characters, or an operand
    /// key on an operand of <see cref="MaxOperandTyping"/> characters (the game would restart it). +, ×, X+k, X*2, &lt;&lt; and sqr
    /// clamp results above <see cref="Max"/> to it and play goes on. With a <see cref="Buttons.Context"/> near and median read the goal
    /// at each press and money reads the money at that press (see <see cref="LineContext"/>).
    /// </summary>
    const long MaxPercent = 1 << 24;

    /// <summary>The game's biggest result: +, ×, X+k, X*2, &lt;&lt;, sqr clamp to it; p, round and % don't.</summary>
    public const long Max = 999_999;

    /// <summary>Xd and dX do nothing once the display text has this many characters (leading zeros count).</summary>
    public const int MaxDisplay = 6;

    /// <summary>An operand key appends only while the operand has fewer characters than this; after that the game restarts the operand.</summary>
    public const int MaxOperandTyping = 5;

    /// <summary>dX: the game prepends to the number, so leading zeros drop first ("04" + 1X = "14"; "0" + 1X = "10").</summary>
    public static string Prepend(string display, int digit) => digit + long.Parse(display).ToString();

    /// <summary>flip: the game reverses the number and the result is a number, so leading zeros drop on both sides ("120" → "21", "04" → "4").</summary>
    public static string Flip(string display) =>
        long.Parse(new string(long.Parse(display).ToString().Reverse().ToArray())).ToString();

    /// <summary>== (the game's MorphAll): every digit of the left side's number becomes the first digit of the typed text.</summary>
    public static string AllDigitsOf(long left, string typed) => long.Parse(new string(typed[0], left.ToString().Length)).ToString();

    /// <summary>AX (the game's Complement): the typed text in front of the left side's number, read as a number.</summary>
    public static string ComplementOf(long left, string typed) => long.Parse(typed + left.ToString()).ToString();

    /// <summary>Boss rule 12: every = adds 1 to its result.</summary>
    public const int SendPlusOneRule = 12;

    /// <summary>Boss rule 13: an = that misses the goal re-rolls it.</summary>
    public const int RerollOnMissRule = 13;

    /// <summary>True when the line presses no = before its last press (boss rule 13: the only = is the one that sends).</summary>
    public static bool OneSend(IReadOnlyList<Press> presses)
    {
        for (int i = 0; i < presses.Count - 1; i++)
            if (presses[i].Key == Key.Equals)
                return false;
        return true;
    }

    /// <summary>Boss rule 6: each press costs 2 charges.</summary>
    public const int TakeTwoRule = 6;

    /// <summary>
    /// The number a line sends: the last display, plus 1 with plusOne when the line doesn't end with = (one more = sends it).
    /// Null when the line can't run.
    /// </summary>
    public static long? Sent(long start, IReadOnlyList<Press> presses, Buttons? buttons = null, bool plusOne = false)
    {
        var log = Run(start, presses, buttons, plusOne);
        if (log == null || !long.TryParse(log.Count == 0 ? start.ToString() : log[^1], out var shown))
            return null;
        bool endsWithEquals = presses.Count > 0 && presses[^1].Key == Key.Equals;
        return plusOne && !endsWithEquals ? shown + 1 : shown;
    }

    /// <summary>What a run leaves behind besides the log: the money, the goal raises (boss rule 14), the charged presses and whether the blind press is still free.</summary>
    internal sealed class RunEnd
    {
        public long Money;
        public int GoalRaises, Charged;
        public bool BlindLeft;
    }

    /// <summary>With plusOne (boss rule 12) each = adds 1 to its result, and a bare = is allowed as the last press. `end` is filled when the run succeeds.</summary>
    public static IReadOnlyList<string>? Run(long start, IReadOnlyList<Press> presses, Buttons? buttons = null, bool plusOne = false)
        => Run(start, presses, buttons, plusOne, null);

    internal static IReadOnlyList<string>? Run(long start, IReadOnlyList<Press> presses, Buttons? buttons, bool plusOne, RunEnd? end)
    {
        var display = start.ToString();
        var operand = "";
        Key? op = null;
        long left = 0;
        var log = new List<string>();
        int chargeUsed = 0;
        bool questUsed = false;
        var context = buttons?.Context;
        long money = context?.Money ?? 0;
        int charged = context?.ChargedBefore ?? 0, goalRaises = 0;
        bool freeLeft = context?.Blind == true;

        // The text a right-side key types, or null when the key is not allowed here. Charge and quest
        // presses are counted when they are typed.
        string? TypedText(Press press)
        {
            switch (press.Key)
            {
                case Key.Ten when buttons == null || buttons.Ten:
                    return "10";
                case Key.Digit when press.Digit is >= 0 and <= 9 && (buttons == null || buttons.DigitKeys.Contains(press.Digit)):
                    return press.Digit.ToString();
                case Key.Charge when press.Digit >= 1
                        && (buttons == null || buttons.ChargeValue(chargeUsed) == press.Digit):
                    chargeUsed++;
                    return press.Digit.ToString();
                case Key.Quest when !questUsed && press.Digit is >= 0 and <= 9
                        && (buttons == null || buttons.QuestDigit == press.Digit):
                    questUsed = true;
                    return press.Digit.ToString();
                case Key.Memory when press.Digit >= 0 && (buttons == null || buttons.MemoryValue == press.Digit):
                    return press.Digit.ToString();
                default:
                    return null;
            }
        }

        for (int i = 0; i < presses.Count; i++)
        {
            var press = presses[i];
            if (press.Key != Key.Equals)
            {
                // The game's SubUse runs before a button's effect: every press but = is charged (glass too), and a glass press doesn't raise the goal.
                charged++;
                bool glass = buttons != null && buttons.Glass.Contains(press.ChargeKey);
                // The blind passive's free press skips the charge branch (goal raise, rule-10 coin); the diff_type >= 7 drain still runs.
                bool free = !glass && freeLeft;
                if (free)
                    freeLeft = false;
                else if (!glass)
                    goalRaises++;
                if (context?.MoneyRules is { } rules)
                {
                    if (rules.DrainFromThird && charged >= 3 && money > 0)
                        money--;
                    if (rules.Rule10 && !free && money > 0)
                        money--;
                }
            }
            switch (press.Key)
            {
                case Key.Prepend when op == null && display.Length < MaxDisplay && press.Digit is >= 0 and <= 9
                        && (buttons == null || buttons.PrependDigits.Contains(press.Digit)):
                    display = Prepend(display, press.Digit);
                    log.Add(display);
                    break;
                case Key.Append when op == null && display.Length < MaxDisplay && press.Digit is >= 0 and <= 9
                        && (buttons == null || buttons.AppendDigits.Contains(press.Digit)):
                    display += press.Digit;
                    log.Add(display);
                    break;
                case Key.Ten or Key.Digit or Key.Charge or Key.Quest or Key.Memory
                        when op != null && operand.Length < MaxOperandTyping && TypedText(press) is { } typed:
                    if (op is Key.RemorphA or Key.RemorphB)
                    {
                        // The one key after a->c / b->c ends the operation at once.
                        long k = long.Parse(typed);
                        long morphed = op == Key.RemorphA ? left % 10 + k * 10 : left / 10 * 10 + k;
                        op = null;
                        display = morphed.ToString();
                        log.Add(display);
                        break;
                    }
                    if (op is Key.AllDigits or Key.Complement)
                    {
                        // The one key after == / AX ends the operation at once. AX can make a display longer than the game's 6 characters; the solver drops that.
                        var made = op == Key.AllDigits ? AllDigitsOf(left, typed) : ComplementOf(left, typed);
                        if (op == Key.Complement && made.Length > MaxDisplay)
                            return null;
                        op = null;
                        display = made;
                        log.Add(display);
                        break;
                    }
                    operand += typed;
                    log.Add(operand);
                    break;
                case Key.Reset when op == null && (buttons == null || buttons.Reset):
                    display = "0";
                    log.Add(display);
                    break;
                case Key.Flip when op == null && (buttons == null || buttons.Flip):
                    display = Flip(display);
                    log.Add(display);
                    break;
                case Key.Transform when op == null && press.Transform is { } t
                        && (buttons == null || buttons.Transforms.Contains(t)):
                    // Near and median read the goal shown at the press, money the amount left after the press paid.
                    if (context != null && t.Reads != Reads.None)
                        t = t.Reads == Reads.Goal ? t.Bind!(context.Goal + (context.GoalStep ? goalRaises : 0)) : t.Bind!(money);
                    if (t.Apply(display) is not { } next)
                        return null;
                    display = next;
                    log.Add(display);
                    break;
                case Key.Divide or Key.Mod or Key.Add or Key.Subtract or Key.Multiply
                        or Key.Percent or Key.RemorphA or Key.RemorphB or Key.AllDigits or Key.Complement
                        when op == null && (buttons == null || buttons.Has(press.Key))
                             && (press.Key != Key.Complement || display.Length < MaxDisplay):
                    if (!long.TryParse(display, out left))
                        return null;
                    display = left.ToString();
                    op = press.Key;
                    operand = "";
                    log.Add(display);
                    break;
                case Key.Equals when op != null && operand.Length > 0:
                    if (!long.TryParse(operand, out var d))
                        return null;
                    long? result = Apply(op.Value, left, d, out var invalid);
                    if (invalid)
                        return null;
                    op = null;
                    if (result == null)
                        return null;
                    money += CoinPassives(context, result.Value);
                    display = (result.Value + (plusOne ? 1 : 0)).ToString();
                    log.Add(display);
                    break;
                case Key.Equals when op == null && plusOne && i == presses.Count - 1:
                    // A bare send: the game reads the number (leading zeros drop) and adds 1.
                    if (!long.TryParse(display, out var shown))
                        return null;
                    money += CoinPassives(context, shown);
                    display = (shown + 1).ToString();
                    log.Add(display);
                    break;
                case Key.CoinSub when op == null && (buttons == null || buttons.Has(Key.CoinSub)):
                    // MainCalc.UseCoins(-1): with coins left after SubUse's drains on this press (rule 10 takes one first) the display rises
                    // by 1 (PlusNumber reads the number); without them nothing happens.
                    if (money > 0)
                    {
                        money--;
                        display = Math.Min(long.Parse(display) + 1, Max).ToString();
                    }
                    log.Add(display);
                    break;
                default:
                    return null;
            }
        }

        if (end != null)
        {
            end.Money = money;
            end.GoalRaises = goalRaises;
            end.Charged = charged;
            end.BlindLeft = freeLeft;
        }
        return log;
    }

    /// <summary>The money an = adds for the coin passives, on its result before the rule-12 + 1 (0 without a context).</summary>
    static long CoinPassives(LineContext? context, long result)
    {
        if (context?.MoneyRules?.Passives is not { } passives)
            return 0;
        long gain = 0;
        if (passives.Contains("passive777") && result == 777) gain += 10;
        if (passives.Contains("passive_lucky") && result % 7 == 0) gain++;
        if (passives.Contains("passive_perfect") && result % 10 == 0) gain++;
        if (passives.Contains("passive_odd") && result % 2 != 0) gain++;
        return gain;
    }

    /// <summary>
    /// One operator on a left value and a typed operand, as `=` applies it. + and × clamp to <see cref="Max"/>. Invalid is true
    /// for ÷ or mod by 0 and a % result above 2^24 (the press isn't allowed), with a null result.
    /// </summary>
    internal static long? Apply(Key op, long left, long d, out bool invalid)
    {
        invalid = d == 0 && op is Key.Divide or Key.Mod;
        if (invalid)
            return null;
        long? result = op switch
        {
            Key.Divide => left / d,
            Key.Mod => left % d,
            Key.Add => Math.Min(left + d, Max),
            Key.Subtract => Math.Max(0, left - d),
            Key.Multiply => d == 0 ? 0 : left > Max / d ? Max : Math.Min(left * d, Max),
            // The game keeps the result as a float, so above 2^24 it may be off.
            Key.Percent => d == 0 ? 0 : left <= long.MaxValue / d ? left * d / 100 : MaxPercent + 1,
            _ => null,
        };
        invalid = op == Key.Percent && result > MaxPercent;
        return invalid ? null : result;
    }

    /// <summary>Number of presses that cost something. = is free.</summary>
    public static int Cost(IReadOnlyList<Press> presses) => presses.Count(p => p.Key != Key.Equals);
}
