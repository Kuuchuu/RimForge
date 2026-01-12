using RimForge.Comps;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimForge.Buildings
{
    [StaticConstructorOnStartup]
    public class Building_Capacitor : Building
    {
        public static Material ChargeMat;
        public static float OffY = -0.0128f;
        public static float SizeX = 0.3311f, SizeY = 0.4533f;

        public CompCapacitor CompCap => _power ??= GetComp<CompCapacitor>();
        private CompCapacitor _power;

        private MaterialPropertyBlock block;

        // TODO use this more modern method to draw battery overlay, this is the correct way to do it:
        /*
         * 	protected override void DrawAt(Vector3 drawLoc, bool flip = false)
			{
				base.DrawAt(drawLoc, flip);
				CompPowerBattery comp = base.GetComp<CompPowerBattery>();
				GenDraw.FillableBarRequest r = new GenDraw.FillableBarRequest
				{
					center = drawLoc + Vector3.up * 0.1f,
					size = Building_Battery.BarSize,
					fillPercent = comp.StoredEnergy / comp.Props.storedEnergyMax,
					filledMat = Building_Battery.BatteryBarFilledMat,
					unfilledMat = Building_Battery.BatteryBarUnfilledMat,
					margin = 0.15f
				};
				Rot4 rotation = base.Rotation;
				rotation.Rotate(RotationDirection.Clockwise);
				r.rotation = rotation;
				GenDraw.DrawFillableBar(r);
				if (this.ticksToExplode > 0 && base.Spawned)
				{
					base.Map.overlayDrawer.DrawOverlay(this, OverlayTypes.BurningWick);
				}
			}
         */
        
        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);

            if (phase != DrawPhase.Draw)
                return;

            if (ChargeMat == null)
            {
                ChargeMat = new Material(ShaderTypeDefOf.Cutout.Shader);
                ChargeMat.mainTexture = Content.CapacitorCharge;
            }

            float lerp = CompCap.PercentageStored;

            Vector3 pos = DrawPos + new Vector3(0, float.Epsilon * 3, OffY - SizeY * (1 - lerp) * 0.5f);
            Vector3 size = new Vector3(SizeX, 1f, Mathf.Max(SizeY * lerp, 0.05f));
            var matrix = Matrix4x4.TRS(pos, Quaternion.identity, size);

            block ??= new MaterialPropertyBlock();
            block.SetVector("_MainTex_ST", new Vector4(1, lerp, 0f, 0f));
            block.SetColor("_Color", Color.Lerp(Color.red, Color.green, lerp));

            Graphics.DrawMesh(MeshPool.plane10, matrix, ChargeMat, 0, null, 0, block);
        }
    }
}
