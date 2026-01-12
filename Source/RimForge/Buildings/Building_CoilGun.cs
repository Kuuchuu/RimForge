using RimForge.Effects;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimForge.Patches;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimForge.Buildings
{
    public class Building_Coilgun : Building, ICustomTargetingUser
    {
        [TweakValue("RimForge", 0, 20)]
        public static float CoilgunRecoil = 0.6f;
        [TweakValue("RimForge", 1, 100)]
        public static int BloodTrailLength = 20;
        public static List<CoilgunShellDef> ShellDefs = new List<CoilgunShellDef>();

        private const float TurretTurnSpeed = 60f / 60f;
        private const int FINISH_READYING = 120;
        private const int FINISH_FIRE = FINISH_READYING + 60 * 4;
        private const int FINISH_PAUSE = FINISH_FIRE + 60 * 4;
        private const int FINISH_UNREADYING = FINISH_PAUSE + 60 * 5;

        public enum State
        {
            Idle,
            Readying,
            Paused,
            UnReadying
        }

        public new CompPowerTrader PowerComp => _power ??= GetComp<CompPowerTrader>();
        private CompPowerTrader _power;
        public CompRefuelable FuelComp => _fuel ??= GetComp<CompRefuelable>();
        private CompRefuelable _fuel;

        public float ArmLerp;
        public float TurretRotation;
        public float TargetRotation;
        public float Recoil;
        public float RecoilVel;

        public State CurrentState = State.Idle;
        public int FireTicks = -1;
        public LocalTargetInfo CurrentTargetInfo = LocalTargetInfo.Invalid;
        public IntVec3 LastKnowPos;
        public bool DrawAffectedCells = false;

        public DrawPart Top, Cables, LeftPivot, RightPivot;
        public DrawPart BarLeft, BarRight;
        public CoilgunShellDef CurrentShellDef
        {
            get => FuelComp.Props.fuelFilter.AnyAllowedDef as CoilgunShellDef;
            set
            {
                FuelComp.Props.fuelFilter.SetDisallowAll();
                if(value != null)
                    FuelComp.Props.fuelFilter.SetAllow(value, true);
                shellDef = value;
            }
        }

        private List<LinearElectricArc> backArcs = new List<LinearElectricArc>();
        private List<LinearElectricArc> frontArcs = new List<LinearElectricArc>();
        private Sustainer soundSustainer;
        private List<IntVec3> cells = new List<IntVec3>();
        private HashSet<IntVec3> cellsHash = new HashSet<IntVec3>();
        private List<Building_Capacitor> rawCapacitors = new List<Building_Capacitor>();
        private int ticksSinceCapacitorRefresh;
        private int tickCounter;
        private CoilgunShellDef shellDef;
        private Dictionary<int, List<Action>> tickActions = new Dictionary<int, List<Action>>(128);

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref FireTicks, "coil_fireTicks", -1);
            Scribe_Values.Look(ref CurrentState, "coil_state", State.Idle);
            Scribe_TargetInfo.Look(ref CurrentTargetInfo, "coil_target", LocalTargetInfo.Invalid);
            Scribe_Values.Look(ref LastKnowPos, "coil_lastKnownPos");
            Scribe_Values.Look(ref Recoil, "coil_recoil");
            Scribe_Values.Look(ref RecoilVel, "coil_recoilVel");
            Scribe_Values.Look(ref ticksSinceCapacitorRefresh, "coil_ticksSinceCapRefresh");
            Scribe_Collections.Look(ref rawCapacitors, "coil_rawCaps", LookMode.Reference);
            rawCapacitors ??= new List<Building_Capacitor>();

            Scribe_Defs.Look(ref shellDef, "coil_shellDef");

            if (Scribe.mode == LoadSaveMode.LoadingVars)
                ReplaceFuelProps(GetComp<CompRefuelable>());

            CurrentShellDef = shellDef;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            DrawAffectedCells = false;
            CurrentShellDef ??= RFDefOf.RF_CoilgunShellAP;
        }

        public override void PostMake()
        {
            base.PostMake();
            ReplaceFuelProps(FuelComp);
            CurrentShellDef = shellDef;
        }

        public void ReplaceFuelProps(CompRefuelable comp)
        {
            var props = comp.Props;
            var newProps = props.CloneObject();
            newProps.fuelFilter = new ThingFilter();
            comp.props = newProps;
        }

        public virtual void Setup()
        {
            if (Content.CoilgunTop == null)
                Content.LoadBuildingGraphics(this);

            Top = new DrawPart(Content.CoilgunTop, new Vector2(2.2f, 0f));
            Cables = new DrawPart(Content.CoilgunCables, new Vector2(2.2f, 0f));
            Top.DepthOffset = AltitudeLayer.PawnUnused.AltitudeFor() + 0.001f; // Barrel of gun draws over pawns.
            Cables.DepthOffset = Top.DepthOffset + 0.05f;

            LeftPivot = new DrawPart(Content.CoilgunLinkLeft, new Vector2(1f, 0f));
            RightPivot = new DrawPart(Content.CoilgunLinkRight, new Vector2(1f, 0f));

            BarLeft = new DrawPart(Content.CoilgunBarLeft, new Vector2(0f, 0f));
            BarRight = new DrawPart(Content.CoilgunBarRight, new Vector2(0f, 0f));

            BarLeft.OffsetUsingGrandparent = true;
            BarRight.OffsetUsingGrandparent = true;

            Top.AddChild(LeftPivot);
            Top.AddChild(RightPivot);

            LeftPivot.AddChild(BarLeft);
            RightPivot.AddChild(BarRight);
            BarLeft.MatchRotation = Top;
            BarRight.MatchRotation = Top;
        }

        public override void Tick()
        {
            base.Tick();

            ticksSinceCapacitorRefresh++;

            if (CurrentTargetInfo.IsValid)
            {
                LastKnowPos = CurrentTargetInfo.Cell;
                var targetDrawPos = CurrentTargetInfo.HasThing
                    ? CurrentTargetInfo.Thing.DrawPos
                    : CurrentTargetInfo.CenterVector3;
                var offset = (targetDrawPos - DrawPos);
                offset.z *= -1;
                TargetRotation = offset.ToAngleFlat();
            }
            TurretRotation = Mathf.MoveTowardsAngle(TurretRotation, TargetRotation, TurretTurnSpeed);

            // Do tick actions.
            tickCounter++;
            if (tickActions.TryGetValue(tickCounter, out var list))
            {
                foreach (var thing in list)
                {
                    try
                    {
                        thing?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Core.Error("Exception in tick event:", e);
                    }
                }
                tickActions.Remove(tickCounter);
            }

            FuelComp.Props.fuelLabel = CurrentShellDef.LabelCap;
            FuelComp.Props.fuelGizmoLabel = CurrentShellDef.LabelCap;

            Graphic TopGraphic()
            {
                bool wantsFrontArcs = ArmLerp >= 0.7f;
                if (wantsFrontArcs)
                    return Content.CoilgunTopGlow;
                return Content.CoilgunTop;
            }
            Graphic CableGraphic()
            {
                bool wantsFrontArcs = ArmLerp >= 0.7f;
                if (wantsFrontArcs)
                    return Content.CoilgunCablesGlow;
                return Content.CoilgunCables;
            }

            if (Top != null)
            {
                Top.Graphic = TopGraphic();
                Cables.Graphic = CableGraphic();
            }

            TickState();
            TickBackArcs();
            TickFrontArcs();
        }

        private void AddTickEvent(int tick, Action a)
        {
            if (a == null || tick <= tickCounter)
                return;

            if (tickActions.TryGetValue(tick, out var list))
                list.Add(a);
            else
                tickActions.Add(tick, new List<Action> { a });
        }

        private void TickState()
        {
            if (CurrentState != State.Idle)
                FireTicks++;

            bool playSound = false;

            switch (CurrentState)
            {
                case State.Readying:

                    float delta = Mathf.Abs(Mathf.DeltaAngle(TurretRotation, TargetRotation));
                    if (delta > 6f && FireTicks < FINISH_FIRE)
                        FireTicks--;

                    ArmLerp = Mathf.InverseLerp(0, FINISH_READYING, FireTicks);
                    playSound = FireTicks >= FINISH_READYING;
                    if (FireTicks >= FINISH_FIRE)
                    {
                        Fire(LastKnowPos);
                        RecoilVel = CoilgunRecoil;
                        Find.CameraDriver.shaker.SetMinShake(3f);
                        CurrentState = State.Paused;
                    }
                    break;

                case State.Paused:
                    playSound = true;
                    ArmLerp = 1;
                    if(FireTicks >= FINISH_PAUSE)
                        CurrentState = State.UnReadying;
                    break;

                case State.UnReadying:
                    ArmLerp = 1f - Mathf.InverseLerp(FINISH_PAUSE, FINISH_UNREADYING, FireTicks);
                    if (FireTicks >= FINISH_UNREADYING)
                    {
                        CurrentState = State.Idle;
                        FireTicks = -1;
                    }
                    break;
            }

            RecoilVel *= 0.87f;
            Recoil += RecoilVel;
            Recoil *= 0.96f;
            SoundTick(playSound);
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            if (!DrawAffectedCells)
                return;

            var endPos = CurrentTargetInfo.Cell;
            var map = Map;
            int mapSize = Mathf.CeilToInt(Mathf.Sqrt(map.Size.x * map.Size.x + map.Size.y * map.Size.y) * 1.5f);
            Vector3 dir = (endPos - Position).ToVector3().normalized;
            IntVec3 newEndPos = Position + (dir * mapSize).ToIntVec3();
            //Core.Log($"{endPos} - {Position} = {dir}, {Position} + {dir} * {mapSize} = {newEndPos}");
            var list = GetAffectedCells(newEndPos);
            GenDraw.DrawFieldEdges(list, Color.red);

            var currentShell = CurrentShellDef;
            if (currentShell?.explosionDamageType == null || currentShell.explosionRadius <= 0f)
                return;

            list.Sort((a, b) =>
            {
                int sqrDstA = (a - Position).LengthHorizontalSquared;
                int sqrDstB = (b - Position).LengthHorizontalSquared;
                return sqrDstA - sqrDstB;
            });

            foreach (var cell in list)
            {
                if (!cell.InBounds(map))
                    break;

                foreach(var thing in map.thingGrid.ThingsListAtFast(cell))
                {
                    if (thing == null || thing.Destroyed)
                        continue;

                    if ((thing is Building b && b.def.altitudeLayer >= AltitudeLayer.DoorMoveable) || thing is Pawn {Dead: false, Downed: false})
                    {
                        GenDraw.DrawTargetHighlightWithLayer(cell, AltitudeLayer.MoteOverhead);
                        GenExplosion.RenderPredictedAreaOfEffect(cell, currentShell.explosionRadius, Color.red);
                        return;
                    }
                }
            }
        }

        public bool HasLoadedShell()
        {
            return FuelComp.Fuel > 0;
        }

        public void EjectLoadedShell()
        {
            if (!HasLoadedShell())
                return;

            var thing = ThingMaker.MakeThing(CurrentShellDef);
            GenPlace.TryPlaceThing(thing, Position - new IntVec3(0, 0, 3), Map, ThingPlaceMode.Near);

            ClearLoadedShell();
        }

        public void ClearLoadedShell()
        {
            FuelComp.ConsumeFuel(1);
        }

        public IEnumerable<Building_Capacitor> GetConnectedCapacitors()
        {
            if (rawCapacitors == null)
                yield break;

            if (ticksSinceCapacitorRefresh >= 240)
                RefreshCapacitors();

            var selfNet = PowerComp.PowerNet;

            foreach (var cap in rawCapacitors)
            {
                if (cap == null || cap.Destroyed || !cap.Spawned)
                    continue;

                var net = cap.CompCap?.PowerNet;
                if (net == null)
                    continue;
                if (net != selfNet)
                    continue;

                yield return cap;
            }
        }

        private void RemoveCapacitorPower(float capacitors)
        {
            float toRemove = capacitors;
            foreach(var cap in GetConnectedCapacitors())
            {
                if (toRemove <= 0f)
                    return;

                float take = Mathf.Min(toRemove, cap.CompCap.PercentageStored);
                cap.CompCap.PercentageStored -= take;
                toRemove -= take;
            }

            if (toRemove > 0)
                Core.Error($"Tried to remove {capacitors} capacitor percentage of power from capacitors, could only remove {capacitors - toRemove}.");
        }

        private void GetCapacitorState(out int count, out float stored)
        {
            count = 0;
            stored = 0;
            foreach (var cap in GetConnectedCapacitors())
            {
                count++;
                stored += cap.CompCap.PercentageStored;
            }
        }

        private void RefreshCapacitors()
        {
            rawCapacitors.Clear();
            var selfNet = PowerComp.PowerNet;
            if (selfNet?.powerComps == null)
                return;

            foreach (var thing in selfNet.powerComps)
            {
                if (thing.parent is Building_Capacitor cap)
                    rawCapacitors.Add(cap);
            }
        }

        private List<IntVec3> GetAffectedCells(IntVec3 end)
        {
            IntVec3 start = Position;
            cellsHash.Clear();
            cells.Clear();

            Building_TeslaCoil.MakeLine(start, end, cellsHash);

            // ... but this algorithm doesn't fill in the 'corners', allowing pawns to slip through the 'gaps'
            //     when approaching at an angle.
            //     Lets fix that.
            var toAdd = new List<IntVec3>(cellsHash.Count);
            if (cellsHash.Contains(start))
                cellsHash.Remove(start);
            if (cellsHash.Contains(end))
                cellsHash.Remove(end);
            foreach (var point in cellsHash)
            {
                bool tr = cellsHash.Contains(new IntVec3(point.x + 1, 0, point.z + 1));
                bool br = cellsHash.Contains(new IntVec3(point.x + 1, 0, point.z - 1));
                bool tl = cellsHash.Contains(new IntVec3(point.x - 1, 0, point.z + 1));
                bool bl = cellsHash.Contains(new IntVec3(point.x - 1, 0, point.z - 1));

                if (tr)
                    toAdd.Add(new IntVec3(point.x + 1, 0, point.z));
                if (bl)
                    toAdd.Add(new IntVec3(point.x, 0, point.z - 1));

                if (br)
                    toAdd.Add(new IntVec3(point.x, 0, point.z - 1));
                if (tl)
                    toAdd.Add(new IntVec3(point.x - 1, 0, point.z));

            }
            foreach (var point in toAdd)
            {
                // Important note: there are duplicate positions in toAdd.
                // However, since tempPoints is a HashSet, it discards these duplicate items.
                cellsHash.Add(point);
            }

            foreach (var item in cellsHash)
            {
                float sqrDst = (item - start).LengthHorizontalSquared;
                if(sqrDst > 8 * 8)
                    cells.Add(item);
            }

            return cells;
        }

        private void Fire(IntVec3 endPos)
        {
            var map = Map;
            int mapSize = Mathf.CeilToInt(Mathf.Sqrt(map.Size.x * map.Size.x + map.Size.y * map.Size.y) * 1.5f);
            Vector3 dir = (endPos - Position).ToVector3().normalized;
            IntVec3 newEndPos = Position + (dir * mapSize).ToIntVec3();
            var list = GetAffectedCells(newEndPos);
            CurrentTargetInfo = LocalTargetInfo.Invalid;

            var shellDef = CurrentShellDef;

            float damage = shellDef.baseDamage * Settings.CoilgunBaseDamageMultiplier;

            int affected = 0;
            int cells = 0;
            int penDepth = 0;

            list.Sort((a, b) =>
            {
                int sqrDstA = (a - Position).LengthHorizontalSquared;
                int sqrDstB = (b - Position).LengthHorizontalSquared;
                return sqrDstA - sqrDstB;
            });

            IntVec3? firstCell = null;
            IntVec3? lastCell = null;
            float totalDamage = 0;
            int bloodCount = 0;
            ThingDef bloodToSplatter = null;
            Color bloodSplatterColor = default;
            Vector2 bloodStartPos = default;
            string bloodPawnName = null;

            void MakeBloodAt(IntVec3 cell, int remaining)
            {
                float p = 1f - ((float)remaining / BloodTrailLength);
                float radius = p * BloodTrailLength * 0.095f;
                int count = (int) Math.Max(1, radius * radius * Mathf.PI);
                for (int i = 0; i < count; i++)
                {
                    IntVec3 pos = cell;
                    if (i != 0)
                    {
                        pos += (Rand.InsideUnitCircleVec3 * radius).ToIntVec3();
                        if (pos == cell)
                            continue;
                    }

                    int delay = (BloodTrailLength - remaining) * Rand.Range(1, 3);
                    int tick = tickCounter + delay;
                    AddTickEvent(tick, () =>
                    {
                        FilthMaker.TryMakeFilth(pos, map, bloodToSplatter, bloodPawnName, Mathf.Min(remaining, 2));
                    });
                    var splash = new FollowBezierSpark();
                    splash.Start = bloodStartPos;
                    splash.End = pos.ToVector3Shifted().WorldToFlat();
                    splash.Ticks = delay;
                    splash.Color = bloodSplatterColor;
                    splash.Spawn(map);
                }
            }

            bool hasDoneExplosion = false;
            string stoppedAfterHitting = null;
            int pawnKills = 0;
            foreach (var cell in list.TakeWhile(cell => cell.InBounds(map)))
            {
                cells++;
                firstCell ??= cell;
                lastCell = cell;

                if (bloodCount > 0)
                {
                    MakeBloodAt(cell, bloodCount);
                    bloodCount--;
                }
                
                var things = map.thingGrid.ThingsListAtFast(cell);
                if (things == null)
                    continue;

                bool keepGoing = true;
                for(int i = 0; i < things.Count; i++)
                {
                    try
                    {
                        var thing = things[i];
                        if (thing.Destroyed)
                            continue;
                        if (thing is Pawn p && (p.Downed || p.Dead))
                            continue;
                        
                        Pawn pawn = thing as Pawn;
                        Building b = thing as Building;
                        if (b != null || pawn != null)
                        {
                            var info = new DamageInfo(RFDefOf.RF_CoilgunDamage, damage * (b == null ? 1f : Settings.CoilgunBuildingDamageMulti), 100, instigator: this);
                            info.SetIgnoreArmor(true);
                            var result = thing.TakeDamage(info);
                            affected++;
                            totalDamage += result.totalDamageDealt;

                            #region VEA
                            if (Core.CoilgunHitPawn != null && pawn != null && (pawn.Dead || pawn.Destroyed))
                            {
                                pawnKills++;
                                Core.CoilgunHitPawn(pawn, shellDef, penDepth);
                            }
                            #endregion

                            if (Settings.CoilgunSplatterBlood && pawn?.RaceProps?.BloodDef != null)
                            {
                                Color defaultBlood = pawn.RaceProps.meatColor == Color.white ? Color.red : pawn.RaceProps.meatColor;
                                bloodToSplatter = pawn.RaceProps.BloodDef;
                                bloodCount = BloodTrailLength;
                                bloodPawnName = pawn.LabelIndefinite();
                                bloodStartPos = pawn.DrawPos.WorldToFlat();
                                bloodSplatterColor = pawn.RaceProps.IsMechanoid ? Color.black : pawn.RaceProps.Humanlike ? Color.red : defaultBlood;
                            }

                            if (pawn?.RaceProps?.IsMechanoid ?? false)
                            {
                                var basePos = pawn.DrawPos;
                                basePos.y = AltitudeLayer.MoteOverhead.AltitudeFor();

                                FleckMaker.ThrowLightningGlow(basePos, map, 0.5f);
                                FleckMaker.ThrowLightningGlow(basePos, map, 0.5f);
                                
                                FleckMaker.ThrowMicroSparks(basePos + Rand.InsideUnitCircleVec3 * 0.5f, map);
                                FleckMaker.ThrowMicroSparks(basePos + Rand.InsideUnitCircleVec3 * 0.5f, map);
                                FleckMaker.ThrowMicroSparks(basePos + Rand.InsideUnitCircleVec3 * 0.5f, map);
                            }
                            
                            if (!hasDoneExplosion && shellDef.explosionDamageType != null && shellDef.explosionRadius > 0 && (b == null || b.def.altitudeLayer >= AltitudeLayer.DoorMoveable))
                            {
                                if(shellDef.useHEKillTracker)
                                    HEShellKillTracker.BeginCapture();
                                GenExplosion.DoExplosion(cell, map, shellDef.explosionRadius, shellDef.explosionDamageType, this, shellDef.explosionDamage ?? -1, shellDef.explosionArmorPen ?? -1f);
                                hasDoneExplosion = true;
                            }
                        }
                        if (b != null || (pawn != null && shellDef.pawnsCountAsPen))
                        {
                            if (b != null && b.def.altitudeLayer < AltitudeLayer.DoorMoveable)
                                continue;

                            damage *= shellDef.penDamageMultiplier * Settings.CoilgunPenDamageMultiplier;
                            penDepth++;

                            if (shellDef.maxPen >= 0 && penDepth > shellDef.maxPen)
                            {
                                keepGoing = false;
                                stoppedAfterHitting = b?.GetCustomLabelNoCount(false) ?? pawn.LabelCap;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Core.Error($"Exception dealing coilgun damage to {things[i]}.", e);
                    }
                }
                if (!keepGoing)
                    break;
            }

            Core.CoilgunPostFire?.Invoke(pawnKills, totalDamage, shellDef);

            Vector3 moteStart = firstCell.Value.ToVector3ShiftedWithAltitude(AltitudeLayer.MoteOverhead);
            Vector3 moteEnd = lastCell.Value.ToVector3ShiftedWithAltitude(AltitudeLayer.MoteOverhead);
            // Legacy 1.2 (although I don't think it ever worked even in 1.2)
            //MoteMaker.MakeConnectingLine(moteStart, moteEnd, ThingDefOf.Mote_FireGlow, map, 1);
            FleckMaker.ConnectingLine(moteStart, moteEnd, FleckDefOf.FireGlow, map, 1f);
            DoMuzzleFlash();

            Current.CameraDriver.shaker.DoShake(5);

            RemoveCapacitorPower(Settings.CoilgunBasePowerReq);
            ClearLoadedShell();
            Core.Log($"Hit {affected} things for total {totalDamage} damage, scanned {cells} of {list.Count} cells.");

            if (Settings.CoilgunDisplayDamageReport)
            {
                if(stoppedAfterHitting != null)
                    Messages.Message("RF.Coilgun.DamageReportStopped".Translate(shellDef.LabelCap, affected, totalDamage, stoppedAfterHitting), MessageTypeDefOf.NeutralEvent, false);
                else
                    Messages.Message("RF.Coilgun.DamageReport".Translate(shellDef.LabelCap, affected, totalDamage), MessageTypeDefOf.NeutralEvent, false);
            }
        }

        private void DoMuzzleFlash()
        {
            var pos = ((Vector2)(Top.FinalMatrix * Matrix4x4.Translate(new Vector3(12f, 0, 0))).MultiplyPoint3x4(Vector3.zero)).FlatToWorld(AltitudeLayer.VisEffects.AltitudeFor());
            Mote mote = (Mote)ThingMaker.MakeThing(RFDefOf.RF_Motes_MuzzleFlash, null);
            mote.Scale = 20f;
            mote.exactRotation = -TurretRotation;
            mote.exactPosition = pos;
            GenSpawn.Spawn(mote, Position, Map, WipeMode.Vanish);
        }

        private void SoundTick(bool shouldBePlayingSound)
        {
            if (shouldBePlayingSound && (soundSustainer == null || soundSustainer.Ended))
            {
                // Start playing audio.
                SoundInfo info = SoundInfo.InMap(this, MaintenanceType.PerTick);
                soundSustainer = RFDefOf.RF_Sound_CoilgunFire.TrySpawnSustainer(info);
            }
            if (!shouldBePlayingSound)
            {
                soundSustainer?.End();
            }

            if (soundSustainer != null)
            {
                if (soundSustainer.Ended)
                    soundSustainer = null;
                else
                    soundSustainer.Maintain();
            }
        }

        private void TickBackArcs()
        {
            bool wantsBackArcs = ArmLerp >= 1f;
            if (!wantsBackArcs)
            {
                foreach (var arc in backArcs)
                {
                    arc.Destroy();
                }
                backArcs.Clear();
                return;
            }

            const int TO_SPAWN = 10;
            while (backArcs.Count < TO_SPAWN)
            {
                var newArc = new LinearElectricArc(20);
                newArc.Amplitude = Vector2.Lerp(new Vector2(0.01f, 0.1f), new Vector2(0.1f, 0.2f), (float)backArcs.Count / TO_SPAWN);
                newArc.Spawn(this.Map);
                backArcs.Add(newArc);
            }

            foreach (var arc in backArcs)
            {
                float xa = Rand.Range(-5f, -3.13f);
                float xb = Rand.Range(-5f, -3.13f);
                float addA = ((1f - Mathf.Abs(0.5f - Mathf.InverseLerp(-5f, -3.13f, xa))) * 2f) * 0.3f;
                float addB = ((1f - Mathf.Abs(0.5f - Mathf.InverseLerp(-5f, -3.13f, xb))) * 2f) * 0.3f;
                var a = (Vector2) Top.FinalMatrix.MultiplyPoint3x4(new Vector2(xa, 2.13f + addA));
                var b = (Vector2) Top.FinalMatrix.MultiplyPoint3x4(new Vector2(xb, -2.13f - addB));

                arc.Start = a;
                arc.End = b;
            }
        }

        private void TickFrontArcs()
        {
            bool wantsFrontArcs = ArmLerp >= 0.7f;
            if (!wantsFrontArcs)
            {
                foreach (var arc in frontArcs)
                {
                    arc.Destroy();
                }
                frontArcs.Clear();
                return;
            }

            const int TO_SPAWN = 25;
            while (frontArcs.Count < TO_SPAWN)
            {
                var newArc = new LinearElectricArc(4);
                newArc.Amplitude = Vector2.Lerp(new Vector2(0.01f, 0.1f), new Vector2(0.1f, 0.2f), (float)frontArcs.Count / TO_SPAWN);
                newArc.Spawn(this.Map);
                frontArcs.Add(newArc);
            }

            foreach (var arc in frontArcs)
            {
                float h = Rand.Range(-0.94f, 3.843f);
                var a = (Vector2)Top.FinalMatrix.MultiplyPoint3x4(new Vector2(h, 0.489f));
                var b = (Vector2)Top.FinalMatrix.MultiplyPoint3x4(new Vector2(h, -0.489f));

                arc.Start = a;
                arc.End = b;
            }
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);

            if (phase != DrawPhase.Draw)
                return;

            if (Top == null)
                Setup();

            GetCapacitorState(out int capCount, out var _);

            if (capCount == 0)
            {
                Map.overlayDrawer.DrawOverlay(this, OverlayTypes.QuestionMark);
            }

            ArmLerp = Mathf.Clamp01(ArmLerp);
            float realLerp = 0.625f * ArmLerp; // 1.0 is full extended but I think 0.625 looks better.
            float armAngle = Mathf.Lerp(0f, 145f, realLerp);

            Top.SetTR(DrawPos.WorldToFlat(), TurretRotation);
            Top.DrawOffset = new Vector2(2.2f - Recoil, 0f);
            Cables.SetTR(DrawPos.WorldToFlat(), TurretRotation);
            Cables.DrawOffset = new Vector2(0.128f - Recoil, 0f);

            LeftPivot.SetTR(new Vector2(-3.376f, 0.8038f), armAngle);
            LeftPivot.DrawOffset = new Vector2(0.7074f, 0.1286f);

            RightPivot.SetTR(new Vector2(-3.376f, -0.8038f), -armAngle);
            RightPivot.DrawOffset = new Vector2(0.7074f, -0.1286f);

            BarLeft.SetTR(new Vector2(0.7717f, -0.3536f), 0f);
            BarLeft.DrawOffset  = Vector2.Lerp(new Vector2(-0.772f, 0.322f), new Vector2(-0.9f, -0.03f), realLerp);
            
            BarRight.SetTR(new Vector2(0.7717f, 0.3536f), 0f);
            BarRight.DrawOffset = Vector2.Lerp(new Vector2(-0.772f, -0.322f), new Vector2(-0.9f, 0.03f), realLerp);

            Top.Draw(this);
            Cables.Draw(this);

            float beamLerp = 1f - Mathf.InverseLerp(FINISH_FIRE, FINISH_FIRE + 30, FireTicks);
            if (FireTicks >= FINISH_FIRE && beamLerp > 0f)
            {
                Color color = new Color32(255, 210, 61, 255);
                color.a = beamLerp;

                float rot = TurretRotation * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rot), 0f, Mathf.Sin(rot)) * 500f;
                Vector3 pos = DrawPos + dir;

                Content.CoilgunBeam.MatSouth.color = color;
                //Content.CoilgunBeam.MatSouth.SetTextureOffset("_MainTex", new Vector2(-BeamPct, 0));
                Content.CoilgunBeam.Draw(pos, Rot4.North, this, -TurretRotation);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var item in base.GetGizmos())
                yield return item;

            GetCapacitorState(out int capCount, out float capPower);
            bool hasPower = PowerComp.PowerOn;
            bool hasShell = HasLoadedShell();

            yield return new Command_TargetCustom()
            {
                defaultLabel = "RF.Coilgun.AttackLabel".Translate(),
                defaultDesc = "RF.Coilgun.AttackDesc".Translate(),
                targetingParams = new TargetingParameters()
                {
                    canTargetLocations = true
                },
                action = (target, _) =>
                {
                    if (!target.IsValid)
                        return;

                    Core.Log($"Started coilgun attacking '{target}'");
                    CurrentState = State.Readying;
                    FireTicks = 0;
                    CurrentTargetInfo = target;
                    LastKnowPos = Position;
                },
                icon = Content.CoilgunShootIcon,
                defaultIconColor = new Color32(252, 194, 3, 255),
                user = this,
                disabled = capCount <= 0 || capPower < Settings.CoilgunBasePowerReq || !hasPower || !hasShell,
                disabledReason = !hasPower ? "RF.Coilgun.DisabledNoPower".Translate() : !hasShell ? "RF.Coilgun.DisabledNoShell".Translate() : "RF.Coilgun.DisabledNoCaps".Translate()
            };
            yield return new Command_Action()
            {
                defaultLabel = "RF.Coilgun.ChangeShellLabel".Translate(),
                defaultDesc = "RF.Coilgun.ChangeShellDesc".Translate(),
                action = () =>
                {
                    Func<ThingDef, string> labelGetter  = shell => shell.LabelCap;
                    Func<ThingDef, Action> actionGetter = shell =>
                    {
                        if (shell == CurrentShellDef)
                            return null;
                        return () =>
                        {
                            EjectLoadedShell();
                            CurrentShellDef = (CoilgunShellDef) shell;
                        };
                    };
                    FloatMenuUtility.MakeMenu(ShellDefs, labelGetter, actionGetter);
                },
                icon = CurrentShellDef?.uiIcon
            };
            yield return new Command_Target()
            {
                defaultLabel = "check props",
                targetingParams = new TargetingParameters()
                {
                    canTargetLocations = true
                },
                action = t =>
                {
                    Thing thing = t.Thing;

                    if (thing is Building_Coilgun coilgun)
                    {
                        Log.Warning($"Other: {coilgun.CurrentShellDef}, self: {this.CurrentShellDef}, Equal props: {coilgun.GetComp<CompRefuelable>().Props == this.GetComp<CompRefuelable>().Props}, equal comp: {coilgun.GetComp<CompRefuelable>() == this.GetComp<CompRefuelable>()}");
                    }
                }
            };
        }

        public override string GetInspectString()
        {
            string basic = base.GetInspectString();
            GetCapacitorState(out int connected, out float stored);

            string capState = "RF.Coilgun.CapState".Translate((stored * 100f).ToString("F0"), connected, (Settings.CoilgunBasePowerReq * 100f).ToString("F0"));

            if(!string.IsNullOrWhiteSpace(basic))
                return $"{basic.TrimEnd()}\n{capState}";
            return capState;
        }

        public void OnStartTargeting(int _)
        {
            DrawAffectedCells = true;
        }

        public void OnStopTargeting(int _)
        {
            DrawAffectedCells = false;
        }

        public void SetTargetInfo(LocalTargetInfo info, int _)
        {
            CurrentTargetInfo = info;
        }
    }

    public class Command_TargetCustom : Command
    {
        public Action<LocalTargetInfo, int> action;
        public TargetingParameters targetingParams;
        public ICustomTargetingUser user;
        public int times = 1;
        public Func<bool> continueCheck;

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            StartTargeting(0);
        }

        private void StartTargeting(int index)
        {
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            user.OnStartTargeting(index);
            Find.Targeter.BeginTargeting(targetingParams, t => action(t, index), targ =>
            {
                if (targ.IsValid)
                {
                    user.SetTargetInfo(targ, index);
                    GenDraw.DrawTargetHighlight(targ);
                }
            }, null, actionWhenFinished: () =>
            {
                user.OnStopTargeting(index);
                int nextIndex = index + 1;
                if (nextIndex < times && (continueCheck?.Invoke() ?? true))
                {
                    Patch_Targeter_StopTargeting.PerformOnce = () =>
                    {
                        StartTargeting(nextIndex);
                    };
                }
            });
        }

        public override bool InheritInteractionsFrom(Gizmo other) => false;
    }
}
