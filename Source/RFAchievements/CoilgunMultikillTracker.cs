using Verse;

namespace RimForge.Achievements
{
    public sealed class CoilgunMultikillTracker : CoilgunPostFireTracker
    {
        public int minKills;
        public CoilgunShellDef exceptShell;
        public CoilgunShellDef onlyShell;

        public CoilgunMultikillTracker() { }

        public CoilgunMultikillTracker(CoilgunMultikillTracker other)
            : base(other)
        {
            this.minKills = other.minKills;
            this.exceptShell = other.exceptShell;
            this.onlyShell = other.onlyShell;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref minKills, "minKills");
            Scribe_Defs.Look(ref exceptShell, "exceptShell");
            Scribe_Defs.Look(ref onlyShell, "onlyShell");
        }

        public override bool Trigger(int kills, float totalDamage, CoilgunShellDef shellDef)
        {
            if (shellDef == null)
                return false;

            bool isOnly = this.onlyShell == null || this.onlyShell == shellDef;
            bool isNotExcluded = this.exceptShell == null || this.exceptShell != shellDef;

            return kills >= this.minKills && isOnly && isNotExcluded;
        }
    }
}
