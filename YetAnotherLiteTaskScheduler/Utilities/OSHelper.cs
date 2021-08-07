using System;

namespace YetAnotherLiteTaskScheduler.Utilities
{
    internal static class OSHelper
    {
        internal static bool IsRunningOnWindows() => Environment.OSVersion.Platform.ToString().StartsWith("Win");
    }
}