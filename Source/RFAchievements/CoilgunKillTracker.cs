using AchievementsExpanded;
using Verse;

namespace RimForge.Achievements
{
    public abstract class CoilgunKillTracker : Tracker3<int, Pawn, CoilgunShellDef>
    {
        private string _key = nameof(CoilgunKillTracker);
        public override string Key
        {
            get => _key;
            set => _key = value;
        }

        protected override string[] DebugText => new string[] {nameof(CoilgunKillTracker)};

        protected CoilgunKillTracker() {}

        protected CoilgunKillTracker(CoilgunKillTracker other)
            :base(other)
        {
            _key = other._key;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _key, "key");
        }
    }
}
