using System.Text.Json;
using System.Text.RegularExpressions;

namespace CalculatorPuzzle;

/// <summary>
/// A puzzle published by the game mod. ButtonsText is in the Buttons box syntax ("X1:2, 10:13").
/// Required is the Market Demand or boss rule-11 button as a solver token (or the unmapped id without "operator_"); null when none.
/// RequiredKind is "market", "boss" or null (an older mod doesn't publish it).
/// BossRule is the boss rule number of the round (0 outside boss rounds, or from an older mod). Glass buttons are written "X1:1g" in ButtonsText.
/// Blocked lists the tokens of buttons the game blocked (rule 2, rule 9) that still had charges; they are written ":0" in ButtonsText.
/// Last is the previous round's goal (<c>StartLvl.last</c>), or null when the mod didn't publish it.
/// Warp is the number of warp-button re-rolls in this round (0 from an older mod).
/// Near1 is true when the player owns the near-one passive (a send within 1 of the goal wins); false from an older mod.
/// Coins is the player's money (<c>StartLvl.coins</c>), DiffType the difficulty (<c>diff_type</c>) and CoinPassives the coin passives present
/// (passive777, passive_lucky, passive_perfect, passive_odd); null / 0 / null from an older mod.
/// </summary>
public sealed record GamePuzzle(int Seq, string Time, int Round, long Start, long Goal,
                                string ButtonsText, IReadOnlyList<string> Ignored, IReadOnlyList<string> Glass,
                                string? Required = null, long? Last = null, string? RequiredKind = null, int BossRule = 0,
                                IReadOnlyList<string>? Blocked = null, int Warp = 0, bool Near1 = false,
                                long? Coins = null, int DiffType = 0, IReadOnlyList<string>? CoinPassives = null)
{
    /// <summary>How money changes during a line in this round (the difficulty drain, boss rule 10 and the coin passives).</summary>
    public MoneyRules MoneyRules => new(DiffType >= 7, BossRule == 10, CoinPassives ?? []);
}

/// <summary>Reads puzzle.json, which the read-only MelonLoader mod writes when a new puzzle has settled.</summary>
public static class GameLink
{
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CalcHook", "puzzle.json");

    /// <summary>The solver token for a game button id, or null when the solver doesn't know the button.</summary>
    public static string? MapButton(string id)
    {
        if (Regex.Match(id, @"^operator_(\d)x$") is { Success: true } pre)
            return pre.Groups[1].Value + "X";
        if (Regex.Match(id, @"^operator_x(\d)$") is { Success: true } app)
            return "X" + app.Groups[1].Value;
        if (Regex.Match(id, @"^number(\d|10)$") is { Success: true } num)
            return num.Groups[1].Value;
        if (Regex.Match(id, @"^operator_(\d)to(\d)$") is { Success: true } map)
            return $"{map.Groups[1].Value}->{map.Groups[2].Value}";
        if (Regex.Match(id, @"^operator_([+-])(\d+)$") is { Success: true } step)
            return $"X{step.Groups[1].Value}{step.Groups[2].Value}";
        return id switch
        {
            "operator/" => "÷",
            "operator+" => "+",
            "operator-" => "-",
            "operator*" => "×",
            "operator_mod" => "mod",
            "operator_swap" => "swap",
            "operator_reverse" => "flip",
            "operator_to0" => "reset",
            "operator_++" => "X++",
            "operator_--" => "X--",
            "operator_*2" => "X*2",
            "operator_/2" => "X/2",
            "operator_<" => "<<",
            "operator_>" => ">>",
            "operator_sqr" => "sqr",
            "operator_sqrt" => "sqrt",
            "operator_to25" => "to25",
            "operator_round" => "round",
            "operator_prime" => "p",
            "number_charge" => "c",
            "number_quest" => "q",
            "operator%" => "%",
            "operator_last" => "last",
            "operator_remorph_a" => "a->c",
            "operator_remorph_b" => "b->c",
            "operator_100-x" => "100-X",
            "operator_flip" => "25|69",
            "operator_near" => "near",
            "operator_mediana" => "median",
            "operator_sup" => "sup",
            "operator_money" => "money",
            "operator_equal" => "==",
            "operator_complement" => "AX",
            "number_mw" => "m",
            _ => null,
        };
    }

    /// <summary>Parses puzzle.json. Returns null for broken JSON, a missing field or a version other than 1.</summary>
    public static GamePuzzle? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.GetProperty("version").GetInt32() != 1)
                return null;

            var tokens = new List<string>();
            var ignored = new List<string>();
            var glassTokens = new List<string>();
            var blockedTokens = new List<string>();
            foreach (var button in root.GetProperty("buttons").EnumerateArray())
            {
                var id = button.GetProperty("id").GetString() ?? "";
                int uses = button.GetProperty("uses").GetInt32();
                bool glass = button.TryGetProperty("glass", out var g) && g.ValueKind == JsonValueKind.True;
                bool blocked = button.TryGetProperty("blocked", out var bl) && bl.ValueKind == JsonValueKind.True;
                if (MapButton(id) is { } token)
                {
                    // Glass breaks after one press, whatever the charge count says. A blocked button can't be pressed at all.
                    if (blocked) glass = false;
                    int charge = blocked ? 0 : glass ? Math.Min(uses, 1) : uses;
                    string count = glass && charge > 0 ? charge + "g" : charge.ToString();
                    if (token == "c")
                        // The charge key types its charge count (the raw uses), so the token carries it: c12:12, or c:0.
                        tokens.Add(uses > 0 && !blocked ? $"{ButtonLabels.Label($"c{uses}")}:{count}" : $"{ButtonLabels.Label("c")}:0");
                    else if (token == "q")
                    {
                        // The quest key types its current digit; an older mod doesn't publish it.
                        if (button.TryGetProperty("digit", out var digit) && digit.ValueKind == JsonValueKind.Number)
                            tokens.Add($"{ButtonLabels.Label($"q{digit.GetInt32()}")}:{count}");
                        else
                        {
                            ignored.Add("quest");
                            continue;
                        }
                    }
                    else if (token == "m")
                    {
                        // The mw key types its stored number; an empty one (nothing absorbed yet) can't be used.
                        if (button.TryGetProperty("stored", out var stored) && stored.ValueKind == JsonValueKind.Number)
                            tokens.Add($"{ButtonLabels.Label($"m{stored.GetInt32()}")}:{count}");
                        else
                        {
                            ignored.Add("mw");
                            continue;
                        }
                    }
                    else
                        tokens.Add($"{ButtonLabels.Label(token)}:{count}");
                    if (glass) glassTokens.Add(token);
                    if (blocked && uses > 0) blockedTokens.Add(token);
                }
                else
                    ignored.Add(id.StartsWith("operator_") ? id["operator_".Length..] : id);
            }

            string? required = null;
            if (root.TryGetProperty("required", out var r) && r.ValueKind == JsonValueKind.String && r.GetString() is { Length: > 0 } requiredId)
                required = MapButton(requiredId) ?? (requiredId.StartsWith("operator_") ? requiredId["operator_".Length..] : requiredId);
            string? requiredKind = required != null && root.TryGetProperty("requiredKind", out var rk)
                && rk.ValueKind == JsonValueKind.String && rk.GetString() is { Length: > 0 } kind ? kind : null;

            return new GamePuzzle(
                root.GetProperty("seq").GetInt32(),
                root.GetProperty("time").GetString() ?? "",
                root.GetProperty("round").GetInt32(),
                root.GetProperty("start").GetInt64(),
                root.GetProperty("goal").GetInt64(),
                string.Join(", ", tokens),
                ignored,
                glassTokens,
                required,
                root.TryGetProperty("last", out var lastElement) && lastElement.ValueKind == JsonValueKind.Number
                    ? lastElement.GetInt64() : null,
                requiredKind,
                root.TryGetProperty("bossRule", out var boss) && boss.ValueKind == JsonValueKind.Number ? boss.GetInt32() : 0,
                blockedTokens,
                root.TryGetProperty("warp", out var warp) && warp.ValueKind == JsonValueKind.Number && warp.TryGetInt32(out var warpCount) && warpCount > 0
                    ? warpCount : 0,
                root.TryGetProperty("near1", out var near1) && near1.ValueKind == JsonValueKind.True,
                root.TryGetProperty("coins", out var coins) && coins.ValueKind == JsonValueKind.Number ? coins.GetInt64() : null,
                root.TryGetProperty("diffType", out var diff) && diff.ValueKind == JsonValueKind.Number && diff.TryGetInt32(out var diffType) ? diffType : 0,
                root.TryGetProperty("coinPassives", out var passives) && passives.ValueKind == JsonValueKind.Array
                    ? passives.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.String).Select(p => p.GetString()!).ToList() : null);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
