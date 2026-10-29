using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace CalcHook
{
    /// <summary>
    /// Read-only state reader used by the MelonLoader mod.
    /// Only reads fields; never calls game methods that change state.
    /// Field names come from REA's static analysis of Assembly-CSharp.dll:
    /// StartLvl.number (goal), StartLvl.calc_transform (button parent), MainCalc.total (left side),
    /// MainCalc.operand / currentoperand (pending operation), CountUses.buttonKey / uses (per-button charges).
    /// </summary>
    public sealed class CalcHookCore
    {
        public const string Tag = "[CalcHook]";

        readonly Action<string> _info;
        readonly Action<string> _error;
        readonly string _statePath;
        readonly string _puzzlePath;

        // Puzzle publishing: a puzzle is (round, goal). It is written once, after the game settles.
        const float SettleSeconds = 0.05f;
        bool _hasPublished;
        int _publishedRound, _publishedGoal;
        bool _pending = true;
        string _fingerprint;
        int _warps;  // warp-button presses in this round
        bool _publishedNear1;  // passive_near1 as of the last publish
        bool _publishedBlindOwned;  // passive_blind as of the last publish (not the flag: it drops on the round's first press)
        float _stableSince;
        int _seq;

        StartLvl _level;
        MainCalc _calc;
        float _nextPoll;
        string _lastLine;
        long? _roundStart;
        int _lastGoal = int.MinValue;
        readonly HashSet<string> _reportedErrors = new HashSet<string>();

        // Market Demand (challenge 8): StartLvl.challengeButtonName is private, so it is read by reflection.
        const int MarketDemand = 8;
        // Boss rule 11: "You must use button ..., otherwise the answer will not be counted." Public fields, never in challenge 8.
        const int MustUseRule = 11;
        // Boss rule 14: "The needed number increases by 1 each time the charge is spent." The goal moves while the line is pressed.
        const int GoalPerChargeRule = 14;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly FieldInfo RequiredNameField = typeof(StartLvl).GetField("challengeButtonName", Private);
        static readonly FieldInfo RequiredLabelField = typeof(StartLvl).GetField("challengeButtonLabel", Private);
        static readonly FieldInfo RequiredUsesField = typeof(StartLvl).GetField("challengeButtonUses", Private);

        public CalcHookCore(Action<string> info, Action<string> error)
        {
            _info = info;
            _error = error;
            _statePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CalcHook", "state.json");
            _puzzlePath = Path.Combine(Path.GetDirectoryName(_statePath), "puzzle.json");
        }

        public void OnLoaded(string loaderName, string loaderVersion)
        {
            Safe("OnLoaded", () =>
                _info(Tag + " loaded (" + loaderName + " " + loaderVersion + ", Unity " + Application.unityVersion + ")"));
        }

        /// <summary>Call every frame; polls about once a second and runs the puzzle settle check.</summary>
        public void Tick()
        {
            Safe("Settle", CheckPuzzle);
            Safe("Overlay", UpdateOverlay);
            Safe("Tick", () =>
            {
                if (Time.unscaledTime < _nextPoll) return;
                _nextPoll = Time.unscaledTime + 1f;
                Poll("poll");
            });
        }

        /// <summary>Called from the Harmony postfix on StartLvl.NextLvl (a new round was set up).</summary>
        public void OnRoundLoaded(StartLvl level)
        {
            Safe("OnRoundLoaded", () =>
            {
                if (level != null) _level = level;
                _roundStart = null;
                _lastGoal = int.MinValue;
                _warps = 0;
                _pending = true;
                _fingerprint = null;
                Poll("NextLvl");
            });
        }

        /// <summary>Called from the Harmony postfix on StartLvl.D20 (the warp button replaced every button).</summary>
        public void OnWarp(StartLvl level)
        {
            Safe("OnWarp", () =>
            {
                if (level != null) _level = level;
                _warps++;
                _pending = true;
                _fingerprint = null;
                _info(Tag + " warp " + _warps + ": buttons re-rolled");
            });
        }

        /// <summary>
        /// Every frame. A new (round, goal) becomes pending. It is published once the game isn't restoring
        /// or transitioning and round, goal, left side, and button ids and charges have been unchanged for SettleSeconds.
        /// </summary>
        void CheckPuzzle()
        {
            if (_level == null) _level = UnityEngine.Object.FindObjectOfType<StartLvl>();
            if (_calc == null) _calc = UnityEngine.Object.FindObjectOfType<MainCalc>();
            if (_level == null || _calc == null) return;

            int round = _level.round;
            int goal = _level.number;
            // In a rule-14 round every charged press changes the goal; publish once per round, not once per goal.
            bool goalMoves = _level.IsBossRound && _level.rule == GoalPerChargeRule;
            // The passive can be picked after the puzzle was published (the perk picker); publish again, like a warp.
            bool near1 = Near1();
            bool blindOwned = BlindOwned();
            if (!_hasPublished || round != _publishedRound || (!goalMoves && goal != _publishedGoal) || near1 != _publishedNear1
                || blindOwned != _publishedBlindOwned)
            {
                if (!_pending) { _pending = true; _fingerprint = null; }
            }
            if (!_pending) return;

            long left = _calc.total;
            if (IsRestoring() || _level.IsTransitioning || left == goal)
            {
                _fingerprint = null;
                return;
            }

            var buttons = ReadButtons();
            var fp = new StringBuilder();
            fp.Append(round).Append('|').Append(goal).Append('|').Append(left);
            foreach (var b in buttons)
            {
                fp.Append('|').Append(b.Key).Append(':').Append(b.Uses).Append(b.Glass ? ":g" : "");
                // An absorb changes the button set (the mw key starts to type a number); coins are left out, they change with every press in some modes.
                if (b.Key == "number_mw") fp.Append("|mw:").Append(b.Stored >= 0 ? b.Stored.ToString() : "empty");
            }
            string kind;
            string required = RequiredKey(buttons, out kind);
            fp.Append("|req:").Append(required ?? "-").Append(':').Append(kind ?? "-");
            fp.Append("|near1:").Append(near1 ? '1' : '0');
            fp.Append("|blind:").Append(blindOwned ? '1' : '0');
            string now = fp.ToString();
            float t = Time.realtimeSinceStartup;
            if (now != _fingerprint)
            {
                _fingerprint = now;
                _stableSince = t;
                return;
            }
            if (t - _stableSince < SettleSeconds) return;

            _hasPublished = true;
            _publishedRound = round;
            _publishedGoal = goal;
            _publishedNear1 = near1;
            _publishedBlindOwned = blindOwned;
            _pending = false;
            _seq++;
            WritePuzzle(round, left, goal, buttons, required, kind);
            _info(Tag + " puzzle ready: round " + round + ", " + left + " -> " + goal + ", " + buttons.Count + " buttons"
                + ", coins=" + _level.coins
                + (_warps > 0 ? ", warp " + _warps : "")
                + (near1 ? ", near1" : "")
                + (Blind() ? ", blind" : "")
                + (required != null ? ", required " + required + (kind == "boss" ? " (boss)" : "") : ""));
        }

        /// <summary>The near-one passive is owned: its prefab instance is a GameObject named passive_near1 (the game's own test).</summary>
        static bool Near1() => UnityEngine.GameObject.Find("passive_near1") != null;

        /// <summary>The blind passive is owned (GameObject passive_blind, the game's own test).</summary>
        static bool BlindOwned() => UnityEngine.GameObject.Find("passive_blind") != null;

        /// <summary>The blind flag (GamePrefs "blind" = 1): the round's next normal press is free. It drops to 0 on that press.</summary>
        static bool Blind()
        {
            // GamePrefs may not be ready yet (menu); that counts as no free press.
            try { return GamePrefs.GetInt("blind") == 1; }
            catch (System.Exception) { return false; }
        }

        // The coin passives that pay out on an = (MainCalc.Send); the game tests each with GameObject.Find.
        static readonly string[] CoinPassiveNames = { "passive777", "passive_lucky", "passive_perfect", "passive_odd" };

        /// <summary>JSON array of the coin passives the player owns.</summary>
        static string CoinPassivesJson()
        {
            var owned = new List<string>();
            foreach (var name in CoinPassiveNames)
                if (UnityEngine.GameObject.Find(name) != null) owned.Add("\"" + name + "\"");
            return "[" + string.Join(", ", owned.ToArray()) + "]";
        }

        // GamePrefs may not be ready yet (menu); that counts as difficulty 0.
        static int DiffType()
        {
            try { return GamePrefs.GetInt("diff_type"); }
            catch (NullReferenceException) { return 0; }
        }

        // RunSave may not exist yet (menu); that counts as not restoring.
        static bool IsRestoring()
        {
            try { return RunSave.Restoring; }
            catch (NullReferenceException) { return false; }
        }

        void WritePuzzle(int round, long start, int goal, List<ButtonInfo> buttons, string required, string kind)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("{\n  \"version\": 1,\n");
                sb.Append("  \"seq\": ").Append(_seq).Append(",\n");
                sb.Append("  \"time\": ").Append(Str(DateTime.Now.ToString("o"))).Append(",\n");
                sb.Append("  \"round\": ").Append(round).Append(",\n");
                sb.Append("  \"start\": ").Append(start).Append(",\n");
                sb.Append("  \"goal\": ").Append(goal).Append(",\n");
                sb.Append("  \"calculator\": ").Append(Str(_level.calculator != null ? _level.calculator.name : null)).Append(",\n");
                sb.Append("  \"scene\": ").Append(Str(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)).Append(",\n");
                sb.Append("  \"challenge\": ").Append(Challenge()).Append(",\n");
                sb.Append("  \"bossRule\": ").Append(_level.IsBossRound ? _level.rule : 0).Append(",\n");
                sb.Append("  \"required\": ").Append(Str(required)).Append(",\n");
                sb.Append("  \"requiredKind\": ").Append(Str(kind)).Append(",\n");
                sb.Append("  \"last\": ").Append(_level.last).Append(",\n");
                sb.Append("  \"warp\": ").Append(_warps).Append(",\n");
                sb.Append("  \"near1\": ").Append(Near1() ? "true" : "false").Append(",\n");
                sb.Append("  \"blind\": ").Append(Blind() ? "true" : "false").Append(",\n");
                sb.Append("  \"coins\": ").Append(_level.coins).Append(",\n");
                sb.Append("  \"diffType\": ").Append(DiffType()).Append(",\n");
                sb.Append("  \"coinPassives\": ").Append(CoinPassivesJson()).Append(",\n");
                sb.Append("  \"buttons\": [");
                for (int i = 0; i < buttons.Count; i++)
                {
                    var b = buttons[i];
                    sb.Append(i == 0 ? "\n" : ",\n");
                    sb.Append("    { \"id\": ").Append(Str(b.Key))
                      .Append(", \"label\": ").Append(Str(b.Label))
                      .Append(", \"uses\": ").Append(b.Uses)
                      .Append(", \"glass\": ").Append(b.Glass ? "true" : "false")
                      .Append(", \"blocked\": ").Append(b.Blocked ? "true" : "false")
                      .Append(", \"legendary\": ").Append(b.Legendary ? "true" : "false")
                      .Append(b.Digit >= 0 ? ", \"digit\": " + b.Digit : "")
                      .Append(b.Stored >= 0 ? ", \"stored\": " + b.Stored : "")
                      .Append(", \"col\": ").Append(b.Col)
                      .Append(", \"row\": ").Append(b.Row).Append(" }");
                }
                sb.Append(buttons.Count > 0 ? "\n  ]\n" : "]\n");
                sb.Append("}\n");
                WriteAtomic(_puzzlePath, sb.ToString());
            }
            catch (Exception e)
            {
                Report("WritePuzzle", e);
            }
        }

        static void WriteAtomic(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        void Poll(string source)
        {
            if (_level == null) _level = UnityEngine.Object.FindObjectOfType<StartLvl>();
            if (_calc == null) _calc = UnityEngine.Object.FindObjectOfType<MainCalc>();
            if (_level == null || _calc == null) return;

            int goal = _level.number;
            long left = _calc.total;
            if (goal != _lastGoal)
            {
                // First reading after the goal changes is the round's start number.
                _lastGoal = goal;
                _roundStart = left;
            }

            var buttons = ReadButtons();
            string kind;
            string required = RequiredKey(buttons, out kind);
            var sb = new StringBuilder();
            sb.Append("start=").Append(_roundStart.HasValue ? _roundStart.Value.ToString() : "?")
              .Append(" goal=").Append(goal)
              .Append(" left=").Append(left)
              .Append(" display=\"").Append(TextOf(_calc.output)).Append('"')
              .Append(" operand=").Append(_calc.operand)
              .Append(" op=").Append(_calc.currentoperand)
              .Append(" last=").Append(_level.last)
              .Append(required != null ? " required=" + required + (kind == "boss" ? " (boss)" : "") : "")
              .Append(" buttons=[");
            for (int i = 0; i < buttons.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(buttons[i].Key).Append(':').Append(buttons[i].Uses)
                  .Append(buttons[i].Glass ? "g" : "").Append(buttons[i].Legendary ? "L" : "")
                  .Append(buttons[i].Digit >= 0 ? "/d" + buttons[i].Digit : "")
                  .Append(buttons[i].Stored >= 0 ? "/m" + buttons[i].Stored : "");
            }
            sb.Append(']');
            var shop = ShopNow(buttons);
            if (shop != null)
            {
                sb.Append(" shop=[");
                for (int i = 0; i < shop.Count; i++)
                {
                    var sl = shop[i];
                    if (i > 0) sb.Append(", ");
                    sb.Append(sl.Slot).Append(':').Append(sl.Key ?? (sl.IsBox && sl.Active && !sl.Opened ? "?" : "-"));
                    if (sl.Key != null) sb.Append('$').Append(sl.Cost);
                    sb.Append(sl.Glass ? "g" : "").Append(sl.Legendary ? "L" : "").Append(sl.Owned ? "+" : "").Append(sl.Opened ? "o" : "");
                }
                sb.Append(']');
            }

            var passives = new List<PassiveOffer>();
            Safe("Passives", () => passives = ReadPassivePicker());
            if (passives.Count > 0) sb.Append(" passives=[").Append(PassiveNames(passives)).Append(']');

            string line = sb.ToString();
            if (line == _lastLine) return;
            _lastLine = line;
            _info(Tag + " " + line);
            WriteJson(source, goal, left, buttons, required, kind, shop, passives);
        }

        struct ButtonInfo
        {
            public string Key;
            public string Name;
            public string ObjectName;  // raw GameObject.name, which is what StartLvl.challengeButtonName holds
            public string Label;
            public int Uses;
            public bool Glass;
            public bool Blocked;       // Blockirator.inf_blocked: the game won't let this button be pressed
            public bool Legendary;
            public int Digit;          // number_quest: the digit it would type now (QuestionNumber.digit); -1 for any other button
            public int Stored;         // number_mw: the number it types once it stores one (NumberReader.number when readed); -1 when empty or any other button
            public int Col, Row;       // grid cell from anchoredPosition; -1 when the transform isn't a RectTransform
        }

        // Grid from the game's layout code: x = -300 + col * 91, y = -115 - row * 80 (StartLvl.MiniCreate, CreateCalc).
        // Place.x / overall aren't used: CreateCalc writes Place.x = i % 8 while drawing at column i % 4 + 2.
        const float GridLeft = -300f, GridTop = -115f, CellW = 91f, CellH = 80f;

        static void CellOf(CountUses cu, out int col, out int row)
        {
            var rt = cu.transform as RectTransform;
            if (rt == null) { col = row = -1; return; }
            var p = rt.anchoredPosition;
            col = Mathf.RoundToInt((p.x - GridLeft) / CellW);
            row = Mathf.RoundToInt(-(p.y - GridTop) / CellH);
        }

        List<ButtonInfo> ReadButtons()
        {
            var list = new List<ButtonInfo>();
            var root = _level.calc_transform;
            if (root == null) return list;
            foreach (var cu in root.GetComponentsInChildren<CountUses>(false))
            {
                if (cu == null || !cu.gameObject.activeInHierarchy) continue;
                string name = cu.gameObject.name.Replace("(Clone)", "").Trim();
                int col, row;
                CellOf(cu, out col, out row);
                list.Add(new ButtonInfo
                {
                    Col = col,
                    Row = row,
                    // Mirrors StartLvl.ButtonKey: buttonKey when set, else the object name.
                    Key = string.IsNullOrEmpty(cu.buttonKey) ? name : cu.buttonKey,
                    Name = name,
                    ObjectName = cu.gameObject.name,
                    Label = LabelOf(cu),
                    Uses = cu.uses,
                    Glass = cu.glassed,
                    Blocked = cu.TryGetComponent<Blockirator>(out var block) && block.inf_blocked,
                    Legendary = IsLegendary(cu),
                    Digit = QuestDigit(cu),
                    Stored = StoredNumber(cu),
                });
            }
            return list;
        }

        // GamePrefs may not be ready yet (menu); that counts as no challenge.
        static int Challenge()
        {
            try { return GamePrefs.GetInt("challenge"); }
            catch (NullReferenceException) { return 0; }
        }

        /// <summary>Reads one private StartLvl field. Default when the field is gone (a game update) or unreadable; logged once.</summary>
        T ReadRequired<T>(FieldInfo field)
        {
            if (field == null)
            {
                Report("Required", new MissingFieldException("StartLvl market-demand field not found"));
                return default(T);
            }
            try { return (T)field.GetValue(_level); }
            catch (InvalidCastException e) { Report("Required", e); return default(T); }
        }

        /// <summary>
        /// The id (as in buttons[].id) of the button the round requires, and why: "market" (Market Demand, challenge 8)
        /// or "boss" (boss rule 11, StartLvl.must_butt). Null when neither applies or no active button has that name.
        /// </summary>
        string RequiredKey(List<ButtonInfo> buttons, out string kind)
        {
            kind = null;
            string name = null, why = null;
            if (Challenge() == MarketDemand)
            {
                name = ReadRequired<string>(RequiredNameField);
                why = "market";
            }
            else if (_level.IsBossRound && _level.rule == MustUseRule && _level.must_butt != null)
            {
                name = _level.must_butt.name;
                why = "boss";
            }
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var b in buttons)
                if (b.ObjectName == name) { kind = why; return b.Key; }
            return null;
        }

        // number_quest: QuestionNumber.digit is public and re-rolled after every press. -1 for other buttons.
        static int QuestDigit(CountUses cu)
        {
            var quest = cu.GetComponent<QuestionNumber>();
            return quest == null ? -1 : quest.digit;
        }

        // number_mw: NumberReader.number is public; it only counts once readed is true (an absorb stored it). -1 otherwise.
        static int StoredNumber(CountUses cu)
        {
            var reader = cu.GetComponent<NumberReader>();
            return reader == null || !reader.readed ? -1 : reader.number;
        }

        // Legendary slots from Descriptionframe.UpdateItemHints: near, cut, equal, complement, mediana.
        static bool IsLegendary(CountUses cu)
        {
            var info = cu.GetComponent<Infos>();
            if (info == null || info.passive) return false;
            int id = info.id;
            return id == 10 || id == 11 || id == 28 || id == 46 || id == 47;
        }

        static string LabelOf(CountUses cu)
        {
            var parts = new List<string>();
            foreach (var t in cu.GetComponentsInChildren<UnityEngine.UI.Text>(false))
                if (t != null && !string.IsNullOrEmpty(t.text)) parts.Add(t.text.Trim());
            return string.Join("|", parts.ToArray());
        }

        static string TextOf(UnityEngine.UI.Text t) => t == null ? "" : t.text;

        void WriteJson(string source, int goal, long left, List<ButtonInfo> buttons, string required, string kind, List<ShopSlot> shop, List<PassiveOffer> passives)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("{\n");
                sb.Append("  \"time\": ").Append(Str(DateTime.Now.ToString("o"))).Append(",\n");
                sb.Append("  \"source\": ").Append(Str(source)).Append(",\n");
                sb.Append("  \"scene\": ").Append(Str(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)).Append(",\n");
                sb.Append("  \"calculator\": ").Append(Str(_level.calculator != null ? _level.calculator.name : null)).Append(",\n");
                sb.Append("  \"round\": ").Append(_level.round).Append(",\n");
                sb.Append("  \"challenge\": ").Append(Challenge()).Append(",\n");
                sb.Append("  \"required\": ").Append(Str(required)).Append(",\n");
                sb.Append("  \"requiredKind\": ").Append(Str(kind)).Append(",\n");
                sb.Append("  \"bossRule\": ").Append(_level.IsBossRound ? _level.rule : 0).Append(",\n");
                sb.Append("  \"mustUsed\": ").Append(_level.must_butted ? "true" : "false").Append(",\n");
                sb.Append("  \"requiredLabel\": ").Append(Str(ReadRequired<string>(RequiredLabelField))).Append(",\n");
                int? requiredUses = ReadRequired<int?>(RequiredUsesField);
                sb.Append("  \"requiredUses\": ").Append(requiredUses.HasValue ? requiredUses.Value.ToString() : "null").Append(",\n");
                sb.Append("  \"start\": ").Append(_roundStart.HasValue ? _roundStart.Value.ToString() : "null").Append(",\n");
                sb.Append("  \"goal\": ").Append(goal).Append(",\n");
                sb.Append("  \"goalText\": ").Append(Str(TextOf(_level.need))).Append(",\n");
                sb.Append("  \"left\": ").Append(left).Append(",\n");
                sb.Append("  \"display\": ").Append(Str(TextOf(_calc.output))).Append(",\n");
                sb.Append("  \"operand\": ").Append(_calc.operand).Append(",\n");
                sb.Append("  \"currentOperator\": ").Append(_calc.currentoperand).Append(",\n");
                sb.Append("  \"last\": ").Append(_level.last).Append(",\n");
                sb.Append("  \"warp\": ").Append(_warps).Append(",\n");
                sb.Append("  \"near1\": ").Append(Near1() ? "true" : "false").Append(",\n");
                sb.Append("  \"blind\": ").Append(Blind() ? "true" : "false").Append(",\n");
                sb.Append("  \"coins\": ").Append(_level.coins).Append(",\n");
                sb.Append("  \"diffType\": ").Append(DiffType()).Append(",\n");
                sb.Append("  \"coinPassives\": ").Append(CoinPassivesJson()).Append(",\n");
                sb.Append("  \"buttons\": [");
                for (int i = 0; i < buttons.Count; i++)
                {
                    var b = buttons[i];
                    sb.Append(i == 0 ? "\n" : ",\n");
                    sb.Append("    { \"key\": ").Append(Str(b.Key))
                      .Append(", \"name\": ").Append(Str(b.Name))
                      .Append(", \"label\": ").Append(Str(b.Label))
                      .Append(", \"uses\": ").Append(b.Uses)
                      .Append(", \"glass\": ").Append(b.Glass ? "true" : "false")
                      .Append(", \"blocked\": ").Append(b.Blocked ? "true" : "false")
                      .Append(", \"legendary\": ").Append(b.Legendary ? "true" : "false")
                      .Append(b.Digit >= 0 ? ", \"digit\": " + b.Digit : "")
                      .Append(b.Stored >= 0 ? ", \"stored\": " + b.Stored : "")
                      .Append(", \"col\": ").Append(b.Col)
                      .Append(", \"row\": ").Append(b.Row).Append(" }");
                }
                sb.Append(buttons.Count > 0 ? "\n  ],\n" : "],\n");
                sb.Append("  \"shop\": ");
                if (shop == null) sb.Append("null\n");
                else
                {
                    sb.Append("{\n    \"open\": true,\n    \"coins\": ").Append(_level.coins).Append(",\n    \"slots\": [");
                    for (int i = 0; i < shop.Count; i++)
                    {
                        var sl = shop[i];
                        sb.Append(i == 0 ? "\n" : ",\n");
                        sb.Append("      { \"slot\": ").Append(Str(sl.Slot))
                          .Append(", \"id\": ").Append(Str(sl.Key))
                          .Append(", \"cost\": ").Append(sl.Cost)
                          .Append(", \"glass\": ").Append(sl.Glass ? "true" : "false")
                          .Append(", \"legendary\": ").Append(sl.Legendary ? "true" : "false")
                          .Append(", \"owned\": ").Append(sl.Owned ? "true" : "false")
                          .Append(", \"active\": ").Append(sl.Active ? "true" : "false")
                          .Append(", \"opened\": ").Append(sl.Opened ? "true" : "false")
                          .Append(", \"x\": ").Append(sl.X.ToString("0.0", CultureInfo.InvariantCulture))
                          .Append(", \"y\": ").Append(sl.Y.ToString("0.0", CultureInfo.InvariantCulture)).Append(" }");
                    }
                    sb.Append(shop.Count > 0 ? "\n    ]\n  }" : "]\n  }");
                }
                sb.Append(",\n  \"passivePicker\": ");
                if (passives.Count == 0) sb.Append("null\n");
                else
                {
                    sb.Append("{\n    \"open\": true,\n    \"offers\": [");
                    for (int i = 0; i < passives.Count; i++)
                    {
                        var o = passives[i];
                        sb.Append(i == 0 ? "\n" : ",\n");
                        sb.Append("      { \"id\": ").Append(o.Id)
                          .Append(", \"name\": ").Append(Str(o.Name))
                          .Append(", \"description\": ").Append(Str(o.Description))
                          .Append(", \"x\": ").Append(o.X.ToString("0.0", CultureInfo.InvariantCulture)).Append(" }");
                    }
                    sb.Append("\n    ]\n  }\n");
                }
                sb.Append("}\n");

                WriteAtomic(_statePath, sb.ToString());
            }
            catch (Exception e)
            {
                Report("WriteJson", e);
            }
        }

        // ---- Redacted overlay (challenge 9). IMGUI boxes and labels only: no game objects, no clicks, no game writes. ----

        const int Redacted = 9;
        const float OverlayRefresh = 0.1f;
        bool _overlayOn;                 // F9 toggles; set true when the challenge becomes 9
        int _lastChallenge = -1;
        float _nextOverlayRead;
        List<ButtonInfo> _overlayButtons = new List<ButtonInfo>();
        string _overlayHeader = "";
        int _overlayWidth;               // CalcSize.size_x, 0 when unreadable
        CalcSize _calcSize;
        StartLvl _calcSizeLevel;
        readonly Dictionary<string, string> _symbols = new Dictionary<string, string>();

        // Equals key: never moves, 4th row, 3rd column of the starting calculator (grid column 4, row 3).
        const int EqualsCol = 4, EqualsRow = 3;

        void UpdateOverlay()
        {
            if (Input.GetKeyDown(KeyCode.F9)) _overlayOn = !_overlayOn;
            int challenge = Challenge();
            if (challenge != _lastChallenge)
            {
                _lastChallenge = challenge;
                if (challenge == Redacted) _overlayOn = true;
            }
            if (!_overlayOn) { _passiveOffers.Clear(); return; }
            if (Time.unscaledTime < _nextOverlayRead) return;
            _nextOverlayRead = Time.unscaledTime + OverlayRefresh;
            if (_level == null || _calc == null) { _overlayButtons.Clear(); _shopSlots.Clear(); _passiveOffers.Clear(); return; }
            _overlayButtons = ReadButtons();
            Safe("Shop", () =>
            {
                _shopSlots = ShopOpen() ? ReadShop(_overlayButtons) : new List<ShopSlot>();
                _shopCoins = _level.coins;
            });
            Safe("Passives", () => _passiveOffers = ReadPassivePicker());
            _overlayLeft = _calc.total;
            _overlayHeader = _overlayLeft + " → " + _level.number;
            _overlayWidth = CalcWidth();
        }

        // ---- Shop overlay. Reads public fields; the only game call is StartLvl.GetAlchemyButtonKey (read-only). ----

        struct ShopSlot
        {
            public string Slot;       // GameObject name: "item1", "item1 (3)", ..., "box"
            public string Key;        // button id; null for the closed box or an empty slot
            public int Cost;
            public bool Glass, Legendary, Owned, Active;
            public bool IsBox;
            public int OwnedUses;     // owned button's CountUses.uses; meaningful only when Owned
            public bool Opened;       // box slot showing open_box's revealed offer
            public float X, Y;        // anchoredPosition under vitrina/items
        }

        // StartLvl.shops[0] (vitrina) sits at local y 221 when shown and 1320 when hidden.
        const float ShopShownBelowY = 770f;
        const int BoxCost = 3;        // BuyBox.Buy takes 3 coins
        List<ShopSlot> _shopSlots = new List<ShopSlot>();   // empty when the shop is closed
        int _shopCoins;

        bool ShopOpen()
        {
            var shops = _level.shops;
            return shops != null && shops.Length > 0 && shops[0] != null
                && shops[0].transform.localPosition.y < ShopShownBelowY;
        }

        /// <summary>The shop for Poll: null when closed or unreadable (logged once).</summary>
        List<ShopSlot> ShopNow(List<ButtonInfo> owned)
        {
            try { return ShopOpen() ? ReadShop(owned) : null; }
            catch (Exception e) { Report("Shop", e); return null; }
        }

        static void Pos(GameObject go, ref ShopSlot slot)
        {
            var rt = go.transform as RectTransform;
            if (rt == null) return;
            slot.X = rt.anchoredPosition.x;
            slot.Y = rt.anchoredPosition.y;
        }

        List<ShopSlot> ReadShop(List<ButtonInfo> owned)
        {
            var list = new List<ShopSlot>();
            var ownedKeys = new Dictionary<string, int>();
            foreach (var b in owned) if (!ownedKeys.ContainsKey(b.Key)) ownedKeys.Add(b.Key, b.Uses);
            foreach (var go in _level.items ?? new GameObject[0])
            {
                if (go == null) continue;
                var r = go.GetComponent<Randomisator>();
                if (r == null) continue;                       // items[6] is "Next"
                list.Add(Offer(go, r, go.name, ownedKeys));
            }
            var box = _level.box;
            if (box != null)
            {
                var slot = new ShopSlot { Slot = box.name, Cost = BoxCost, IsBox = true, Active = box.activeInHierarchy };
                Pos(box, ref slot);
                if (!slot.Active)
                {
                    // Box bought: show the revealed offer while open_box is up. Never read it while closed (stale roll).
                    var bb = box.GetComponent<BuyBox>();
                    var r = bb != null && bb.toOpen != null && bb.toOpen.activeInHierarchy
                        ? bb.toOpen.GetComponentInChildren<Randomisator>(false) : null;
                    if (r != null)
                    {
                        var o = Offer(r.gameObject, r, box.name, ownedKeys);
                        o.X = slot.X; o.Y = slot.Y; o.Opened = true; o.IsBox = true;
                        slot = o;
                    }
                }
                list.Add(slot);
            }
            return list;
        }

        // An inactive offer was bought; its fields are stale, so nothing is read from it.
        ShopSlot Offer(GameObject go, Randomisator r, string slotName, Dictionary<string, int> ownedKeys)
        {
            var s = new ShopSlot { Slot = slotName, Active = go.activeInHierarchy };
            Pos(go, ref s);
            if (!s.Active) return s;
            try
            {
                string key = _level.GetAlchemyButtonKey(go);
                s.Key = string.IsNullOrEmpty(key) ? null : key;
            }
            catch (Exception e) { Report("Shop", e); }
            s.Cost = r.cost;
            s.Glass = r.glassed;
            int t = r.type;
            s.Legendary = t == 10 || t == 11 || t == 28 || t == 46 || t == 47;
            int uses = 0;
            s.Owned = s.Key != null && ownedKeys.TryGetValue(s.Key, out uses);
            if (s.Owned) s.OwnedUses = uses;
            return s;
        }

        // CalcSize.size_x on the calculator: 1 = 4 wide, 2 = 6 wide, 3 or more = 8 wide.
        int CalcWidth()
        {
            if (_calcSizeLevel != _level) { _calcSizeLevel = _level; _calcSize = null; }
            if (_calcSize == null) _calcSize = UnityEngine.Object.FindObjectOfType<CalcSize>();
            return _calcSize != null ? _calcSize.size_x : 0;
        }

        GUIStyle _boxStyle, _symbolStyle, _usesStyle, _headerStyle, _hintStyle, _ownedStyle, _passiveTitleStyle, _passiveTextStyle;
        Texture2D _white;

        void BuildStyles()
        {
            _white = new Texture2D(1, 1);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
            _white.hideFlags = HideFlags.HideAndDontSave;   // mod-owned; not part of any scene
            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = _white;
            _symbolStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                wordWrap = false,
            };
            _usesStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontSize = 11 };
            _usesStyle.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            _ownedStyle = new GUIStyle(_usesStyle) { alignment = TextAnchor.UpperLeft };
            _ownedStyle.normal.textColor = new Color(0.6f, 1f, 0.6f, 1f);
            _headerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 15, fontStyle = FontStyle.Bold };
            _headerStyle.normal.textColor = Color.white;
            _hintStyle = new GUIStyle(_headerStyle) { alignment = TextAnchor.MiddleRight, fontSize = 11, fontStyle = FontStyle.Normal };
            _hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
            _passiveTitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = PassiveTitleSize, fontStyle = FontStyle.Bold, wordWrap = true };
            _passiveTitleStyle.normal.textColor = Color.white;
            _passiveTextStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = PassiveTextSize, wordWrap = true };
            _passiveTextStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        }

        string SymbolOf(string id)
        {
            string s;
            // A shop offer of the mw key is always empty (nothing absorbed yet).
            if (!_symbols.TryGetValue(id, out s)) _symbols[id] = s = id == "number_mw" ? ButtonSymbols.Label("mw") : ButtonSymbols.Of(id);
            return s;
        }

        // The charge, quest, mw and prime keys show a live value ("c12", "q8", "m5", "p8081"), so they skip the per-id cache.
        string SymbolOf(ButtonInfo b, long left) =>
            b.Key == "operator_prime" ? PrimeSymbol(left)
            : b.Key == "number_mw" ? ButtonSymbols.Of(b.Key, b.Uses, b.Digit, left, b.Stored)
            : b.Key == "number_charge" || b.Key == "number_quest" ? ButtonSymbols.Of(b.Key, b.Uses, b.Digit) : SymbolOf(b.Key);

        long _primeFor = long.MinValue, _prime, _overlayLeft;

        // OnGUI draws several times per frame; find the prime once per left value.
        string PrimeSymbol(long left)
        {
            if (left != _primeFor) { _prime = ButtonSymbols.NearestPrime(left); _primeFor = left; }
            return "p" + _prime;
        }

        static int SymbolFontSize(string s) => s.Length <= 4 ? 22 : s.Length <= 7 ? 14 : s.Length <= 9 ? 11 : 9;

        /// <summary>Call from MelonMod.OnGUI. Draws the overlay calculator in the bottom-left corner.</summary>
        public void OnGUI()
        {
            Safe("OnGUI", DrawOverlay);
        }

        // 1080p units: cell 64 x 48, gap 4, padding 8, header 24.
        const float CellWidth = 64f, CellHeight = 48f, CellGap = 4f, PanelPad = 8f, PanelHead = 24f;

        void DrawOverlay()
        {
            // OnGUI runs for layout, input and repaint events; only repaint draws (nothing here takes input).
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            if (!_overlayOn) return;
            bool shop = _shopSlots.Count > 0;
            if (_overlayButtons.Count == 0 && !shop && _passiveOffers.Count == 0) return;
            if (_boxStyle == null) BuildStyles();

            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            var oldBackground = GUI.backgroundColor;
            try
            {
                float s = Screen.height / 1080f;
                // Without a calculator panel the shop panel takes its place at the bottom-left corner.
                var calcPanel = new Rect(16f - 8f, 0f, 0f, 0f);
                bool drawn = false;
                if (_overlayButtons.Count > 0) drawn = DrawCalculator(ref s, out calcPanel);
                if (!drawn)
                {
                    s = Screen.height / 1080f;
                    calcPanel = new Rect(16f - 8f, Screen.height / s - 16f, 0f, 0f);
                }
                if (shop)
                {
                    GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
                    DrawShop(calcPanel);
                }
                DrawPassives();
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.backgroundColor = oldBackground;
            }
        }

        /// <summary>Draws the calculator panel. panel is its rect in scaled GUI units; s is the scale used (it may shrink to fit).</summary>
        bool DrawCalculator(ref float s, out Rect panel)
        {
            panel = default(Rect);
            // Bounds: columns from CalcSize, else from the used columns; rows 0..max(3, largest used row).
            int minCol, maxCol;
            if (_overlayWidth == 1) { minCol = 2; maxCol = 5; }
            else if (_overlayWidth == 2) { minCol = 1; maxCol = 6; }
            else if (_overlayWidth >= 3) { minCol = 0; maxCol = 7; }
            else { minCol = EqualsCol; maxCol = EqualsCol; }
            int maxRow = EqualsRow;
            var cells = new Dictionary<int, ButtonInfo>();
            foreach (var b in _overlayButtons)
            {
                if (b.Col < 0 || b.Row < 0) continue;
                if (_overlayWidth <= 0) { minCol = Mathf.Min(minCol, b.Col); maxCol = Mathf.Max(maxCol, b.Col); }
                maxRow = Mathf.Max(maxRow, b.Row);
                int key = b.Row * 100 + b.Col;
                if (cells.ContainsKey(key)) Report("Overlay", new InvalidOperationException("two buttons at " + b.Col + "," + b.Row));
                cells[key] = b;   // the later one wins
            }
            if (maxCol < minCol) return false;
            int nCols = maxCol - minCol + 1, nRows = maxRow + 1;

            const float cw = CellWidth, ch = CellHeight, gap = CellGap, pad = PanelPad, head = PanelHead;
            float width = pad * 2 + nCols * cw + (nCols - 1) * gap;
            float height = pad * 2 + head + nRows * ch + (nRows - 1) * gap;
            s = Mathf.Min(s, 0.5f * Screen.height / height);
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float x0 = 16f, y0 = Screen.height / s - 16f - height;

            GUI.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.88f);
            GUI.Box(new Rect(x0, y0, width, height), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(x0 + pad, y0 + pad, width - pad * 2, head), _overlayHeader, _headerStyle);
            GUI.Label(new Rect(x0 + pad, y0 + pad, width - pad * 2, head), "F9 hide", _hintStyle);

            float gx = x0 + pad, gy = y0 + pad + head;
            for (int row = 0; row < nRows; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    var rect = new Rect(gx + (col - minCol) * (cw + gap), gy + row * (ch + gap), cw, ch);
                    ButtonInfo b;
                    if (cells.TryGetValue(row * 100 + col, out b))
                        DrawCell(rect, SymbolOf(b, _overlayLeft), b.Uses.ToString(), b.Glass, b.Uses == 0);
                    else if (col == EqualsCol && row == EqualsRow)
                        DrawCell(rect, "=", null, false, false);
                }
            }
            panel = new Rect(x0, y0, width, height);
            return true;
        }

        static readonly Color LegendaryGold = new Color(0.95f, 0.75f, 0.2f, 1f);

        /// <summary>Shop panel right of the calculator panel (8 px gap), bottom edges aligned.</summary>
        void DrawShop(Rect calcPanel)
        {
            // Rows: larger anchoredPosition.y is higher; slots in a row go by x.
            var slots = new List<ShopSlot>(_shopSlots);
            slots.Sort((a, b) =>
            {
                int ra = Mathf.RoundToInt(a.Y * 10f), rb = Mathf.RoundToInt(b.Y * 10f);
                return ra != rb ? rb.CompareTo(ra) : a.X.CompareTo(b.X);
            });
            var rows = new List<List<ShopSlot>>();
            int lastRow = int.MinValue;
            foreach (var sl in slots)
            {
                int r = Mathf.RoundToInt(sl.Y * 10f);
                if (rows.Count == 0 || r != lastRow) { rows.Add(new List<ShopSlot>()); lastRow = r; }
                rows[rows.Count - 1].Add(sl);
            }
            int nCols = 4;
            foreach (var row in rows) nCols = Mathf.Max(nCols, row.Count);

            const float cw = CellWidth, ch = CellHeight, gap = CellGap, pad = PanelPad, head = PanelHead;
            float width = pad * 2 + nCols * cw + (nCols - 1) * gap;
            float height = pad * 2 + head + rows.Count * ch + (rows.Count - 1) * gap;
            float x0 = calcPanel.xMax + 8f, y0 = calcPanel.yMax - height;

            GUI.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.88f);
            GUI.Box(new Rect(x0, y0, width, height), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(x0 + pad, y0 + pad, width - pad * 2, head), "Shop", _headerStyle);
            GUI.Label(new Rect(x0 + pad, y0 + pad, width - pad * 2, head), "$" + _shopCoins, _hintStyle);

            float gx = x0 + pad, gy = y0 + pad + head;
            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                float start = gx + (nCols - row.Count) * (cw + gap) / 2f;
                for (int c = 0; c < row.Count; c++)
                    DrawShopCell(new Rect(start + c * (cw + gap), gy + r * (ch + gap), cw, ch), row[c]);
            }
        }

        void DrawShopCell(Rect rect, ShopSlot sl)
        {
            if (sl.IsBox && sl.Active && !sl.Opened) DrawCell(rect, "?", "$" + sl.Cost, false, false);
            else if (sl.Key == null) DrawCell(rect, "", null, false, true);
            else if (sl.Opened) DrawCell(rect, SymbolOf(sl.Key), null, sl.Glass, false);
            else DrawCell(rect, SymbolOf(sl.Key), sl.Cost == 0 ? "free" : "$" + sl.Cost, sl.Glass, false);

            if (sl.Key == null) return;
            if (sl.Opened) DrawFrame(rect, Color.white);
            else if (sl.Legendary) DrawFrame(rect, LegendaryGold);
            if (sl.Owned) GUI.Label(new Rect(rect.x + 3f, rect.y + 1f, rect.width - 3f, rect.height - 1f), (sl.OwnedUses + 1).ToString(), _ownedStyle);
        }

        // 2 px frame from four thin boxes; reuses the 1x1 white texture.
        void DrawFrame(Rect r, Color color)
        {
            GUI.backgroundColor = color;
            GUI.Box(new Rect(r.x, r.y, r.width, 2f), GUIContent.none, _boxStyle);
            GUI.Box(new Rect(r.x, r.yMax - 2f, r.width, 2f), GUIContent.none, _boxStyle);
            GUI.Box(new Rect(r.x, r.y, 2f, r.height), GUIContent.none, _boxStyle);
            GUI.Box(new Rect(r.xMax - 2f, r.y, 2f, r.height), GUIContent.none, _boxStyle);
        }

        void DrawCell(Rect rect, string symbol, string uses, bool glass, bool empty)
        {
            GUI.backgroundColor = empty ? new Color(0.2f, 0.2f, 0.22f, 0.9f)
                : glass ? new Color(0.45f, 0.7f, 0.95f, 0.95f)
                : new Color(0.45f, 0.45f, 0.5f, 0.95f);
            GUI.Box(rect, GUIContent.none, _boxStyle);
            _symbolStyle.fontSize = SymbolFontSize(symbol);
            _symbolStyle.normal.textColor = empty ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            GUI.Label(rect, symbol, _symbolStyle);
            if (uses != null)
            {
                var corner = new Rect(rect.x, rect.y, rect.width - 3f, rect.height - 1f);
                GUI.Label(corner, uses, _usesStyle);
            }
        }

        static string Str(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        // ---- Passive picker (challenge 9 only). The game shows Latin button texts there; this draws the real passive texts. Read-only. ----

        // Size: first values for playtesting (Test Z7).
        const float PassiveWidthShare = 0.75f;   // panel width / screen width
        const float PassiveHeightShare = 0.25f;  // max panel height / screen height
        const float PassiveTop = 16f;
        const int PassiveTitleSize = 18, PassiveTextSize = 14;
        const int PassiveTitleMin = 12, PassiveTextMin = 10;

        struct PassiveOffer
        {
            public int Id;                   // RandomisatorPassives.id (0-based passive index)
            public string Name, Description; // Description null when the index has no text
            public float X;                  // transform.position.x, for left-to-right order
        }

        List<PassiveOffer> _passiveOffers = new List<PassiveOffer>();   // empty when the panel doesn't show
        TextsLoader _texts;
        string _textsLang;
        List<(string Name, string Description)> _passiveTexts = new List<(string Name, string Description)>();

        /// <summary>The offered passives left to right, with their real texts. Empty unless the picker is open in challenge 9.</summary>
        List<PassiveOffer> ReadPassivePicker()
        {
            var list = new List<PassiveOffer>();
            if (Challenge() != Redacted || _level == null || _level.passiveshop == null || !_level.passiveshop.activeInHierarchy) return list;
            var pool = _level.passiveshop.GetComponent<PassivePool>();
            if (pool == null || pool.passives == null) return list;
            foreach (var p in pool.passives)
            {
                if (p == null || !p.gameObject.activeInHierarchy) continue;
                string name, desc;
                PassiveText(p.id, out name, out desc);
                list.Add(new PassiveOffer { Id = p.id, Name = name, Description = desc, X = p.transform.position.x });
            }
            list.Sort((a, b) => a.X.CompareTo(b.X));
            return list;
        }

        void PassiveText(int id, out string name, out string desc)
        {
            string lang = GamePrefs.GetString("lang");
            if (_texts == null || lang != _textsLang)
            {
                var go = GameObject.Find("TextsManager");
                _texts = go != null ? go.GetComponent<TextsLoader>() : null;
                var asset = _texts == null ? null : lang == "rus" ? _texts.textAsset_p : _texts.textAsset_eng_p;
                _passiveTexts = PassiveTexts.Parse(asset != null ? asset.text : null);
                _textsLang = _texts != null ? lang : null;   // no loader yet: look again next time
            }
            if (id >= 0 && id < _passiveTexts.Count && !string.IsNullOrEmpty(_passiveTexts[id].Name))
            {
                name = _passiveTexts[id].Name;
                desc = _passiveTexts[id].Description;
            }
            else
            {
                name = "passive " + (id + 1);
                desc = null;
            }
        }

        /// <summary>Top-center panel with one box per offered passive. Checks challenge 9 itself, so a stale list can't show elsewhere.</summary>
        void DrawPassives()
        {
            if (Challenge() != Redacted || _passiveOffers.Count == 0) return;
            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float screenW = Screen.width / s;
            float width = screenW * PassiveWidthShare, x0 = (screenW - width) / 2f, y0 = PassiveTop;
            int n = _passiveOffers.Count;
            const float pad = PanelPad, gap = CellGap, head = PanelHead, inner = 6f, titleGap = 4f;
            float boxW = (width - 2 * pad - (n - 1) * gap) / n;
            float maxH = 1080f * PassiveHeightShare;

            int titleSize = PassiveTitleSize, textSize = PassiveTextSize;
            float boxH;
            while (true)
            {
                _passiveTitleStyle.fontSize = titleSize;
                _passiveTextStyle.fontSize = textSize;
                boxH = 0f;
                foreach (var o in _passiveOffers)
                {
                    float h = inner * 2 + _passiveTitleStyle.CalcHeight(new GUIContent(o.Name), boxW - 2 * inner) + titleGap
                        + (string.IsNullOrEmpty(o.Description) ? 0f : _passiveTextStyle.CalcHeight(new GUIContent(o.Description), boxW - 2 * inner));
                    boxH = Mathf.Max(boxH, h);
                }
                if (2 * pad + head + boxH <= maxH || (titleSize <= PassiveTitleMin && textSize <= PassiveTextMin)) break;
                titleSize = Mathf.Max(PassiveTitleMin, titleSize - 1);
                textSize = Mathf.Max(PassiveTextMin, textSize - 1);
            }
            boxH = Mathf.Min(boxH, maxH - 2 * pad - head);
            float height = 2 * pad + head + boxH;

            GUI.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.88f);
            GUI.Box(new Rect(x0, y0, width, height), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(x0 + pad, y0 + pad, width - pad * 2, head), "Passives", _headerStyle);
            for (int i = 0; i < n; i++)
            {
                var o = _passiveOffers[i];
                float bx = x0 + pad + i * (boxW + gap), by = y0 + pad + head;
                GUI.backgroundColor = new Color(0.2f, 0.2f, 0.26f, 0.9f);
                GUI.Box(new Rect(bx, by, boxW, boxH), GUIContent.none, _boxStyle);
                GUI.BeginGroup(new Rect(bx, by, boxW, boxH));   // clips what doesn't fit under the height cap
                float th = _passiveTitleStyle.CalcHeight(new GUIContent(o.Name), boxW - 2 * inner);
                GUI.Label(new Rect(inner, inner, boxW - 2 * inner, th), o.Name, _passiveTitleStyle);
                if (!string.IsNullOrEmpty(o.Description))
                    GUI.Label(new Rect(inner, inner + th + titleGap, boxW - 2 * inner, boxH), o.Description, _passiveTextStyle);
                GUI.EndGroup();
            }
        }

        static string PassiveNames(List<PassiveOffer> offers)
        {
            var names = new List<string>();
            foreach (var o in offers) names.Add(o.Name);
            return string.Join(", ", names.ToArray());
        }

        void Safe(string where, Action action)
        {
            try { action(); }
            catch (Exception e) { Report(where, e); }
        }

        // Log each distinct error once so a per-frame failure doesn't flood the log.
        void Report(string where, Exception e)
        {
            try
            {
                string key = where + ":" + e.GetType().FullName + ":" + e.Message;
                if (_reportedErrors.Add(key)) _error(Tag + " error in " + where + ": " + e);
            }
            catch { }
        }
    }
}
