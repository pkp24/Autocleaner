﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Autocleaner
{
    public class JobGiverAutocleaner : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            DebugLog.Message($"[Autocleaner] JobGiverAutocleaner.TryGiveJob called for {pawn?.LabelShort ?? "NULL"}");
            
            PawnAutocleaner cleaner = pawn as PawnAutocleaner;
            if (cleaner == null) 
            {
                DebugLog.Warning("[Autocleaner] JobGiverAutocleaner: Pawn is not PawnAutocleaner");
                return null;
            }
            if (!cleaner.active) 
            {
                DebugLog.Message("[Autocleaner] JobGiverAutocleaner: Cleaner is not active");
                return null;
            }
            if (!cleaner.Spawned)
            {
                DebugLog.Warning("[Autocleaner] JobGiverAutocleaner: Cleaner is not spawned");
                return null;
            }
            if (cleaner.LowPower) 
            {
                DebugLog.Message("[Autocleaner] JobGiverAutocleaner: Cleaner has low power");
                return null;
            }
            if (cleaner.health?.summaryHealth?.SummaryHealthPercent < 1) 
            {
                DebugLog.Message("[Autocleaner] JobGiverAutocleaner: Cleaner is damaged");
                return null;
            }
            
            // Don't clean if completely out of power - prioritize finding power
            if (cleaner.charge <= 0) 
            {
                DebugLog.Message("[Autocleaner] JobGiverAutocleaner: Cleaner has no charge");
                return null;
            }
            
            // Don't give a new job if the pawn already has one
            if (pawn.CurJob != null) 
            {
                DebugLog.Message("[Autocleaner] JobGiverAutocleaner: Pawn already has a job");
                return null;
            }
            
            // Add a small cooldown to prevent job spam
            if (cleaner.lastJobTick > 0 && Find.TickManager.TicksGame - cleaner.lastJobTick < 10) 
            {
                DebugLog.Message("[Autocleaner] JobGiverAutocleaner: Job cooldown active");
                return null;
            }

            WorkGiverCleanFilth scanner = Globals.AutocleanerCleanFilth?.Worker as WorkGiverCleanFilth;
            if (scanner == null) 
            {
                DebugLog.Warning("[Autocleaner] JobGiverAutocleaner: WorkGiverCleanFilth is null");
                return null;
            }

            try
            {
                IEnumerable<Thing> enumerable = scanner.PotentialWorkThingsGlobal(pawn).Where(x => scanner.HasJobOnThing(pawn, x));
                Danger maxDanger = (pawn.playerSettings != null && pawn.playerSettings.UsesConfigurableHostilityResponse && pawn.playerSettings.hostilityResponse != HostilityResponseMode.Flee) ? Danger.Deadly : Danger.None;
                Thing thing = GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, enumerable, scanner.PathEndMode, TraverseParms.For(pawn, maxDanger, TraverseMode.ByPawn, false), 9999f);
                if (thing == null) 
                {
                    DebugLog.Message("[Autocleaner] JobGiverAutocleaner: No filth found to clean");
                    return null;
                }

                Job job = scanner.JobOnThing(pawn, thing, false);
                if (job != null)
                {
                    cleaner.lastJobTick = Find.TickManager.TicksGame;
                    DebugLog.Message($"[Autocleaner] JobGiverAutocleaner: Created cleaning job for {pawn.LabelShort}");
                }
                return job;
            }
            catch (Exception ex)
            {
                DebugLog.Error($"[Autocleaner] JobGiverAutocleaner.TryGiveJob failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return null;
            }
        }
    }
}
