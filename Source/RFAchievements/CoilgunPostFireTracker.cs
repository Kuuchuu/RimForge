using AchievementsExpanded;
using Verse;

namespace RimForge.Achievements
{
    public abstract class CoilgunPostFireTracker : Tracker3<int, float, CoilgunShellDef>
    {
        private string _key = nameof(CoilgunPostFireTracker);
        public override string Key
        {
            get => _key;
            set => _key = value;
        }
        protected override string[] DebugText => new string[] {nameof(CoilgunPostFireTracker) };

        protected CoilgunPostFireTracker() {}

        protected CoilgunPostFireTracker(CoilgunPostFireTracker other)
            :base(other)
        {
            this._key = other._key;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _key, "key");
        }

        public override bool Trigger(int kills, float totalDamage, CoilgunShellDef shellDef)
        {
            return false;
        }
    }
}
