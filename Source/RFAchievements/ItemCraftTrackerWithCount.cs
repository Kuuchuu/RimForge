using AchievementsExpanded;
using Verse;

namespace RimForge.Achievements
{
    public class ItemCraftTrackerWithCount : ItemCraftTracker
    {
        private string _key = nameof(ItemCraftTrackerWithCount);
        public override string Key
        {
            get => _key;
            set => _key = value;
        }

        public ItemCraftTrackerWithCount() { }
        public ItemCraftTrackerWithCount(ItemCraftTrackerWithCount other)
            : base(other) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _key, "key");
        }

        public override bool Trigger(Thing thing)
        {
            bool done = false;
            for (int i = 0; i < thing.stackCount; i++)
            {
                if (base.Trigger(thing))
                    done = true;
            }
            return done;
        }
    }
}
