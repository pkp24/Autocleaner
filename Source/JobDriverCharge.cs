using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Autocleaner
{
    public class JobDriverCharge : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        private PawnAutocleaner Cleaner { get { return pawn as PawnAutocleaner; } }
        private AutocleanerJobDef JobDef { get { return job.def as AutocleanerJobDef; } }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Log.Message($"[Autocleaner] JobDriverCharge.MakeNewToils called for pawn: {pawn?.LabelShort ?? "NULL"}");
            
            if (Cleaner == null)
            {
                Log.Warning("[Autocleaner] JobDriverCharge: Cleaner is null");
                EndJobWith(JobCondition.Incompletable);
                yield break;
            }
            if (JobDef == null)
            {
                Log.Warning("[Autocleaner] JobDriverCharge: JobDef is null");
                EndJobWith(JobCondition.Incompletable);
                yield break;
            }
            if (Cleaner.AutoDef == null)
            {
                Log.Warning("[Autocleaner] JobDriverCharge: Cleaner.AutoDef is null");
                EndJobWith(JobCondition.Incompletable);
                yield break;
            }
            if (Map == null)
            {
                Log.Warning("[Autocleaner] JobDriverCharge: Map is null - autocleaner not properly spawned yet");
                EndJobWith(JobCondition.Incompletable);
                yield break;
            }
            if (!pawn.Spawned)
            {
                Log.Warning("[Autocleaner] JobDriverCharge: Pawn is not spawned");
                EndJobWith(JobCondition.Incompletable);
                yield break;
            }

            Log.Message($"[Autocleaner] JobDriverCharge: All checks passed, creating toil for {pawn.LabelShort}");

            int ticksWaitedForPower = 0;
            yield return new Toil()
            {
                initAction = delegate ()
                {
                    Log.Message($"[Autocleaner] JobDriverCharge.initAction called for {pawn?.LabelShort ?? "NULL"}");
                    
                    if (Cleaner == null)
                    {
                        Log.Warning("[Autocleaner] JobDriverCharge.initAction: Cleaner is null");
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    if (JobDef == null)
                    {
                        Log.Warning("[Autocleaner] JobDriverCharge.initAction: JobDef is null");
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    if (Cleaner.AutoDef == null)
                    {
                        Log.Warning("[Autocleaner] JobDriverCharge.initAction: Cleaner.AutoDef is null");
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    if (Map == null)
                    {
                        Log.Warning("[Autocleaner] JobDriverCharge.initAction: Map is null");
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    if (!pawn.Spawned)
                    {
                        Log.Warning("[Autocleaner] JobDriverCharge.initAction: Pawn is not spawned");
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    
                    try
                    {
                        Log.Message($"[Autocleaner] JobDriverCharge.initAction: Attempting to reserve position for {pawn.LabelShort}");
                        
                        if (Map.pawnDestinationReservationManager != null)
                        {
                            Map.pawnDestinationReservationManager.Reserve(pawn, job, pawn.Position);
                            Log.Message("[Autocleaner] JobDriverCharge.initAction: Position reserved successfully");
                        }
                        else
                        {
                            Log.Warning("[Autocleaner] JobDriverCharge.initAction: pawnDestinationReservationManager is null");
                        }
                        
                        if (pawn.pather != null)
                        {
                            pawn.pather.StopDead();
                            Log.Message("[Autocleaner] JobDriverCharge.initAction: Pather stopped");
                        }
                        else
                        {
                            Log.Warning("[Autocleaner] JobDriverCharge.initAction: Pather is null");
                        }
                        
                        Log.Message($"[Autocleaner] JobDriverCharge.initAction: Starting charging for {pawn.LabelShort}");
                        Cleaner.StartCharging();
                        Log.Message("[Autocleaner] JobDriverCharge.initAction: Charging started successfully");
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[Autocleaner] JobDriverCharge.initAction failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
                        EndJobWith(JobCondition.Incompletable);
                    }
                },
                tickAction = delegate ()
                {
                    if (Cleaner == null || JobDef == null || Cleaner.AutoDef == null)
                    {
                        Log.Warning("[Autocleaner] JobDriverCharge.tickAction: Required components are null");
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    if (Cleaner.charger == null)
                    {
                        Log.Warning("[Autocleaner] JobDriverCharge.tickAction: Charger is null");
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    CompPowerTrader comp = Cleaner.charger.TryGetComp<CompPowerTrader>();
                    if (comp == null || !comp.PowerOn)
                    {
                        if(ticksWaitedForPower++ > 20)
                        {
                            Log.Message($"[Autocleaner] JobDriverCharge.tickAction: No power after {ticksWaitedForPower} ticks, attempting to move to power");
                            // Instead of ending the job, try to move to find power
                            if (Map != null && Map.powerNetManager != null)
                            {
                                // Look for a power source within range
                                CompPower bestTransmitter = PowerConnectionMaker.BestTransmitterForConnector(pawn.Position, Map);
                                if (bestTransmitter != null && bestTransmitter.PowerNet != null && bestTransmitter.PowerNet.HasActivePowerSource)
                                {
                                    // There's power nearby, try to move to it
                                    IntVec3 powerPos = bestTransmitter.parent.Position;
                                    if (pawn.CanReach(powerPos, PathEndMode.OnCell, Danger.Deadly))
                                    {
                                        Log.Message($"[Autocleaner] JobDriverCharge.tickAction: Moving to power at {powerPos}");
                                        pawn.jobs.StartJob(JobMaker.MakeJob(Globals.AutocleanerCharge, powerPos), JobCondition.InterruptForced);
                                        return;
                                    }
                                }
                            }
                            Log.Warning("[Autocleaner] JobDriverCharge.tickAction: No reachable power found, ending job");
                            EndJobWith(JobCondition.Incompletable);
                        }
                        return;
                    }
                    Cleaner.charge += Math.Abs(JobDef.activeDischargePerSecond) / Cleaner.AutoDef.dischargePeriodTicks;
                    if (Cleaner.charge >= Cleaner.AutoDef.charge)
                    {
                        Cleaner.charge = Cleaner.AutoDef.charge;
                        Log.Message($"[Autocleaner] JobDriverCharge.tickAction: Charging complete for {pawn.LabelShort}");
                        EndJobWith(JobCondition.Succeeded);
                        return;
                    }
                },
                finishActions = new List<Action>() { delegate () {
                    Log.Message($"[Autocleaner] JobDriverCharge.finishActions: Stopping charging for {pawn?.LabelShort ?? "NULL"}");
                    if (Cleaner != null) Cleaner.StopCharging();
                } },
                defaultCompleteMode = ToilCompleteMode.Never,
            };
            yield break;
        }
    }
}
