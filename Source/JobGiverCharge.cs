using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using RimWorld;

namespace Autocleaner
{
    public class JobGiverCharge : ThinkNode_JobGiver
    {
                protected override Job TryGiveJob(Pawn pawn)
        {
            DebugLog.Message($"[Autocleaner] JobGiverCharge.TryGiveJob called for {pawn?.LabelShort ?? "NULL"}");
            
            PawnAutocleaner cleaner = pawn as PawnAutocleaner;
            if (cleaner == null) 
            {
                DebugLog.Warning("[Autocleaner] JobGiverCharge: Pawn is not PawnAutocleaner");
                return null;
            }
            if (!cleaner.active) 
            {
                DebugLog.Message("[Autocleaner] JobGiverCharge: Cleaner is not active");
                return null;
            }
            if (cleaner.charge >= cleaner.AutoDef.charge) 
            {
                DebugLog.Message($"[Autocleaner] JobGiverCharge: Cleaner has full charge ({cleaner.charge}/{cleaner.AutoDef.charge})");
                return null;
            }
            if (cleaner.Map == null) 
            {
                DebugLog.Warning("[Autocleaner] JobGiverCharge: Map is null - autocleaner not properly spawned yet");
                return null;
            }
            if (!cleaner.Spawned) 
            {
                DebugLog.Warning("[Autocleaner] JobGiverCharge: Cleaner is not spawned");
                return null;
            }
            if (cleaner.Map.powerNetManager == null) 
            {
                DebugLog.Warning("[Autocleaner] JobGiverCharge: powerNetManager is null");
                return null;
            }
            
            DebugLog.Message($"[Autocleaner] JobGiverCharge: Creating charging job for {pawn.LabelShort} (charge: {cleaner.charge}/{cleaner.AutoDef.charge})");
            // Always try to charge if we need it, regardless of current power availability
            // The JobDriverCharge will handle the case where there's no power
            Job job = JobMaker.MakeJob(Globals.AutocleanerCharge);
            return job;
        }
    }
}
