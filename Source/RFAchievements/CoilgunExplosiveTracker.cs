using AchievementsExpanded;
using Verse;

namespace RimForge.Achievements
{
    public sealed class CoilgunExplosiveTracker : Tracker2<Explosion, int>
    {
        public override string Key
        {
            get => nameof(CoilgunExplosiveTracker);
            set { }
        }

        protected override string[] DebugText => [nameof(CoilgunExplosiveTracker)];

        public int minKills;

        public CoilgunExplosiveTracker() {}

        public CoilgunExplosiveTracker(CoilgunExplosiveTracker other)
            :base(other)
        {
            this.minKills = other.minKills;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref minKills, "minKills");
        }

        public override bool Trigger(Explosion e, int kills)
        {
            Core.Log($"Explosion killed {kills} targets. Minimum required: {minKills}");
            return e != null && kills >= minKills;
        }
    }
}
