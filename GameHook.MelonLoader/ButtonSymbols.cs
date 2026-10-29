#nullable disable
using System.Text.RegularExpressions;

namespace CalcHook
{
    /// <summary>Overlay text for a game button id. Plain C# so the test project can link it.</summary>
    public static class ButtonSymbols
    {
        /// <summary>The in-game label for the id (ButtonLabels.Label of its solver token), or "(name)" for an id the solver doesn't map.</summary>
        public static string Of(string id) => Token(id) is string t ? Label(t) : "(" + Bare(id) + ")";

        static readonly System.Collections.Generic.Dictionary<string, string> Fixed = new System.Collections.Generic.Dictionary<string, string>
        {
            ["money"] = "$", ["flip"] = "reverse", ["round"] = "->)", ["sup"] = "combine",
            ["sqr"] = "X²", ["median"] = "_2_", ["25|69"] = "mirror", ["=="] = "equalize",
            ["<<"] = "X<<", [">>"] = "X>>", ["near"] = "+-10", ["reset"] = "x->0", ["to25"] = "x->25", ["coinsub"] = "$++",
        };

        /// <summary>Mirrors ButtonLabels.Label (CalculatorPuzzle/ButtonLabels.cs). ButtonSymbolsTests keeps them equal.</summary>
        public static string Label(string token)
        {
            string label;
            if (Fixed.TryGetValue(token, out label)) return label;
            Match m;
            if ((m = Regex.Match(token, @"^c(\d*)$")).Success) return "volt" + m.Groups[1].Value;
            if ((m = Regex.Match(token, @"^q(\d?)$")).Success) return "unk" + m.Groups[1].Value;
            if ((m = Regex.Match(token, @"^m(\d+)$")).Success) return "mem" + m.Groups[1].Value;
            return token == "mw" || token == "m" ? "mem" : token;
        }

        /// <summary>
        /// Like Of(id), but the charge key shows the value it would type ("c12") and the quest key its current digit
        /// ("q8"). uses is the charge count, digit the quest digit (-1 = unknown).
        /// </summary>
        public static string Of(string id, int uses, int digit)
        {
            if (id == "number_charge" && uses > 0) return Label("c" + uses);
            if (id == "number_quest" && digit >= 0) return Label("q" + digit);
            return Of(id);
        }

        /// <summary>Like Of(id, uses, digit), and the prime key shows the nearest prime of the left side ("p8081").</summary>
        public static string Of(string id, int uses, int digit, long left) =>
            id == "operator_prime" ? "p" + NearestPrime(left) : Of(id, uses, digit);

        /// <summary>Like Of(id, uses, digit, left), and the mw key shows the number it stores ("m5"), or "mw" while it is empty (stored &lt; 0).</summary>
        public static string Of(string id, int uses, int digit, long left, int stored) =>
            id == "number_mw" ? (stored >= 0 ? Label("m" + stored) : Label("mw")) : Of(id, uses, digit, left);

        /// <summary>The game's MainCalc.FindNearestPrime: below 2 gives 2; otherwise the nearest prime, a tie goes down.</summary>
        public static long NearestPrime(long v)
        {
            if (v < 2) return 2;
            for (long d = 0; ; d++)
            {
                if (IsPrime(v - d)) return v - d;
                if (IsPrime(v + d)) return v + d;
            }
        }

        static readonly ulong[] Bases = { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 };

        // Deterministic Miller-Rabin for n below 2^63.
        static bool IsPrime(long n)
        {
            if (n < 2) return false;
            ulong v = (ulong)n;
            foreach (ulong b in Bases)
            {
                if (v == b) return true;
                if (v % b == 0) return false;
            }
            ulong d = v - 1;
            int s = 0;
            while ((d & 1) == 0) { d >>= 1; s++; }
            foreach (ulong b in Bases)
            {
                ulong x = PowMod(b, d, v);
                if (x == 1 || x == v - 1) continue;
                bool composite = true;
                for (int i = 1; i < s && composite; i++)
                {
                    x = MulMod(x, x, v);
                    if (x == v - 1) composite = false;
                }
                if (composite) return false;
            }
            return true;
        }

        // Double-and-add: operands are below 2^63, so a + a never overflows.
        static ulong MulMod(ulong a, ulong b, ulong m)
        {
            ulong result = 0;
            a %= m;
            while (b > 0)
            {
                if ((b & 1) == 1) result = (result + a) % m;
                a = (a + a) % m;
                b >>= 1;
            }
            return result;
        }

        static ulong PowMod(ulong b, ulong e, ulong m)
        {
            ulong result = 1;
            b %= m;
            while (e > 0)
            {
                if ((e & 1) == 1) result = MulMod(result, b, m);
                b = MulMod(b, b, m);
                e >>= 1;
            }
            return result;
        }

        // Mirrors GameLink.MapButton (CalculatorPuzzle/GameLink.cs). ButtonSymbolsTests keeps them equal.
        public static string Token(string id)
        {
            Match m;
            if ((m = Regex.Match(id, @"^operator_(\d)x$")).Success) return m.Groups[1].Value + "X";
            if ((m = Regex.Match(id, @"^operator_x(\d)$")).Success) return "X" + m.Groups[1].Value;
            if ((m = Regex.Match(id, @"^number(\d|10)$")).Success) return m.Groups[1].Value;
            if ((m = Regex.Match(id, @"^operator_(\d)to(\d)$")).Success) return m.Groups[1].Value + "->" + m.Groups[2].Value;
            if ((m = Regex.Match(id, @"^operator_([+-])(\d+)$")).Success) return "X" + m.Groups[1].Value + m.Groups[2].Value;
            switch (id)
            {
                case "operator/": return "÷";
                case "operator+": return "+";
                case "operator-": return "-";
                case "operator*": return "×";
                case "operator_mod": return "mod";
                case "operator_swap": return "swap";
                case "operator_coinsub": return "coinsub";
                case "operator_reverse": return "flip";
                case "operator_to0": return "reset";
                case "operator_++": return "X++";
                case "operator_--": return "X--";
                case "operator_*2": return "X*2";
                case "operator_/2": return "X/2";
                case "operator_<": return "<<";
                case "operator_>": return ">>";
                case "operator_sqr": return "sqr";
                case "operator_sqrt": return "sqrt";
                case "operator_to25": return "to25";
                case "operator_round": return "round";
                case "operator_prime": return "p";
                case "number_charge": return "c";
                case "number_quest": return "q";
                case "operator%": return "%";
                case "operator_last": return "last";
                case "operator_remorph_a": return "a->c";
                case "operator_remorph_b": return "b->c";
                case "operator_100-x": return "100-X";
                case "operator_flip": return "25|69";
                case "operator_near": return "near";
                case "operator_mediana": return "median";
                case "operator_sup": return "sup";
                case "operator_money": return "money";
                case "operator_equal": return "==";
                case "operator_complement": return "AX";
                case "number_mw": return "m";
                default: return null;
            }
        }

        // "operator_energy" -> "energy", "operator%" -> "%", "number11" -> "11", "number_charge" -> "charge".
        static string Bare(string id)
        {
            string s = id.StartsWith("operator") ? id.Substring(8) : id.StartsWith("number") ? id.Substring(6) : id;
            return s.StartsWith("_") ? s.Substring(1) : s;
        }
    }
}
