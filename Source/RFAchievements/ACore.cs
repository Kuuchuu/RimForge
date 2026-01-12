using AchievementsExpanded;
using RimForge;
using RimForge.Achievements;
using System;
using System.Linq;
using Verse;

namespace Rimforge.Achievements
{
    [HotSwapAll]
    public class ACore : Mod
    {
        public ACore(ModContentPack content) : base(content)
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                Core.CoilgunHitPawn += OnCoilgunHitPawn;
                Core.CoilgunPostFire += OnCoilgunPostFire;
                Core.CoilgunExplosion += OnCoilgunExplosion;
                Core.GenericAchievementEvent += GenericEventTracker.Fire;
                Core.Log("Hooked achievements!");
            });
        }

        private static void OnCoilgunHitPawn(Pawn pawn, CoilgunShellDef shellDef, int penDepth)
        {
            foreach (var card in AchievementPointManager.GetCards<CoilgunKillTracker>())
            {
                try
                {
                    if (((CoilgunKillTracker)card.tracker).Trigger(penDepth, pawn, shellDef))
                    {
                        card.UnlockCard();
                    }
                }
                catch (Exception ex)
                {
                    Core.Error($"Unable to trigger event for card validation. To avoid further errors {card.def.LabelCap} has been automatically unlocked.\n\nException={ex.Message}");
                    card.UnlockCard();
                }
            }
        }

        private static void OnCoilgunPostFire(int pawnKills, float totalDamage, CoilgunShellDef shellDef)
        {
            var cards = AchievementPointManager.GetCards<CoilgunPostFireTracker>();
            if (cards.Count == 0)
            {
                Core.Error("No CoilgunPostFireTracker cards found during OnCoilgunPostFire event.");
                return;
            }
            Core.Log($"Postfire: {pawnKills} kills, {totalDamage} damage with {shellDef?.defName ?? "null shell"} - checking {cards.Count} cards ({string.Join(", ", cards.Select(c => c.def.defName))})");
            
            foreach (var card in cards)
            {
                try
                {
                    if (((CoilgunPostFireTracker)card.tracker).Trigger(pawnKills, totalDamage, shellDef))
                    {
                        card.UnlockCard();
                    }
                }
                catch (Exception ex)
                {
                    Core.Error($"Unable to trigger event for card validation. To avoid further errors {card.def.LabelCap} has been automatically unlocked.\n\nException={ex.Message}");
                    card.UnlockCard();
                }
            }
        }

        private static void OnCoilgunExplosion(Explosion e, int count)
        {
            foreach (var card in AchievementPointManager.GetCards<CoilgunExplosiveTracker>())
            {
                try
                {
                    if (((CoilgunExplosiveTracker)card.tracker).Trigger(e, count))
                    {
                        card.UnlockCard();
                    }
                }
                catch (Exception ex)
                {
                    Core.Error($"Unable to trigger event for card validation. To avoid further errors {card.def.LabelCap} has been automatically unlocked.\n\nException={ex.Message}");
                    card.UnlockCard();
                }
            }
        }
    }
}
