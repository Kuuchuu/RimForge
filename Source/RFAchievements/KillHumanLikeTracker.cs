using AchievementsExpanded;
using JetBrains.Annotations;
using Verse;

namespace RimForge.Achievements
{
    /// <summary>
    /// Sames as KillTracker but only counts kills of humanlike pawns.
    /// As determined by Pawn.RaceProps.Humanlike.
    /// </summary>
    [UsedImplicitly]
    public class KillHumanLikeTracker : KillTracker
    {
        public KillHumanLikeTracker() { }

        public KillHumanLikeTracker(KillHumanLikeTracker reference) : base(reference) { }

        public override bool Trigger(Pawn pawn, DamageInfo? dinfo)
        {
            if (!(pawn.RaceProps?.Humanlike ?? false))
                return false;
            
            return base.Trigger(pawn, dinfo);
        }
    }
}