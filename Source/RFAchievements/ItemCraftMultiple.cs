using System;
using AchievementsExpanded;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimForge.Achievements
{
    [UsedImplicitly]
    public class ItemCraftMultiple : ItemCraftTracker
    {
        public List<ThingDef> toCraft = new List<ThingDef>();

        private List<int> crafted = new List<int>();
        private Dictionary<ThingDef, int> defToIndex = new Dictionary<ThingDef, int>();

        public override (float percent, string text) PercentComplete
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < crafted.Count; i++)
                {
                    sum += Mathf.Min(crafted[i], base.count);
                }

                float pct = sum / ((float) base.count * crafted.Count);
                string text = $"{sum} / {base.count * crafted.Count}";

                return (pct, text);
            }
        }

        public bool IsComplete
        {
            get
            {
                foreach (var item in crafted)
                {
                    if (item < count)
                        return false;
                }

                return true;
            }
        }

        protected override string[] DebugText =>
        [
            $"toCraft: {string.Join(",", toCraft)}",
            $"crafted: {string.Join(", ", crafted)}",
            $"Percentage: {PercentComplete.percent}",
            PercentComplete.text
        ];

        public ItemCraftMultiple() { }

        public ItemCraftMultiple(ItemCraftMultiple other)
            : base(other)
        {
            this.toCraft = other.toCraft?.ListFullCopy() ?? new List<ThingDef>();
            this.crafted = other.crafted?.ListFullCopy() ?? new List<int>();
            while (crafted.Count != toCraft.Count)
            {
                crafted.Add(0);
            }
            defToIndex = new Dictionary<ThingDef, int>();
            for (int i = 0; i < toCraft.Count; i++)
            {
                defToIndex.Add(toCraft[i], i);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref toCraft, "toCraft", LookMode.Def);
            Scribe_Collections.Look(ref crafted, "crafted", LookMode.Value);
            toCraft ??= new List<ThingDef>();
            crafted ??= new List<int>();

            while (crafted.Count != toCraft.Count)
            {
                crafted.Add(0);
            }
            defToIndex = new Dictionary<ThingDef, int>();
            for (int i = 0; i < toCraft.Count; i++)
            {
                defToIndex.Add(toCraft[i], i);
            }
        }

        public override bool Trigger(Thing thing)
        {
            if (!defToIndex.TryGetValue(thing.def, out int index))
                return false;

            if (madeFrom != null || madeFrom != thing.Stuff)
                return false;

            if (quality != null && thing.TryGetQuality(out var qual) && qual < quality)
                return false;

            crafted[index] += thing.stackCount;
            if (IsComplete)
                return true;
            return false;
        }
    }
}
