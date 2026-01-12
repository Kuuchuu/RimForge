using AchievementsExpanded;
using Verse;

namespace RimForge.Achievements
{
    public sealed class GenericEventTracker : Tracker<Core.AchievementEvent>
    {
        public static void Fire(Core.AchievementEvent trigger)
        {
            if (trigger == Core.AchievementEvent.None)
                return;

            Core.Log($"Generic RF Achievement Event Fired: {trigger}");
            var cards = AchievementPointManager.GetCards<GenericEventTracker>();
            if (cards == null)
                return;

            foreach (var item in cards)
            {
                if(item.tracker is GenericEventTracker ge)
                {
                    if (ge.Trigger(trigger))
                    {
                        item.UnlockCard();
                    }
                }
            }
        }

        public override string Key
        {
            get => nameof(GenericEventTracker);
            set { }
        }

        protected override string[] DebugText => [nameof(GenericEventTracker)];

        public Core.AchievementEvent trigger;

        public GenericEventTracker() { }

        public GenericEventTracker(GenericEventTracker other)
            :base(other)
        {
            this.trigger = other.trigger;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref trigger, "trigger");
        }

        public override bool Trigger(Core.AchievementEvent trigger)
        {
            return this.trigger == trigger;
        }
    }
}
