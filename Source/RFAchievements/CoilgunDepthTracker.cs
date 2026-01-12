using Verse;

namespace RimForge.Achievements
{
    public class CoilgunDepthTracker : CoilgunKillTracker
    {
        public int minDepth;

        public CoilgunDepthTracker(){}

        public CoilgunDepthTracker(CoilgunDepthTracker other)
            : base(other)
        {
            this.minDepth = other.minDepth;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref minDepth, "minDepth");
        }

        public override bool Trigger(int depth, Pawn p, CoilgunShellDef s)
        {
            return depth >= minDepth;
        }
    }
}
