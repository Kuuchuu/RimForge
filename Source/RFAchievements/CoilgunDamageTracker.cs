using System.Collections.Generic;
using Verse;

namespace RimForge.Achievements
{
    public sealed class CoilgunDamageTracker : CoilgunPostFireTracker
    {
        public int minKills;
        public float minDamage;
        public HashSet<CoilgunShellDef> exceptShells;

        public CoilgunDamageTracker() {}

        public CoilgunDamageTracker(CoilgunDamageTracker other)
            : base(other)
        {
            this.minDamage = other.minDamage;
            this.minKills = other.minKills;
            this.exceptShells = other.exceptShells;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref minDamage, "minDamage");
            Scribe_Values.Look(ref minKills, "minKills");
            Scribe_Collections.Look(ref exceptShells, "exceptShells", LookMode.Def);
        }

        public override bool Trigger(int kills, float totalDamage, CoilgunShellDef shellDef)
        {
            if (kills < minKills)
                return false;
            if (totalDamage < minDamage)
                return false;

            if (exceptShells == null || exceptShells.Count == 0 || shellDef == null)
                return true;

            return !exceptShells.Contains(shellDef);
        }
    }
}
