using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Autocleaner
{
    [StaticConstructorOnStartup]
    public class PawnAutocleaner : Pawn
    {
        static public float lowLower = 0.25f;

        public AutocleanerDef AutoDef { get { return def as AutocleanerDef; } }

        public bool active = true;
        public float charge;
        public int lastJobTick;

        public bool Broken {
            get {
                if (health != null && health.hediffSet != null && health.hediffSet.hediffs != null) {
                    int badCount = 0;
                    foreach (var x in health.hediffSet.hediffs) {
                        if (x.def.isBad) badCount++;
                    }
                    return badCount > 0;
                }
                return false;
            }
        }
        public bool LowPower {
            get {
                var autoDef = AutoDef;
                if (autoDef != null) {
                    return charge < autoDef.charge * lowLower;
                }
                return false;
            }
        }
        public Thing charger = null;

        public PawnAutocleaner()
        {
            DebugLog.Message("[Autocleaner] PawnAutocleaner constructor called");
            
            if (relations == null) 
            {
                DebugLog.Message("[Autocleaner] Creating relations tracker");
                relations = new Pawn_RelationsTracker(this);
            }
            if (thinker == null) 
            {
                DebugLog.Message("[Autocleaner] Creating thinker");
                thinker = new Pawn_Thinker(this);
            }
            lastJobTick = 0;
            
            // Ensure training tracker is never created for autocleaners
            if (training != null)
            {
                DebugLog.Message("[Autocleaner] Removing training tracker");
                training = null;
            }
            
            // Clamp charge to valid range. New autocleaners start at 0% charge
            if (def != null && AutoDef != null)
            {
                if (charge < 0)
                    charge = 0;
                if (charge > AutoDef.charge)
                    charge = AutoDef.charge;

                DebugLog.Message($"[Autocleaner] Initializing charge to {charge}");
            }
            else if (def == null)
            {
                DebugLog.Warning("[Autocleaner] PawnAutocleaner constructor: def is null - this may cause issues");
            }
            else if (AutoDef == null)
            {
                DebugLog.Warning("[Autocleaner] PawnAutocleaner constructor: AutoDef is null - def may not be AutocleanerDef");
            }
            
            DebugLog.Message($"[Autocleaner] PawnAutocleaner constructor completed for {def?.defName ?? "NULL"}");
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref active, "autocleanerActive");
            Scribe_Values.Look(ref charge, "autocleanerCharge");
            Scribe_Values.Look(ref lastJobTick, "autocleanerLastJobTick");
        }

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);

            if(jobs!=null && !Dead) jobs.StopAll(false, true);
            
            // Clean up charger if we're dead
            if (Dead) StopCharging();
        }

        public void StartCharging()
        {
            DebugLog.Message($"[Autocleaner] StartCharging called for {LabelShort ?? "NULL"}");
            
            StopCharging();

            if (Dead) 
            {
                DebugLog.Warning("[Autocleaner] StartCharging: Pawn is dead");
                return;
            }
            if (Map == null) 
            {
                DebugLog.Warning("[Autocleaner] StartCharging: Map is null");
                return;
            }
            if (AutoDef == null) 
            {
                DebugLog.Warning("[Autocleaner] StartCharging: AutoDef is null");
                return;
            }
            if (AutoDef.charger == null) 
            {
                DebugLog.Warning("[Autocleaner] StartCharging: AutoDef.charger is null");
                return;
            }

            DebugLog.Message($"[Autocleaner] StartCharging: Spawning charger at {Position}");
            charger = GenSpawn.Spawn(AutoDef.charger, Position, Map);
            DebugLog.Message($"[Autocleaner] StartCharging: Charger spawned: {charger != null}");
        }

        public void StopCharging()
        {
            if (charger == null) return;

            DebugLog.Message($"[Autocleaner] StopCharging: Destroying charger for {LabelShort ?? "NULL"}");
            if(charger.Spawned) charger.Destroy();
            charger = null;
        }

        protected override void Tick()
        {
            base.Tick();

            // Don't process ticks if we're dead
            if (Dead) return;

            // Remove any toxic hediffs that might have been applied (check every 60 ticks = once per second)
            if (this.IsHashIntervalTick(60) && health != null && health.hediffSet != null && health.hediffSet.hediffs != null && health.hediffSet.hediffs.Count > 0)
            {
                var toxicHediffs = health.hediffSet.hediffs.Where(h => 
                    h.def.defName.Contains("Toxic") || 
                    h.def.defName.Contains("Poison") ||
                    h.def.defName.Contains("Toxicity") ||
                    h.def.defName.Contains("ToxicBuildup")).ToList();
                
                foreach (var hediff in toxicHediffs)
                {
                    health.RemoveHediff(hediff);
                }
            }

            if (!this.IsHashIntervalTick(AutoDef.dischargePeriodTicks)) return;

            AutocleanerJobDef job = CurJobDef as AutocleanerJobDef;
            if (job == null) return;

            charge -= job.dischargePerSecond * GenTicks.TicksToSeconds(AutoDef.dischargePeriodTicks);
            if (charge < 0) charge = 0;
            if (charge > AutoDef.charge) charge = AutoDef.charge;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            // Don't show gizmos if we're dead
            if (Dead)
            {
                foreach (Gizmo gizmo in base.GetGizmos())
                {
                    yield return gizmo;
                }
                yield break;
            }

            if (Find.Selector.SingleSelectedThing == this)
            {
                yield return new GizmoAutocleaner
                {
                    cleaner = this
                };
            }

            yield return new Command_Action
            {
                defaultLabel = "AutocleanerDeactivate".Translate(),
                defaultDesc = "AutocleanerDeactivateDesc".Translate(),
                action = delegate
                {
                    Thing thing = GenSpawn.Spawn(Globals.AutocleanerItem, Position, Map);
                    CompStoreAutocleaner comp = thing.TryGetComp<CompStoreAutocleaner>();
                    if (comp != null)
                    {
                        comp.containedThing = this;
                    }

                    DeSpawn();

                    Find.Selector.SelectedObjects.Add(thing);
                },
                icon = iconUninstall,
            };

            if (active)
            {
                yield return new Command_Action
                {
                    defaultLabel = "AutocleanerPause".Translate(),
                    defaultDesc = "AutocleanerPauseDesc".Translate(),
                    action = delegate
                    {
                        DebugLog.Message(def + " " + (def as AutocleanerDef));
                        jobs.StopAll(false, true);
                        active = !active;
                    },
                    icon = iconPause,
                };
            }
            else
            {
                yield return new Command_Action
                {
                    defaultLabel = "AutocleanerResume".Translate(),
                    defaultDesc = "AutocleanerResumeDesc".Translate(),
                    action = delegate
                    {
                        active = !active;
                    },
                    icon = iconResume,
                };
            }

            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            yield break;
        }

        public override string GetInspectString()
        {
            if (charger == null || Dead) return base.GetInspectString();

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());
            if (stringBuilder.Length > 0) stringBuilder.AppendLine();
            stringBuilder.Append(charger.GetInspectString());
            return stringBuilder.ToString();
        }


        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);

            // Don't draw overlays if we're dead (corpse) or if Map is null
            if (Dead || Map == null || Map.overlayDrawer == null) return;

            OverlayTypes overlay = OverlayTypes.Forbidden;

            if (Broken) overlay = OverlayTypes.BrokenDown;
            else if (charger != null && CurJobDef == Globals.AutocleanerCharge) overlay = OverlayTypes.NeedsPower;
            else if (LowPower) overlay = OverlayTypes.PowerOff;

            if (overlay != OverlayTypes.Forbidden) Map.overlayDrawer.DrawOverlay(this, overlay);
        }

        static Texture2D iconUninstall = ContentFinder<Texture2D>.Get("UI/Designators/Uninstall", true);
        static Texture2D iconPause = ContentFinder<Texture2D>.Get("Autocleaner/Pause", true);
        static Texture2D iconResume = ContentFinder<Texture2D>.Get("Autocleaner/Resume", true);

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            
            DebugLog.Message($"[Autocleaner] SpawnSetup called for {LabelShort ?? "NULL"}");
            
            // Ensure we have a proper def
            if (def == null)
            {
                DebugLog.Error("[Autocleaner] SpawnSetup: def is null!");
                return;
            }
            
            // Clamp charge when spawning
            if (AutoDef != null)
            {
                if (charge < 0)
                    charge = 0;
                if (charge > AutoDef.charge)
                    charge = AutoDef.charge;

                DebugLog.Message($"[Autocleaner] SpawnSetup: charge set to {charge}");
            }
            
            // Ensure training tracker is never created
            if (training != null)
            {
                DebugLog.Message("[Autocleaner] SpawnSetup: Removing training tracker");
                training = null;
            }

            // If spawned with no charge, immediately look for a charging spot
            if (map != null && charge <= 0)
            {
                IntVec3 dest = FindChargingSpot(map, Position);
                if (dest.IsValid && dest != Position)
                {
                    DebugLog.Message($"[Autocleaner] SpawnSetup: Moving to charging spot at {dest}");
                    jobs?.StartJob(JobMaker.MakeJob(Globals.AutocleanerGoto, dest), JobCondition.InterruptForced);
                }
                else
                {
                    DebugLog.Message("[Autocleaner] SpawnSetup: Starting charge at current position");
                    jobs?.StartJob(JobMaker.MakeJob(Globals.AutocleanerCharge), JobCondition.InterruptForced);
                }
            }
        }

        // Look for a nearby powered corner to charge at
        static TraverseParms traverseParamsSpawn = TraverseParms.For(TraverseMode.NoPassClosedDoors);
        IntVec3 FindChargingSpot(Map map, IntVec3 start)
        {
            if (map == null) return IntVec3.Invalid;

            IntVec3 target = IntVec3.Invalid;
            PathGrid pathGrid = map.pathing.For(traverseParamsSpawn).pathGrid;
            CellIndices cellIndices = map.cellIndices;
            Predicate<IntVec3> passCheck = c =>
            {
                if (c.GetTerrain(map).IsWater) return false;
                if (!pathGrid.WalkableFast(cellIndices.CellToIndex(c))) return false;
                return true;
            };
            Func<IntVec3, bool> processor = x =>
            {
                if (!ValidChargingCell(x, map)) return false;
                target = x;
                return true;
            };

            map.floodFiller.FloodFill(start, passCheck, processor, 3000);
            if (target != IntVec3.Invalid) return target;

            if (RCellFinder.TryFindRandomCellNearWith(start, x => ValidChargingCell(x, map) && this.CanReach(x, PathEndMode.OnCell, Danger.Deadly), map, out target))
                return target;

            if (RCellFinder.TryFindRandomCellNearWith(start, x => ValidChargingCell(x, map, true) && this.CanReach(x, PathEndMode.OnCell, Danger.Deadly), map, out target))
                return target;

            return IntVec3.Invalid;
        }

        bool ValidChargingCell(IntVec3 pos, Map map, bool fallback = false)
        {
            if (!pos.InBounds(map)) return false;
            if (!pos.Roofed(map)) return false;
            if (!pos.Standable(map)) return false;

            if (!fallback)
            {
                int v = 0;
                int h = 0;
                if (new IntVec3(pos.x + 1, pos.y, pos.z + 0).Impassable(map)) v++;
                if (new IntVec3(pos.x - 1, pos.y, pos.z + 0).Impassable(map)) v++;
                if (new IntVec3(pos.x + 0, pos.y, pos.z + 1).Impassable(map)) h++;
                if (new IntVec3(pos.x + 0, pos.y, pos.z - 1).Impassable(map)) h++;

                if (v < 1 || h < 1) return false;
            }

            CompPower comp = PowerConnectionMaker.BestTransmitterForConnector(pos, map);
            if (comp == null || comp.PowerNet == null) return false;

            return comp.PowerNet.HasActivePowerSource;
        }
    }
}
