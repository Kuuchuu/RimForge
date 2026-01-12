using AchievementsExpanded;
using Verse;

namespace RimForge.Achievements
{
    public class CoilgunAnyKillTracker : KillTracker
    {
        public CoilgunAnyKillTracker() {}

        public CoilgunAnyKillTracker(CoilgunAnyKillTracker other) : base(other) { }

        public override bool Trigger(Pawn pawn, DamageInfo? dinfo)
        {
            if (pawn == null)
                return false;
            if (dinfo?.Instigator?.def != RFDefOf.RF_Coilgun)
                return false;

            bool result = base.Trigger(pawn, dinfo);
            return result;
        }
    }
}
