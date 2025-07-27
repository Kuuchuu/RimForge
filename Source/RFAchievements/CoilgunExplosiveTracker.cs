using AchievementsExpanded;
using Verse;

namespace RimForge.Achievements
{
    public class CoilgunExplosiveTracker : Tracker2<Explosion, int>
    {
        private string _key = nameof(CoilgunExplosiveTracker);
        public override string Key
        {
            get => _key;
            set => _key = value;
        }

        protected override string[] DebugText => new string[] {nameof(CoilgunExplosiveTracker) };

        public int minKills;

        public CoilgunExplosiveTracker() {}

        public CoilgunExplosiveTracker(CoilgunExplosiveTracker other)
            :base(other)
        {
            this.minKills = other.minKills;
            this._key = other._key;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _key, "key");
            Scribe_Values.Look(ref minKills, "minKills");
        }

        public override bool Trigger(Explosion e, int kills)
        {
            return e != null && kills >= minKills;
        }
    }
}
