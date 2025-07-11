﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Autocleaner
{
    public class JobGiverGotoCorner : ThinkNode_JobGiver
    {
        public bool SuitablePosition(IntVec3 pos, Map map, bool fallback = false)
        {
            if (!pos.InBounds(map)) return false;
            if (!pos.Roofed(map)) return false;
            if (!pos.Standable(map)) return false;

            if (!fallback)
            {
                int v = 0;
                int h = 0;
                if (new IntVec3(pos.x + 1, pos.y, pos.z + 0).Impassable(map)) { v++; }
                if (new IntVec3(pos.x - 1, pos.y, pos.z + 0).Impassable(map)) { v++; }
                if (new IntVec3(pos.x + 0, pos.y, pos.z + 1).Impassable(map)) { h++; }
                if (new IntVec3(pos.x + 0, pos.y, pos.z - 1).Impassable(map)) { h++; }

                if (v < 1 || h < 1) return false;
            }

            CompPower comp = PowerConnectionMaker.BestTransmitterForConnector(pos, map);
            if (comp == null || comp.PowerNet == null) return false;

            return comp.PowerNet.HasActivePowerSource;
        }

        static TraverseParms traverseParams = TraverseParms.For(TraverseMode.NoPassClosedDoors);

        IntVec3 FindSuitablePosition(Pawn pawn, Map map, IntVec3 pos)
        {
            IntVec3 target = IntVec3.Invalid;
            PathGrid pathGrid = map.pathing.For(traverseParams).pathGrid;
            CellIndices cellIndices = map.cellIndices;
            Predicate<IntVec3> passCheck = delegate (IntVec3 c)
            {
                if (c.GetTerrain(map).IsWater) return false;
                if (!pathGrid.WalkableFast(cellIndices.CellToIndex(c))) return false;

                return true;
            };
            Func<IntVec3, bool> processor = delegate (IntVec3 x)
            {
                if (!SuitablePosition(x, map)) return false;

                target = x;
                return true;
            };

            map.floodFiller.FloodFill(pos, passCheck, processor, 3000);
            if (target != IntVec3.Invalid) return target;

            return FindSuitablePositionRandom(pawn, map, pos);
        }

        IntVec3 FindSuitablePositionRandom(Pawn pawn, Map map, IntVec3 pos)
        {
            IntVec3 target;
            if (!RCellFinder.TryFindRandomCellNearWith(pos, x => SuitablePosition(x, map) && pawn.CanReach(x, PathEndMode.OnCell, Danger.Deadly), map, out target))
            {
                if (!RCellFinder.TryFindRandomCellNearWith(pos, x => SuitablePosition(x, map, true) && pawn.CanReach(x, PathEndMode.OnCell, Danger.Deadly), map, out target))
                {
                    return IntVec3.Invalid;
                }
            }

            return target;
        }
        protected override Job TryGiveJob(Pawn pawn)
        {
            PawnAutocleaner cleaner = pawn as PawnAutocleaner;
            if (cleaner == null) return null;
            if (!cleaner.active) return null;

            Map map = pawn.Map;
            if (map == null) return null;
            
            // If we're at a suitable position (has power), don't move
            if (SuitablePosition(pawn.Position, map)) return null;
            
            // If we have plenty of power and we're not at a suitable position, 
            // we might want to move to a better position, but it's not urgent
            if (cleaner.charge > cleaner.AutoDef.charge * 0.5f) return null;

            IntVec3 target = Autocleaner.settings.lowQualityPathing ? FindSuitablePositionRandom(pawn, map, pawn.Position) : FindSuitablePosition(pawn, map, pawn.Position);
            if (target != IntVec3.Invalid)
            {
                Job job = JobMaker.MakeJob(Globals.AutocleanerGoto, target);
                return job;
            }

            return null;
        }
    }
}