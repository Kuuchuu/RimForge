using AchievementsExpanded;
using Verse;

namespace RimForge.Achievements
{
    public class CoilgunAnyKillTracker : KillTracker
    {
        private string _key = nameof(CoilgunAnyKillTracker);
        public override string Key
        {
            get => _key;
            set => _key = value;
        }
        public CoilgunAnyKillTracker() {}

        public CoilgunAnyKillTracker(CoilgunAnyKillTracker other)
            : base(other)
        {
            this._key = other._key;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _key, "key");
        }

        public override bool Trigger(Pawn pawn, DamageInfo? dinfo)
        {
            if (pawn == null)
                return false;

            if (killedThings.Contains(pawn.GetUniqueLoadID()))
                return false;

            if (dinfo?.Instigator?.def != RFDefOf.RF_Coilgun)
                return false;

            bool result = base.Trigger(pawn, dinfo);
            return result;
        }
    }
}
