namespace Autocleaner
{
    static class DebugLog
    {
        public static void Message(string x)
        {
            if (Autocleaner.settings.enableDebugLogging)
            {
                Verse.Log.Message(x);
            }
        }

        public static void Warning(string x)
        {
            if (Autocleaner.settings.enableDebugLogging)
            {
                Verse.Log.Warning(x);
            }
        }

        public static void Error(string x)
        {
            if (Autocleaner.settings.enableDebugLogging)
            {
                Verse.Log.Error(x);
            }
        }
    }
} 