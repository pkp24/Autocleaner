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
            Log.Message("[Autocleaner] PawnAutocleaner constructor called");
            
            if (relations == null) 
            {
                Log.Message("[Autocleaner] Creating relations tracker");
                relations = new Pawn_RelationsTracker(this);
            }
            if (thinker == null) 
            {
                Log.Message("[Autocleaner] Creating thinker");
                thinker = new Pawn_Thinker(this);
            }
            lastJobTick = 0;
            
            // Ensure training tracker is never created for autocleaners
            if (training != null)
            {
                Log.Message("[Autocleaner] Removing training tracker");
                training = null;
            }
            
            // Initialize charge if not set and def is available
            if (charge <= 0 && def != null && AutoDef != null)
            {
                Log.Message($"[Autocleaner] Initializing charge to {AutoDef.charge}");
                charge = AutoDef.charge;
            }
            else if (def == null)
            {
                Log.Warning("[Autocleaner] PawnAutocleaner constructor: def is null - this may cause issues");
            }
            else if (AutoDef == null)
            {
                Log.Warning("[Autocleaner] PawnAutocleaner constructor: AutoDef is null - def may not be AutocleanerDef");
            }
            
            Log.Message($"[Autocleaner] PawnAutocleaner constructor completed for {def?.defName ?? "NULL"}");
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
            Log.Message($"[Autocleaner] StartCharging called for {LabelShort ?? "NULL"}");
            
            StopCharging();

            if (Dead) 
            {
                Log.Warning("[Autocleaner] StartCharging: Pawn is dead");
                return;
            }
            if (Map == null) 
            {
                Log.Warning("[Autocleaner] StartCharging: Map is null");
                return;
            }
            if (AutoDef == null) 
            {
                Log.Warning("[Autocleaner] StartCharging: AutoDef is null");
                return;
            }
            if (AutoDef.charger == null) 
            {
                Log.Warning("[Autocleaner] StartCharging: AutoDef.charger is null");
                return;
            }

            Log.Message($"[Autocleaner] StartCharging: Spawning charger at {Position}");
            charger = GenSpawn.Spawn(AutoDef.charger, Position, Map);
            Log.Message($"[Autocleaner] StartCharging: Charger spawned: {charger != null}");
        }

        public void StopCharging()
        {
            if (charger == null) return;

            Log.Message($"[Autocleaner] StopCharging: Destroying charger for {LabelShort ?? "NULL"}");
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
                        Log.Message(def + " " + (def as AutocleanerDef));
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
            
            Log.Message($"[Autocleaner] SpawnSetup called for {LabelShort ?? "NULL"}");
            
            // Ensure we have a proper def
            if (def == null)
            {
                Log.Error("[Autocleaner] SpawnSetup: def is null!");
                return;
            }
            
            // Initialize charge if not set
            if (charge <= 0 && AutoDef != null)
            {
                Log.Message($"[Autocleaner] SpawnSetup: Initializing charge to {AutoDef.charge}");
                charge = AutoDef.charge;
            }
            
            // Ensure training tracker is never created
            if (training != null)
            {
                Log.Message("[Autocleaner] SpawnSetup: Removing training tracker");
                training = null;
            }
        }
    }
}
