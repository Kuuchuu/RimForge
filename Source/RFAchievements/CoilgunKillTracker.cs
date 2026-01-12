using AchievementsExpanded;
using Verse;

namespace RimForge.Achievements
{
    public class CoilgunKillTracker : Tracker3<int, Pawn, CoilgunShellDef>
    {
        public override string Key
        {
            get => nameof(CoilgunKillTracker);
            set { }
        }

        protected override string[] DebugText => [nameof(CoilgunKillTracker)];

        public CoilgunKillTracker() {}

        protected CoilgunKillTracker(CoilgunKillTracker other)
            :base(other)
        {
        }
    }
}
