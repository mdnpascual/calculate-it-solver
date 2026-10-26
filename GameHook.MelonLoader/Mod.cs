using System;
using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(CalcHook.Mod), "CalcHook", "0.1.0", "calcpuzzle")]
[assembly: MelonGame(null, null)]

namespace CalcHook
{
    public sealed class Mod : MelonMod
    {
        internal static CalcHookCore Core;

        public override void OnInitializeMelon()
        {
            try
            {
                Core = new CalcHookCore(s => LoggerInstance.Msg(s), s => LoggerInstance.Error(s));
                Core.OnLoaded("MelonLoader", MelonLoader.Properties.BuildInfo.Version);
                // MelonLoader auto-applies [HarmonyPatch] classes in this assembly (NextLvlPatch and WarpPatch below).
            }
            catch (Exception e)
            {
                LoggerInstance.Error(CalcHookCore.Tag + " init failed: " + e);
            }
        }

        public override void OnUpdate() => Core?.Tick();

        public override void OnGUI() => Core?.OnGUI();
    }

    // Postfix only reads state after the game has set up the new round.
    [HarmonyPatch(typeof(StartLvl), nameof(StartLvl.NextLvl))]
    static class NextLvlPatch
    {
        static void Postfix(StartLvl __instance)
        {
            try { Mod.Core?.OnRoundLoaded(__instance); }
            catch { }
        }
    }

    // Postfix only marks the puzzle pending after the warp button has rebuilt the calculator.
    [HarmonyPatch(typeof(StartLvl), nameof(StartLvl.D20))]
    static class WarpPatch
    {
        static void Postfix(StartLvl __instance)
        {
            try { Mod.Core?.OnWarp(__instance); }
            catch { }
        }
    }
}
