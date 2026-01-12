using AchievementsExpanded;

namespace RimForge.Achievements
{
    public class CoilgunPostFireTracker : Tracker3<int, float, CoilgunShellDef>
    {
        public override string Key
        {
            get => nameof(CoilgunPostFireTracker);
            set { }
        }
        protected override string[] DebugText => [nameof(CoilgunPostFireTracker)];

        public CoilgunPostFireTracker() {}

        protected CoilgunPostFireTracker(CoilgunPostFireTracker other)
            :base(other)
        {
        }

        public override bool Trigger(int kills, float totalDamage, CoilgunShellDef shellDef)
        {
            return false;
        }
    }
}
