using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YetAnotherLiteTaskScheduler.Enums;
using YetAnotherLiteTaskScheduler.Utilities;

namespace YetAnotherLiteTaskScheduler
{
    /// ToDo: Queue only max 10 and request from Manager more / let Manager updater after x executions
    internal class ScheduleRunner
    {
        private readonly ILogger logger;

        public string Name { get; } = $"{nameof(ScheduleRunner)}_{Guid.NewGuid()}";

        public ushort MaxWaitingTasks { get; }

        public CultureInfo CurrentCulture { get; }

        public CultureInfo CurrentUICulture { get; }

        public Action<ScheduleRunner, ScheduledTask, ExecutionResult> TaskExecuted { get; }

        public State State { get; private set; } = State.Unkown;

        private Thread executerThread;
        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;
        private ConcurrentQueue<ScheduledTask> instantQueue = new ConcurrentQueue<ScheduledTask>();
        private ConcurrentQueue<ScheduledTask> defaultQueue = new ConcurrentQueue<ScheduledTask>();
        private int waitingTasks = 0;

        internal ScheduleRunner(
            ILogger logger,
            string name,
            ushort maxWaitingTasks,
            CultureInfo currentCulture,
            CultureInfo currentUICulture,
            Action<ScheduleRunner, ScheduledTask, ExecutionResult> taskExecuted)
        {
            this.logger = logger;

            this.State = State.Created;

            this.Name = name;
            this.MaxWaitingTasks = maxWaitingTasks;
            this.CurrentCulture = currentCulture;
            this.CurrentUICulture = currentUICulture;
            this.TaskExecuted = taskExecuted;

            this.State = State.Setup;
            this.SetupCancellationToken();
            this.SetupThread();

            this.State = State.Starting;
            this.executerThread.Start();
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

        private void SetupThread()
        {
            this.logger.LogInformation($"[{this.Name}] Setting up Thread");
            this.executerThread = new Thread(new ThreadStart(this.LoopExecuting))
            {
                Name = this.Name,
                CurrentCulture = this.CurrentCulture,
                CurrentUICulture = this.CurrentUICulture,
            };

            // ApartmentState for COM Objects is not support on linux
            if (Helper.IsRunningOnWindows())
            {
                this.executerThread.SetApartmentState(ApartmentState.MTA);
            }
        }

        internal void AddToQueue(ScheduledTask scheduledTask)
        {
            this.logger.LogInformation($"[{this.Name}] Added Task '{scheduledTask.Name}' to queues");
            var queue = scheduledTask.NextExecuteInMS <= 100 ? this.instantQueue : this.defaultQueue;
            queue.Enqueue(scheduledTask);
        }

        internal void RefreshQueue(IOrderedEnumerable<ScheduledTask> orderedTaks)
        {
            this.ResetQueues();
            foreach (var entry in orderedTaks)
            {
                this.AddToQueue(entry);
            }
        }

        internal void ResetQueues()
        {
            this.logger.LogInformation($"[{this.Name}] Resetting queues");
            var cpyInstantQueue = this.instantQueue;
            var cpyDefaultQueue = this.defaultQueue;
            this.instantQueue = new ConcurrentQueue<ScheduledTask>();
            this.defaultQueue = new ConcurrentQueue<ScheduledTask>();

            Task.Run(() =>
            {
                while (cpyInstantQueue.TryDequeue(out _))
                {
                }

                while (cpyDefaultQueue.TryDequeue(out _))
                {
                }
            });

            this.SetupCancellationToken();
            this.waitingTasks = 0;
        }

        private void LoopExecuting()
        {
            this.logger.LogInformation($"[{this.Name}] Execution loop started");
            this.State = State.Running;
            while (this.State == State.Running)
            {
                if (this.waitingTasks < this.MaxWaitingTasks)
                {
                    var scheduledTask = this.GetScheduledTask();
                    if (scheduledTask != null)
                    {
                        Interlocked.Increment(ref this.waitingTasks);
                        Task.Run(async () =>
                        {
                            var sleepTime = scheduledTask.NextExecuteInMS;
                            if (sleepTime > 0)
                            {
                                try
                                {
                                    this.logger.LogInformation($"[{this.Name}] Delaying {sleepTime} for '{scheduledTask.Name}'");
                                    await Task.Delay(sleepTime, this.cancellationToken);
                                }
                                catch (TaskCanceledException)
                                {
                                }
                            }

                            Interlocked.Decrement(ref this.waitingTasks);
                            this.Execute(scheduledTask);
                        }, this.cancellationToken);

                        this.logger.LogInformation($"[{this.Name}] Waiting Tasks: '{this.waitingTasks}', Instant Queue: '{this.instantQueue.Count}', Default Queue: '{this.defaultQueue.Count}'");
                    }
                }

                Thread.Sleep(1000);
            }

            if (this.State != State.Crashed)
            {
                this.State = State.Stopped;
            }

            this.logger.LogInformation($"[{this.Name}] Execution loop stopped. State: '{this.State}'");
        }

        private void Execute(ScheduledTask scheduledTask)
        {
            if (this.cancellationToken.IsCancellationRequested)
            {
                return;
            }

            ExecutionResult result = null;
            try
            {
                this.logger.LogInformation($"[{this.Name}] Executing: '{scheduledTask.Name}'");
                scheduledTask.Task();
                result = new ExecutionResult(SchedulerErrorCode.Success, null);
            }
            catch (Exception ex)
            {
                result = new ExecutionResult(SchedulerErrorCode.Success, ex);
            }

            this.TaskExecuted(this, scheduledTask, result);
        }

        private ScheduledTask GetScheduledTask()
        {
            var queue = this.instantQueue.Count > 0 ? this.instantQueue : this.defaultQueue;
            queue.TryDequeue(out var result);
            return result;
        }
    }
}
