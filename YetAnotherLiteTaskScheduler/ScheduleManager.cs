using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YetAnotherLiteTaskScheduler.Enums;

namespace YetAnotherLiteTaskScheduler
{
    public class ScheduleManager
    {
        private readonly RunnerConfiguration configuration;
        private readonly ConcurrentDictionary<string, ScheduledTask> scheduledTasks = new ConcurrentDictionary<string, ScheduledTask>();
        private readonly List<ScheduleRunner> runner = new List<ScheduleRunner>();
        private readonly object runnerCheckLock = new object();
        private readonly object reschedulingLock = new object();
        private readonly ILogger logger;

        public IEnumerable<(string Name, State State)> RunnerStates => this.runner.Select(r => (r.Name, r.State));

        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;

        public ScheduleManager()
            : this(new RunnerConfiguration())
        {
        }

        public ScheduleManager(RunnerConfiguration configuration)
            : this(configuration, null)
        {
        }

        public ScheduleManager(ILogger logger)
            : this(new RunnerConfiguration(), logger)
        {
        }

        public ScheduleManager(RunnerConfiguration configuration, ILogger logger)
        {
            this.configuration = configuration ??
                    throw new ArgumentNullException(nameof(configuration));

            this.logger = logger ?? NullLogger.Instance;

            // Have at least 1 runner ready to reduce overhead 
            this.AddRunner(this.GetNewRunner());
        }

        private void SetupCancellationToken()
        {
            if (this.cancellationTokenSource != null)
            {
                this.cancellationTokenSource.Cancel();
            }

            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;
        }


        private ScheduleRunner GetNewRunner()
        {
            var name = $"{this.configuration.ThreadBaseName}_{this.runner.Count}";
            var logger = this.configuration.LogRunnerActions ? this.logger : NullLogger.Instance;
            var runner = new ScheduleRunner(
                logger,
                name,
                this.configuration.MaxWaitingTasks,
                this.configuration.CurrentCulture,
                this.configuration.CurrentUICulture,
                this.TaskExecuted);

            return runner;
        }

        private void TaskExecuted(ScheduleRunner runner, ScheduledTask scheduledTask, ExecutionResult result)
        {
            var logMsg = $"Runner '{runner.Name}' executed Task '{scheduledTask.Name}'. Result: {result.ErrorCode}";
            if (result.ErrorCode != SchedulerErrorCode.Success)
            {
                this.logger.LogWarning(result.Exception, logMsg);
            }
            else
            {
                this.logger.LogInformation(logMsg);
            }

            if (!scheduledTask.Reschedule)
            {
                this.TryRemoveScheduleTask(scheduledTask.Name);
            }
            else
            {
                this.logger.LogInformation($"Rescheduling '{scheduledTask.Name}'");
                scheduledTask.RescheduleTask();
            }

            this.Reschedule();
        }

        public SchedulerErrorCode TryScheduleTask(ScheduledTask scheduledTask)
        {
            if (this.scheduledTasks.ContainsKey(scheduledTask.Name))
            {
                return SchedulerErrorCode.TaskNameAlreadyRegistered;
            }

            if (!this.scheduledTasks.TryAdd(scheduledTask.Name, scheduledTask))
            {
                return SchedulerErrorCode.AddingTaskFailedWithUnkownError;
            }

            this.Reschedule();

            return SchedulerErrorCode.Success;
        }


        public SchedulerErrorCode TryRemoveScheduleTask(ScheduledTask scheduledTask) => this.TryRemoveScheduleTask(scheduledTask.Name);

        public SchedulerErrorCode TryRemoveScheduleTask(string name)
        {
            if (this.scheduledTasks.ContainsKey(name))
            {
                if (!this.scheduledTasks.TryRemove(name, out _))
                {
                    return SchedulerErrorCode.RemovingTaskFailedWithUnkownError;
                }

                this.Reschedule();
            }

            return SchedulerErrorCode.Success;
        }

        private void Reschedule()
        {
            this.SetupCancellationToken();

            Task.Run(() =>
            {
                lock (this.runnerCheckLock)
                {
                    this.CheckRunners();
                }

                lock (this.reschedulingLock)
                {
                    if (this.cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var order = this.scheduledTasks.ToArray().Select(e => e.Value).OrderBy(st => st.NextExecuteInMS);
                    if (this.runner.Count == 1)
                    {
                        this.runner[0].RefreshQueue(order);
                    }
                    else
                    {
                        foreach (var instance in this.runner)
                        {
                            instance.ResetQueues();
                        }

                        var runnerIndex = 0;
                        foreach (var item in order)
                        {
                            if (this.cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }

                            var curRunner = this.runner[runnerIndex];
                            curRunner.AddToQueue(item);
                            runnerIndex++;

                            if (runnerIndex >= this.runner.Count)
                            {
                                runnerIndex = 0;
                            }
                        }
                    }
                }
            }, this.cancellationToken);
        }

        private void CheckRunners()
        {
            if (this.runner.Count == 0)
            {
                this.logger.LogWarning($"No runners found while checking runners!");
                this.AddRunner(this.GetNewRunner());
            }

            foreach (var instance in this.runner.ToList())
            {
                if (instance.State == State.Crashed)
                {
                    this.logger.LogWarning($"Runner '{instance.Name}' crashed!");
                    this.RemoveRunner(instance);
                }
            }

            var maxTasksPerRunner = this.configuration.TasksDividerPerRunner / this.configuration.MaxRunnerInstances;
            var tasksPerRunner = this.scheduledTasks.Count / this.runner.Count;
            if (tasksPerRunner > this.configuration.TasksDividerPerRunner)
            {
                if (this.runner.Count < this.configuration.MaxRunnerInstances)
                {
                    this.AddRunner(this.GetNewRunner());
                }
            }
            else if (this.scheduledTasks.Count < tasksPerRunner && this.runner.Count > 1)
            {
                this.RemoveRunner(this.runner.Last());
            }
        }

        private void AddRunner(ScheduleRunner runner)
        {
            this.runner.Add(runner);
            this.logger.LogInformation($"Added '{runner.Name}' to runners");
        }

        private void RemoveRunner(ScheduleRunner runner)
        {
            this.runner.Remove(runner);
            this.logger.LogInformation($"Removing '{runner.Name}' from runners");
        }
    }
}
