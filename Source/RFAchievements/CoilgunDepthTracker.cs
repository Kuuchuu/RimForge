using Verse;

namespace RimForge.Achievements
{
    public class CoilgunDepthTracker : CoilgunKillTracker
    {
        public int minDepth;

        private string _key = nameof(CoilgunDepthTracker);
        public override string Key
        {
            get => _key;
            set => _key = value;
        }

        public CoilgunDepthTracker(){}

        public CoilgunDepthTracker(CoilgunDepthTracker other)
            : base(other)
        {
            this.minDepth = other.minDepth;
            this._key = other._key;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _key, "key");
            Scribe_Values.Look(ref minDepth, "minDepth");
        }

        public override bool Trigger(int depth, Pawn p, CoilgunShellDef s)
        {
            return depth >= minDepth;
        }
    }
}
