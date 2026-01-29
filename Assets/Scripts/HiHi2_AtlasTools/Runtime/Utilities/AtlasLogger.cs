using UnityEngine;

namespace HiHi2.AtlasTools
{
    public static class AtlasLogger
    {
        private const string PREFIX = AtlasConstants.LOG_PREFIX;
        
        public static void Log(string message)
        {
            Debug.Log($"{PREFIX} {message}");
        }
        
        public static void LogWarning(string message)
        {
            Debug.LogWarning($"{PREFIX} {message}");
        }
        
        public static void LogError(string message)
        {
            Debug.LogError($"{PREFIX} {message}");
        }
        
        public static void LogFormat(string format, params object[] args)
        {
            Debug.Log($"{PREFIX} {string.Format(format, args)}");
        }
    }
}