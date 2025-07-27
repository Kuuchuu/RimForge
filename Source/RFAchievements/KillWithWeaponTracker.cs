using AchievementsExpanded;
using Verse;

namespace RimForge.Achievements
{
    public class KillWithWeaponTracker : KillTracker
    {
        private string _key = nameof(KillWithWeaponTracker);
        public override string Key
        {
            get => _key;
            set => _key = value;
        }

        public ThingDef weaponDef;

        public KillWithWeaponTracker(){}

        public KillWithWeaponTracker(KillWithWeaponTracker reference)
            :base(reference)
        {
            this.weaponDef = reference.weaponDef;
            this._key = reference._key;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Defs.Look(ref weaponDef, "weaponDef");
            Scribe_Values.Look(ref _key, "key");
        }

        public override bool Trigger(Pawn pawn, DamageInfo? dinfo)
        {
            if (dinfo == null)
                return false;

            if (weaponDef != null && dinfo.Value.Weapon != weaponDef)
                return false;

            return base.Trigger(pawn, dinfo);
        }
    }
}