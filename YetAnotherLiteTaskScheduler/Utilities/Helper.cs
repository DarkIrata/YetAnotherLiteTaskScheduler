using System;

namespace YetAnotherLiteTaskScheduler.Utilities
{
    internal static class Helper
    {
        internal static bool IsRunningOnWindows() => Environment.OSVersion.Platform.ToString().StartsWith("Win");
    }
}