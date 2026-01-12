using HarmonyLib;
using RimForge.Effects;
using System;
using RimForge.CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimForge
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HotSwapAllAttribute : Attribute;
    
    [HotSwapAll]
    public class Core : Mod
    {
        public static Action<Pawn, CoilgunShellDef, int> CoilgunHitPawn;
        public static Action<int, float, CoilgunShellDef> CoilgunPostFire;
        public static Action<Explosion, int> CoilgunExplosion;
        public static Action<AchievementEvent> GenericAchievementEvent = _ => { }; // Default implementation do avoid null checks elsewhere.

        public static SkillDef BlessingSkillDef { get; private set; }
        public static ModContentPack ContentPack { get; private set; }
        public static Core Instance { get; private set; }

        public enum AchievementEvent
        {
            None,
            CoilsFire,
            DroneAntimatter,
            DroneAntimatterFull,
            RitualPerformed,
            Ritual50ChanceFailure,
            RitualFailure
        }

        public static void Log(string msg)
        {
            Verse.Log.Message($"<color=#b7ff1c>[RimForge]</color> {msg ?? "<null>"}");
        }

        public static void Warn(string msg)
        {
            Verse.Log.Warning($"[RimForge] {msg ?? "<null>"}");
        }

        public static void Error(string msg, Exception exception = null)
        {
            Verse.Log.Error($"[RimForge] {msg ?? "<null>"}");
            if(exception != null)
                Verse.Log.Error(exception.ToString());
        }

        public readonly Harmony HarmonyInstance; 

        public Core(ModContentPack content) : base(content)
        {
            Log("Hello, world!");
            Instance = this;
            ContentPack = content;

            // Apply harmony patches.
            HarmonyInstance = new Harmony("co.uk.epicguru.rimforge");
            try
            {
                HarmonyInstance.PatchAll();
            }
            catch (Exception e)
            {
                Error("Failed to apply 1 or more harmony patches! Mod will not work as intended. Contact author.", e);
            }
            finally
            {
                Log($"Patched {HarmonyInstance.GetPatchedMethods().EnumerableCount()} methods:\n{string.Join(",\n", HarmonyInstance.GetPatchedMethods())}");
            }

            // Create MonoBehaviour hook.
            var go = new GameObject("RimForge hook");
            go.AddComponent<UnityHook>();

            // When the game closes, shut down this particle processing thread.
            UnityHook.UponApplicationQuit += () =>
            {
                MapEffectHandler.ThreadedHandler?.Stop();
            };

            if (CECompat.IsCEActive)
                Log("Combat Extended detected! Running in Combat Extended mode.");

            // Blessing skill def, used to make recipes that only blessed pawns can do.
            BlessingSkillDef = new SkillDef()
            {
                defName = "RF_BlessingSkill_RuntimeGenerated",
                label = "Blessing of Zir (trait)",
                skillLabel = "Blessing of Zir (trait)",
                description = "Requirement: pawn must have the Blessing of Zir trait.",
                modContentPack = Content,
                usuallyDefinedInBackstories = false,
                pawnCreatorSummaryVisible = false
            };

            LongEventHandler.QueueLongEvent(StartupLoading.DoLoadLate, "RF.LoadLabel", false, null);
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DrawUI(inRect);
        }

        public override string SettingsCategory() => "RimForge";
        
    }
}
