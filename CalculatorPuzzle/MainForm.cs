using System.Collections.Concurrent;

namespace CalculatorPuzzle;

public class MainForm : Form
{
    readonly TextBox startBox = new() { Text = "41", Width = 120 };
    readonly TextBox goalBox = new() { Text = "48", Width = 120 };
    const int MaxPresses = 12;
    const int MaxShownLines = 10;
    const int CostSlack = 2;  // later phases look up to this many presses above the best line so far

    readonly TextBox buttonsBox = new()
    {
        Text = "5X:9, X1:9, X2:9, X7:9, X4:9, 4X:9, 6X:9, 3X:9, 8X:9, 10:9, /:9, X9:9, flip:9, mod:9, =, reset:9, +:9, -:9, ×:9",
        Width = 520,
    };
    readonly TextBox requiredBox = new() { Width = 120 };
    readonly TextBox lastBox = new() { Width = 120 };
    readonly TextBox moneyBox = new() { Width = 120 };
    readonly TextBox bossBox = new() { Width = 120 };
    readonly Label divisionStatusLabel = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    readonly TextBox divisionLinesBox = new()
    {
        ReadOnly = true,
        Multiline = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
        Width = 200,   // below every other row, so the column width comes from them
        Height = 200,
    };

    // Division Shortcut search state. The task only enqueues lines; the timer reads them on the UI thread.
    CancellationTokenSource? searchCancel;
    ConcurrentQueue<ResultLine>? searchQueue;
    Task<string?>? searchTask;
    Task<(string? Error, List<ResultLine> Lines)>? moduloTask;
    Task<GoalStepSearch.Result>? stepTask;  // boss rule 14: the stepped search (all phases in one task)
    Task<AchievementDone>? achievementTask;  // School mathematics / Just a one: one task, the queue and the timer as usual
    AchievementMode searchAchievement;       // the mode of the running (or last) search
    Buttons? searchButtons;
    int arithmeticCount;
    int twoOperatorCount;
    int divisionCount;
    int moduloCount;
    readonly List<ResultLine> found = [];
    string shownLines = "";
    bool searchSameNumber;  // S = G: no lines and no error
    IReadOnlyList<SwapSearch.Leg> searchLegs = [];  // the direct leg, and the flipped one when the puzzle has swap
    int searchBossRule;
    bool searchPlusOne;     // boss rule 12: every = adds 1
    bool searchNear1;       // the near-one passive: lines may end at G - 1, G or G + 1

    // Achievements section: a flat header button that shows or hides the panel with the two radio buttons.
    readonly Button achievementsHeader = new() { Text = "▸ Achievements", AutoSize = true, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft };
    readonly FlowLayoutPanel achievementsPanel = new() { AutoSize = true, FlowDirection = FlowDirection.TopDown, Visible = false, Margin = Padding.Empty };
    readonly RadioButton schoolRadio = new() { Text = "(+, -, /, *) solve (School mathematics)", AutoSize = true, AutoCheck = false };
    readonly RadioButton justOneRadio = new() { Text = "+1 with 4 ops (Just a one)", AutoSize = true, AutoCheck = false };
    AchievementMode achievementMode;
    const int MaxSchoolPresses = 30;
    readonly System.Windows.Forms.Timer searchTimer = new() { Interval = 100 };
    int rejectedLines;
    string? searchRequired;  // Market Demand: the button every good line presses (a token), or null
    int gameWarp;   // warp count of the applied game puzzle
    IReadOnlyList<string> gameBlocked = [];   // blocked tokens of the applied game puzzle
    int gameDiffType;                         // diff_type of the applied game puzzle (money drains from the third press at 7 or more)
    IReadOnlyList<string> gameCoinPassives = [];   // coin passives of the applied game puzzle
    string? requiredNote;    // why Required was not used, shown in the status
    string? gameRequiredKind;   // "market" / "boss" from the last puzzle.json, with the token it filled into Required
    string? gameRequiredToken;
    bool searchBoss;            // boss rule 11: only lines that press searchRequired count
    RequiredPlan searchPlan = RequiredPlan.For(null, false);

    // Game feed. Watcher events only restart the timer; the timer reads the file once things go quiet.
    readonly CheckBox followBox = new() { Text = "Follow game", Checked = true, AutoSize = true };
    readonly CheckBox lockBox = new() { Text = "Lock", AutoSize = true };
    readonly CheckBox near1Box = new() { Text = "Near 1", AutoSize = true };
    readonly System.Windows.Forms.Timer fileTimer = new() { Interval = 100 };
    FileSystemWatcher? watcher;
    int lastSeq = -1;
    string lastTime = "";
    int appliedStepWarp;    // warp count of that puzzle
    bool appliedStepNear1;  // near-one passive of that puzzle
    int? appliedStepRound;  // round of the last applied rule-14 puzzle (the goal moves while the line is pressed)

    public MainForm()
    {
        Text = "Calculator Puzzle";
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 11f);
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Escape || (searchTask == null && moduloTask == null && stepTask == null && achievementTask == null))
                return;
            e.SuppressKeyPress = true;
            StopDivisionSearch(keepLines: true);
            SetStatus($"Stopped. {found.Count} line(s) found.");
        };
        searchTimer.Tick += (_, _) => OnSearchTick();
        fileTimer.Tick += (_, _) => OnGameFileChanged();
        followBox.CheckedChanged += (_, _) => SetFollowing(followBox.Checked);
        lockBox.CheckedChanged += (_, _) =>
        {
            // Unlocking reads the file again, even if the puzzle was handled before.
            if (lockBox.Checked || !followBox.Checked)
                return;
            lastSeq = -1;
            lastTime = "";
            appliedStepRound = null;
            fileTimer.Stop();
            fileTimer.Start();
        };

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(12),
        };
        AddInlineRow(layout, "Start (S)", startBox, ("Goal (G)", goalBox));
        var followPanel = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty, WrapContents = false };
        followPanel.Controls.AddRange([followBox, lockBox, near1Box]);
        AddRow(layout, "Game", followPanel);
        AddInlineRow(layout, "Required", requiredBox, ("Last", lastBox), ("Money", moneyBox), ("Boss", bossBox));
        AddRow(layout, "Buttons", buttonsBox);
        achievementsHeader.FlatAppearance.BorderSize = 0;
        achievementsHeader.Click += (_, _) =>
        {
            achievementsPanel.Visible = !achievementsPanel.Visible;
            UpdateAchievementsHeader();
        };
        // AutoCheck is off so a click on the checked button can turn it off.
        schoolRadio.Click += (_, _) => SetAchievementMode(AchievementMode.School);
        justOneRadio.Click += (_, _) => SetAchievementMode(AchievementMode.JustOne);
        achievementsPanel.Controls.AddRange([schoolRadio, justOneRadio]);
        var achievementsColumn = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = Padding.Empty, WrapContents = false };
        achievementsColumn.Controls.AddRange([achievementsHeader, achievementsPanel]);
        AddRow(layout, "", achievementsColumn);
        AddRow(layout, "", divisionStatusLabel);
        AddRow(layout, "Lines", divisionLinesBox);
        Controls.Add(layout);

        startBox.TextChanged += (_, _) => MarkDivisionStale();
        goalBox.TextChanged += (_, _) => MarkDivisionStale();
        buttonsBox.TextChanged += (_, _) => MarkDivisionStale();
        requiredBox.TextChanged += (_, _) => MarkDivisionStale();
        lastBox.TextChanged += (_, _) => MarkDivisionStale();
        moneyBox.TextChanged += (_, _) => MarkDivisionStale();
        bossBox.TextChanged += (_, _) => MarkDivisionStale();
        near1Box.CheckedChanged += (_, _) => MarkDivisionStale();
        // The Division Shortcut search is slow, so it only runs when Enter is pressed.
        foreach (var box in new[] { startBox, goalBox, requiredBox, lastBox, moneyBox, bossBox, buttonsBox })
            box.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter)
                    return;
                e.SuppressKeyPress = true;
                StartDivisionSearch();
            };
        SetFollowing(true);

        MarkDivisionStale();
    }

    static void AddRow(TableLayoutPanel layout, string caption, Control control)
    {
        layout.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left });
        layout.Controls.Add(control);
    }

    // One row: the first caption in the label column, then each box followed by the next caption, all on one line.
    static void AddInlineRow(TableLayoutPanel layout, string caption, TextBox first, params (string Caption, TextBox Box)[] more)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty, WrapContents = false };
        panel.Controls.Add(first);
        foreach (var (text, box) in more)
        {
            panel.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 6, 3, 0) });
            panel.Controls.Add(box);
        }
        AddRow(layout, caption, panel);
    }

    static string ModeName(AchievementMode mode) => mode == AchievementMode.School ? "School mathematics" : "Just a one";

    void UpdateAchievementsHeader() =>
        achievementsHeader.Text = (achievementsPanel.Visible ? "▾" : "▸") + " Achievements"
            + (achievementMode != AchievementMode.None && !achievementsPanel.Visible ? ": " + ModeName(achievementMode) : "");

    /// <summary>A click on a radio button: select its mode, or turn it off when it is already on. Restarts the search at once.</summary>
    void SetAchievementMode(AchievementMode clicked)
    {
        achievementMode = clicked == achievementMode ? AchievementMode.None : clicked;
        schoolRadio.Checked = achievementMode == AchievementMode.School;
        justOneRadio.Checked = achievementMode == AchievementMode.JustOne;
        UpdateAchievementsHeader();
        StartDivisionSearch();
    }

    void MarkDivisionStale() => SetStatus("Press Enter to recalculate.");

    void SetStatus(string text) => divisionStatusLabel.Text = text;

    string NoLineStatus() => "No line fits the available buttons" + (rejectedLines > 0 ? " within the charges." : ".");

    // ---- Game feed ---------------------------------------------------------------------------

    void SetFollowing(bool on)
    {
        watcher?.Dispose();
        watcher = null;
        fileTimer.Stop();
        if (!on)
            return;
        try
        {
            var folder = Path.GetDirectoryName(GameLink.DefaultPath)!;
            Directory.CreateDirectory(folder);
            watcher = new FileSystemWatcher(folder, Path.GetFileName(GameLink.DefaultPath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };
            void Poke(object? _, FileSystemEventArgs __)
            {
                if (IsHandleCreated)
                    BeginInvoke(() => { fileTimer.Stop(); fileTimer.Start(); });
            }
            watcher.Changed += Poke;
            watcher.Created += Poke;
            watcher.Renamed += Poke;
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
        {
            followBox.Checked = false;
            SetStatus("Can't watch the game folder: " + e.Message);
            return;
        }
        // Re-checking the box reads the current file once, even if it was handled before.
        lastSeq = -1;
        lastTime = "";
        appliedStepRound = null;
        fileTimer.Start();
    }

    void OnGameFileChanged()
    {
        fileTimer.Stop();
        if (lockBox.Checked)
            return;  // unchecking Lock reads the file again
        string? json = null;
        for (int attempt = 0; attempt < 3 && json == null; attempt++)
        {
            try
            {
                using var stream = new FileStream(GameLink.DefaultPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                json = reader.ReadToEnd();
            }
            catch (FileNotFoundException) { return; }
            catch (DirectoryNotFoundException) { return; }
            catch (IOException) { Thread.Sleep(30); }
        }
        if (json == null || GameLink.Parse(json) is not { } puzzle)
            return;
        if (puzzle.Seq == lastSeq && puzzle.Time == lastTime)
            return;
        lastSeq = puzzle.Seq;
        lastTime = puzzle.Time;
        // Rule 14 raises the goal on every press; the round's first puzzle is the one to solve.
        bool stepRule = puzzle.BossRule == GoalStepSearch.GoalPerChargeRule;
        if (stepRule && appliedStepRound == puzzle.Round && appliedStepWarp == puzzle.Warp && appliedStepNear1 == puzzle.Near1)
            return;
        appliedStepRound = stepRule ? puzzle.Round : null;
        appliedStepWarp = puzzle.Warp;
        appliedStepNear1 = puzzle.Near1;
        ApplyGamePuzzle(puzzle);
    }

    void ApplyGamePuzzle(GamePuzzle puzzle)
    {
        startBox.Text = puzzle.Start.ToString();
        goalBox.Text = puzzle.Goal.ToString();
        buttonsBox.Text = puzzle.ButtonsText;
        gameBlocked = puzzle.Blocked ?? [];
        gameDiffType = puzzle.DiffType;
        gameCoinPassives = puzzle.CoinPassives ?? [];
        gameWarp = puzzle.Warp;
        requiredBox.Text = ButtonLabels.Label(puzzle.Required ?? "");
        gameRequiredKind = puzzle.RequiredKind;
        gameRequiredToken = puzzle.Required;
        lastBox.Text = puzzle.Last?.ToString() ?? "";
        moneyBox.Text = puzzle.Coins?.ToString() ?? "";
        bossBox.Text = puzzle.BossRule == 0 ? "" : puzzle.BossRule.ToString();
        near1Box.Checked = puzzle.Near1;
        StartDivisionSearch();
    }

    void StartDivisionSearch()
    {
        StopDivisionSearch(keepLines: false);

        if (!long.TryParse(startBox.Text, out var start) || start < 0
            || !long.TryParse(goalBox.Text, out var goal) || goal < 0)
        {
            SetStatus("Enter non-negative whole numbers for S and G.");
            return;
        }

        // Last, Money and Boss: empty or bad means 0.
        long last = long.TryParse(lastBox.Text.Trim(), out var lastValue) && lastValue >= 0 ? lastValue : 0;
        long money = long.TryParse(moneyBox.Text.Trim(), out var moneyValue) && moneyValue >= 0 ? moneyValue : 0;
        int bossRule = int.TryParse(bossBox.Text.Trim(), out var bossValue) && bossValue is >= 0 and <= 99 ? bossValue : 0;

        var (buttons, parseError) = Buttons.Parse(buttonsBox.Text, last, goal, money);
        if (buttons == null)
        {
            SetStatus(parseError ?? "");
            return;
        }

        // Rule 6: every press costs 2 charges, so the search counts presses (ceil(n / 2)), not charges.
        if (bossRule == EngineeringCalculator.TakeTwoRule)
            buttons = buttons.TakeTwo();
        // Lines run with the goal and money the game has at each press (near, median, money read them).
        buttons = buttons.WithContext(new LineContext(goal, bossRule == GoalStepSearch.GoalPerChargeRule, money,
            new MoneyRules(gameDiffType >= 7, bossRule == 10, gameCoinPassives)));

        // Required must be a button the puzzle has with charges left; otherwise the search runs without it.
        string? required = null;
        requiredNote = null;
        var typed = requiredBox.Text.Trim();
        if (typed.Length > 0)
        {
            var requiredToken = Buttons.Token(typed);
            if (requiredToken == null)
                requiredNote = $"no line can use {typed}";
            else if (buttons.Charges.GetValueOrDefault(requiredToken) <= 0)
                requiredNote = $"{ButtonLabels.Label(requiredToken)} isn't in Buttons";
            else
                required = requiredToken;
        }
        searchRequired = required;
        // Boss mode needs a usable token that Follow game filled in; a hand edit of the box turns it off.
        searchBoss = required != null && gameRequiredKind == "boss" && ButtonLabels.Token(typed) == (gameRequiredToken ?? "");
        var plan = searchPlan = RequiredPlan.For(required, searchBoss);
        if (bossRule == EngineeringCalculator.RerollOnMissRule)
            plan = searchPlan = plan.OneSend();
        var legs = searchLegs = SwapSearch.Legs(start, goal, buttons, plan, required, searchBoss, bossRule);
        searchBossRule = bossRule;
        if (gameWarp > 0)
            requiredNote = (requiredNote == null ? "" : requiredNote + ", ") + $"warp {gameWarp}";
        // A token the user gave charges back is no longer blocked.
        var blockedNote = gameBlocked.Where(t => buttons.Charges.GetValueOrDefault(t) <= 0).ToList();
        if (blockedNote.Count > 0)
            requiredNote = (requiredNote == null ? "" : requiredNote + ", ") + $"blocked: {string.Join(", ", blockedNote.Select(t => ButtonLabels.Label(t).Replace('÷', '/')))}";

        // The task gets its own token and queue, so a stopped search's late lines are never read.
        var cancel = new CancellationTokenSource();
        var queue = new ConcurrentQueue<ResultLine>();
        searchCancel = cancel;
        bool plusOne = searchPlusOne = bossRule == EngineeringCalculator.SendPlusOneRule;
        searchSameNumber = start == goal && !plusOne;
        bool near1 = searchNear1 = near1Box.Checked;
        searchButtons = buttons;
        arithmeticCount = 0;
        twoOperatorCount = 0;
        divisionCount = 0;
        searchQueue = queue;
        var token = cancel.Token;
        rejectedLines = 0;
        moduloCount = 0;
        searchAchievement = achievementMode;
        if (achievementMode != AchievementMode.None)
        {
            // The modes look for their own lines: Required and the boss rules 11-14 don't apply.
            if (bossRule is >= 11 and <= 14)
                requiredNote = (requiredNote == null ? "" : requiredNote + ", ") + $"achievement mode ignores boss rule {bossRule}";
            if (typed.Length > 0)
                requiredNote = (requiredNote == null ? "" : requiredNote + ", ") + "achievement mode ignores Required";
            searchBoss = searchPlusOne = searchNear1 = false;
            searchRequired = null;
            searchSameNumber = false;
            Action rejected = () => Interlocked.Increment(ref rejectedLines);
            achievementTask = achievementMode == AchievementMode.School
                ? Task.Run(() =>
                {
                    var r = SchoolSearch.Find(start, goal, buttons, MaxSchoolPresses, token,
                        line => queue.Enqueue(ResultLine.From(line, buttons)), rejected);
                    return new AchievementDone(r.Error, []);
                }, token)
                : Task.Run(() =>
                {
                    var r = JustOneSearch.Search(start, buttons, MaxPresses, queue.Enqueue, token, rejected);
                    return new AchievementDone(r.Error, r.Credits);
                }, token);
            SetStatus($"{ModeName(achievementMode)}: searching… 0 line(s) so far. Esc to stop.");
            searchTimer.Start();
            return;
        }
        if (bossRule == GoalStepSearch.GoalPerChargeRule)
        {
            stepTask = Task.Run(() =>
            {
                Action onRejected = () => Interlocked.Increment(ref rejectedLines);
                GoalStepSearch.Result? total = null;
                int kept = 0;
                // Direct leg first, then the flipped one (F → S, one press fewer), whose lines get swap in front.
                foreach (var leg in legs)
                {
                    Action<ResultLine> onLine = leg.Flipped
                        ? line =>
                        {
                            if (SwapSearch.Wrap(line, start, buttons, required) is { } wrapped)
                            {
                                Interlocked.Increment(ref kept);
                                queue.Enqueue(wrapped);
                            }
                            else
                                onRejected();
                        }
                        : line =>
                        {
                            Interlocked.Increment(ref kept);
                            queue.Enqueue(line);
                        };
                    var r = GoalStepSearch.Search(leg.Start, leg.Goal, leg.Buttons, leg.Plan, required, onLine, token,
                        Math.Max(1, MaxShownLines - kept), leg.Flipped ? MaxPresses - 1 : MaxPresses, onRejected,
                        leg.Flipped ? false : near1);
                    total = total == null ? r : new GoalStepSearch.Result(total.Arithmetic + r.Arithmetic,
                        total.Division + r.Division, total.Modulo + r.Modulo, Math.Max(total.LastStep, r.LastStep),
                        total.DivisionError ?? r.DivisionError, total.TwoOperator + r.TwoOperator);
                }
                if (SwapSearch.SwapAlone(start, goal, buttons, required, bossRule) is { } alone)
                {
                    queue.Enqueue(alone);
                    total = total is { } t0 ? t0 with { Arithmetic = t0.Arithmetic + 1 } : new GoalStepSearch.Result(1, 0, 0, 0, null);
                }
                return total ?? new GoalStepSearch.Result(0, 0, 0, 0, null);
            }, token);
            SetStatus("Searching… 0 line(s) so far. Esc to stop.");
            searchTimer.Start();
            return;
        }
        searchTask = Task.Run(() =>
        {
            // Arithmetic lines are strong: division only looks up to two presses above the best of them
            // (counting only lines that press the required button, if there is one).
            // Phase-major over the legs (the direct S → G, then the flipped F → S after a swap) and over the targets
            // (G, then G - 1 and G + 1 with Near 1), so every leg's lines set the shared cap.
            // A flipped line costs one more (the swap), so its leg searches with a cap one lower.
            Action onRejected = () => Interlocked.Increment(ref rejectedLines);
            ResultLine? Tag(ResultLine r, SwapSearch.Leg leg, long t)
            {
                if (leg.Flipped)
                    return SwapSearch.Wrap(r, start, buttons, required);
                return t != goal ? r with { Reaches = t } : r;
            }
            // The cap a leg searches with, and the new shared cap after one of its lines.
            int divisionCap = MaxPresses;
            int CapFor(SwapSearch.Leg leg) => leg.Flipped ? divisionCap - 1 : divisionCap;
            void Kept(ResultLine? r)
            {
                if (r == null) { onRejected(); return; }
                queue.Enqueue(r);
                if (!r.Missing)
                    divisionCap = Math.Min(divisionCap, r.Cost + CostSlack);
            }
            IEnumerable<long> TargetsOf(SwapSearch.Leg leg) =>
                searchSameNumber ? [] : leg.Flipped ? [leg.Goal] : NearGoals.Targets(goal, near1, plusOne);

            if (SwapSearch.SwapAlone(start, goal, buttons, required, bossRule) is { } alone)
                Kept(alone);
            foreach (var leg in legs)
            {
                if (!leg.Plan.Arithmetic)
                    continue;
                foreach (var t in TargetsOf(leg))
                {
                    // S one off from G: the bare = sends S and wins.
                    bool bare = near1 && !plusOne && !leg.Flipped && t == leg.Start;
                    foreach (var line in ArithmeticSearch.Find(leg.Start, t, leg.Buttons, leg.Flipped ? MaxPresses - 1 : MaxPresses, token,
                                 accept: leg.Plan.Accept, onlyOps: leg.Plan.OnlyOps, plusOne: plusOne, bareSend: bare))
                        Kept(Tag(ResultLine.From(line, leg.Buttons, required), leg, t));
                }
            }
            // Two-operator lines look up to two presses above the best arithmetic line, and then set the division cap too.
            foreach (var leg in legs.Where(l => l.Plan.TwoOperator))
                foreach (var t in TargetsOf(leg).Where(t => plusOne || t != leg.Start))
                    foreach (var line in TwoOperatorSearch.Find(leg.Start, t, leg.Buttons, CapFor(leg), token, accept: leg.Plan.Accept,
                                 requiredOp: leg.Plan.RequiredOp, onRejected: onRejected, plusOne: plusOne))
                        Kept(Tag(ResultLine.From(line, leg.Buttons, required), leg, t));
            string? divisionError = null;
            bool divisionRan = false;
            foreach (var leg in legs.Where(l => l.Plan.Division))
            {
                divisionRan = true;
                // The input error doesn't depend on the target, so the first one ends the phase.
                foreach (var t in TargetsOf(leg).Where(t => plusOne || t != leg.Start))
                {
                    var error = DivisionShortcut.Search(leg.Start, t, leg.Buttons, CapFor(leg),
                        l =>
                        {
                            if (leg.Plan.Accept == null || leg.Plan.Accept(l.Presses))
                                Kept(Tag(ResultLine.From(l, leg.Buttons, required), leg, t));
                        }, token,
                        onRejected: _ => onRejected(), plusOne: plusOne);
                    if (error != null)
                    {
                        // Only the direct leg's error is shown; a flipped leg that can't divide just has no division lines.
                        if (!leg.Flipped)
                            divisionError = error;
                        break;
                    }
                }
            }
            return divisionRan ? divisionError : null;
        }, token);
        SetStatus("Searching… 0 line(s) so far. Esc to stop.");
        searchTimer.Start();
    }

    void OnSearchTick()
    {
        if (achievementTask != null)
        {
            OnAchievementTick();
            return;
        }
        if (stepTask != null)
        {
            OnStepTick();
            return;
        }
        if (moduloTask != null)
        {
            if (moduloTask.IsCompleted)
                OnModuloDone();
            return;
        }
        if (searchQueue == null || searchTask == null)
        {
            searchTimer.Stop();
            return;
        }

        // Read completion before draining, so lines enqueued before the task ended are all collected.
        bool done = searchTask.IsCompleted;
        Drain();
        if (!done)
        {
            SetStatus($"Searching… {found.Count} line(s) so far. Esc to stop.");
            return;
        }

        string? exception = searchTask.IsFaulted ? searchTask.Exception!.GetBaseException().Message : null;
        searchTask = null;
        searchQueue = null;
        arithmeticCount = found.Count(l => l.Arithmetic != null);
        twoOperatorCount = found.Count(l => l.TwoOperator != null);
        divisionCount = found.Count(l => l.Division != null);
        // A Division Shortcut input error (no ÷, no 10 or digit key) only means no division lines; the modulo search still runs.
        if (exception != null || searchSameNumber || !searchPlan.Modulo || searchButtons == null || searchCancel == null)
        {
            FinishSearch();
            if (exception != null)
                SetStatus(exception);
            else if (found.Count == 0 && !searchSameNumber && !searchBoss)
                SetStatus(NoLineStatus());
            else
                SetStatus(DoneStatus(0));
            return;
        }

        // The division search finished on its own: look for modulo lines up to two presses above the best.
        // Lines rejected for charges never reach found, so they don't set the cap.
        // With a required button only the lines that press it set the cap.
        var counted = found.Where(l => !l.Missing).ToList();
        int cap = counted.Count > 0 ? Math.Min(MaxPresses, counted.Min(l => l.Cost) + CostSlack) : MaxPresses;
        var buttons = searchButtons;
        var token = searchCancel.Token;
        if (!long.TryParse(startBox.Text, out var start) || !long.TryParse(goalBox.Text, out var goal))
        {
            FinishSearch();
            return;
        }
        bool plusOne = searchPlusOne, near1 = searchNear1;
        var plan = searchPlan;
        var required = searchRequired;
        var legs = searchLegs;
        moduloTask = Task.Run(() =>
        {
            string? error = null;
            var lines = new List<ResultLine>();
            foreach (var leg in legs.Where(l => l.Plan.Modulo))
            {
                // A flipped line costs one more (the swap), so its leg searches with a cap one lower.
                int legCap = leg.Flipped ? cap - 1 : cap;
                foreach (var t in searchSameNumber ? [] : leg.Flipped ? [leg.Goal] : NearGoals.Targets(goal, near1, plusOne))
                {
                    if (!plusOne && t == leg.Start)
                        continue;
                    var r = ModuloSearch.Find(leg.Start, t, leg.Buttons, legCap, token, accept: leg.Plan.Accept, plusOne: plusOne);
                    if (!leg.Flipped)
                        error ??= r.Error;
                    foreach (var line in r.Lines)
                    {
                        var rl = ResultLine.From(line, leg.Buttons, required);
                        if (leg.Flipped)
                        {
                            if (SwapSearch.Wrap(rl, start, buttons, required) is { } wrapped)
                                lines.Add(wrapped);
                        }
                        else
                            lines.Add(t != goal ? rl with { Reaches = t } : rl);
                    }
                }
            }
            return (error, lines);
        });
        SetStatus($"Modulo Shortcut… {arithmeticCount + twoOperatorCount + divisionCount} line(s). Esc to stop.");
    }

    void OnAchievementTick()
    {
        var task = achievementTask!;
        // Read completion before draining, so lines enqueued before the task ended are all collected.
        bool done = task.IsCompleted;
        Drain();
        string name = ModeName(searchAchievement);
        if (!done)
        {
            SetStatus($"{name}: searching… {found.Count} line(s) so far. Esc to stop.");
            return;
        }
        achievementTask = null;
        searchQueue = null;
        FinishSearch();
        if (task.IsFaulted)
        {
            SetStatus(task.Exception!.GetBaseException().Message);
            return;
        }
        if (!task.IsCompletedSuccessfully)
            return;
        var notes = new List<string>();
        if (rejectedLines > 0) notes.Add($"{rejectedLines} rejected");
        if (requiredNote != null) notes.Add(requiredNote);
        string tail = notes.Count > 0 ? ", " + string.Join(", ", notes) : "";
        if (found.Count == 0)
            SetStatus(NoLineStatus());
        else
            SetStatus($"{found.Count} line(s)" + tail);
    }

    void OnStepTick()
    {
        var task = stepTask!;
        // Read completion before draining, so lines enqueued before the task ended are all collected.
        bool done = task.IsCompleted;
        Drain();
        if (!done)
        {
            SetStatus($"Searching… {found.Count} line(s) so far. Esc to stop.");
            return;
        }
        stepTask = null;
        searchQueue = null;
        FinishSearch();
        if (task.IsFaulted)
        {
            SetStatus(task.Exception!.GetBaseException().Message);
            return;
        }
        if (!task.IsCompletedSuccessfully)
            return;
        var r = task.Result;
        arithmeticCount = r.Arithmetic;
        twoOperatorCount = r.TwoOperator;
        divisionCount = r.Division;
        moduloCount = r.Modulo;
        if (found.Count == 0)
            SetStatus(NoLineStatus());
        else
            SetStatus(DoneStatus(moduloCount));
    }

    void OnModuloDone()
    {
        var task = moduloTask!;
        moduloTask = null;
        int moduloCount = 0;
        if (task.IsCompletedSuccessfully)
        {
            var result = task.Result;
            moduloCount = result.Lines.Count;
            found.AddRange(result.Lines);
            Redraw();
        }
        FinishSearch();
        if (found.Count == 0 && !searchSameNumber && !searchBoss)
            SetStatus(NoLineStatus());
        else
            SetStatus(DoneStatus(moduloCount));
    }

    // Temporary (Test M, M2): a boss round with a required button always shows this, even when no two-operator line was found.
    const string BossTestMarker = ", TEST M2: boss required, report back";

    // Zero counts are left out; the M2 marker stays.
    string DoneStatus(int modulo)
    {
        var parts = new List<string>();
        if (arithmeticCount > 0) parts.Add($"{arithmeticCount} arithmetic line(s)");
        if (twoOperatorCount > 0) parts.Add($"{twoOperatorCount} two-operator line(s)");
        if (divisionCount > 0) parts.Add($"{divisionCount} division line(s)");
        if (modulo > 0) parts.Add($"{modulo} modulo line(s)");
        if (searchLegs.Any(l => l.Flipped) || found.Any(l => l.Swapped)) parts.Add($"{found.Count(l => l.Swapped)} swap line(s)");
        if (rejectedLines > 0) parts.Add($"{rejectedLines} rejected");
        if (searchRequired != null) parts.Add($"{found.Count(l => !l.Missing)} use {ButtonLabels.Label(searchRequired).Replace('÷', '/')}");
        if (requiredNote != null) parts.Add(requiredNote);
        var text = string.Join(", ", parts);
        return searchBoss ? (text.Length > 0 ? text + BossTestMarker : BossTestMarker.TrimStart(',', ' ')) : text;
    }

    void FinishSearch()
    {
        searchTimer.Stop();
        searchCancel?.Dispose();
        searchCancel = null;
    }

    /// <summary>Moves queued lines into found, re-sorts, and redraws the Lines box if the top lines changed.</summary>
    void Drain()
    {
        if (searchQueue == null)
            return;
        bool added = false;
        while (searchQueue.TryDequeue(out var line))
        {
            found.Add(line);
            added = true;
        }
        if (added)
            Redraw();
    }

    /// <summary>Sorts found and redraws the Lines box if the top lines changed.</summary>
    void Redraw()
    {
        if (searchAchievement == AchievementMode.JustOne)
            JustOneSearch.Order(found);
        else
            found.Sort(ResultLine.Ranking);
        var text = string.Join(Environment.NewLine, found.Take(MaxShownLines));
        if (text != shownLines)
        {
            shownLines = text;
            divisionLinesBox.Text = text;
        }
    }

    void StopDivisionSearch(bool keepLines)
    {
        if (keepLines && searchQueue != null)
            Drain();
        searchTimer.Stop();
        searchCancel?.Cancel();
        searchCancel?.Dispose();
        searchCancel = null;
        searchQueue = null;
        searchTask = null;
        moduloTask = null;
        stepTask = null;
        achievementTask = null;
        if (!keepLines)
        {
            found.Clear();
            shownLines = "";
            divisionLinesBox.Text = "";
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopDivisionSearch(keepLines: false);
        watcher?.Dispose();
        base.OnFormClosing(e);
    }
}
