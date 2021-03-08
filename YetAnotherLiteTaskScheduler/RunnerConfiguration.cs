using System;
using System.Globalization;
using System.Threading;

namespace YetAnotherLiteTaskScheduler
{
    public class RunnerConfiguration
    {
        public string ThreadBaseName { get; }

        public ushort MaxRunnerInstances { get; }

        public uint TasksDividerPerRunner { get; }

        public ushort MaxWaitingTasks { get; }

        public CultureInfo CurrentCulture { get; }

        public CultureInfo CurrentUICulture { get; }

        public bool LogRunnerActions { get; }

        public RunnerConfiguration(
            string basename = nameof(ScheduleRunner),
            ushort maxRunnerInstances = 1,
            uint tasksDividerPerRunner = uint.MaxValue,
            ushort maxWaitingTasks = 5,
            CultureInfo currentculture = null,
            CultureInfo currentUICulter = null,
            bool logRunnerActions = false)
        {
            this.ThreadBaseName = basename ??
                    throw new ArgumentNullException(nameof(basename));

            this.MaxRunnerInstances = maxRunnerInstances;
            this.MaxWaitingTasks = maxWaitingTasks;
            this.TasksDividerPerRunner = tasksDividerPerRunner;
            this.CurrentCulture = currentculture ?? Thread.CurrentThread.CurrentCulture;
            this.CurrentUICulture = currentUICulter ?? Thread.CurrentThread.CurrentUICulture;
            this.LogRunnerActions = logRunnerActions;
        }
    }
}
