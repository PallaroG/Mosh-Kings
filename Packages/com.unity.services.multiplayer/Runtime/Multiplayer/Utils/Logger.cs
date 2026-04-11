using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.Services.Multiplayer
{
    internal class Logger
    {
        const string k_Tag = "[Multiplayer]";
        const string k_UnityAssertions = "UNITY_ASSERTIONS";
        const string k_VerboseLoggingDefine =
            "ENABLE_UNITY_MULTIPLAYER_VERBOSE_LOGGING";

        static void Log(LogType level, object message)
        {
            Debug.unityLogger.Log(level, k_Tag, message);
        }

        public static void Log(object message)
        {
            Log(LogType.Log, message);
        }

        public static void LogWarning(object message)
        {
            Log(LogType.Warning, message);
        }

        public static void LogCallWarning(string enclosingType, string message, [CallerMemberName] string method = "")
        {
            LogWarning($"{enclosingType}.{method}: {message}");
        }

        public static void LogError(object message)
        {
            Log(LogType.Error, message);
        }

        public static void LogCallError(string enclosingType, string message, [CallerMemberName] string method = "")
        {
            LogError($"{enclosingType}.{method}: {message}");
        }

        public static void LogException(Exception exception)
        {
            Log(LogType.Exception, exception);
        }

        [Conditional(k_UnityAssertions)]
        public static void LogAssertion(object message)
        {
            Log(LogType.Assert, message);
        }

        [Conditional(k_VerboseLoggingDefine)]
        public static void LogVerbose(object message)
        {
            Log(message);
        }

        [Conditional(k_VerboseLoggingDefine)]
        public static void LogCallVerbose(string enclosingType, [CallerMemberName] string method = "")
        {
            LogVerbose($"{enclosingType}.{method}");
        }

        [Conditional(k_VerboseLoggingDefine)]
        public static void LogCallVerboseWithMessage(string enclosingType, string message,
            [CallerMemberName] string method = "")
        {
            LogVerbose($"{enclosingType}.{method}: {message}");
        }
    }
}
