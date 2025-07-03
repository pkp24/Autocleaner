using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Autocleaner
{
    public class AutocleanerSettings : ModSettings
    {
        public bool lowQualityPathing = false;
        public bool disableSchedule = false;
        public bool enableDebugLogging = false;

        override public void ExposeData()
        {
            Scribe_Values.Look(ref lowQualityPathing, "lowQualityPathing", false);
            Scribe_Values.Look(ref disableSchedule, "disableSchedule", false);
            Scribe_Values.Look(ref enableDebugLogging, "enableDebugLogging", false);
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.Begin(inRect);
            listing_Standard.CheckboxLabeled("AutocleanerLQPathingName".Translate(), ref lowQualityPathing, "AutocleanerLQPathingDesc".Translate());
            listing_Standard.CheckboxLabeled("AutocleanerDisableScheduleName".Translate(), ref disableSchedule, "AutocleanerDisableScheduleDesc".Translate());
            listing_Standard.CheckboxLabeled("AutocleanerEnableDebugLoggingName".Translate(), ref enableDebugLogging, "AutocleanerEnableDebugLoggingDesc".Translate());
            listing_Standard.End();
        }
    }

}
