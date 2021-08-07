using System;

namespace YetAnotherLiteTaskScheduler
{
    public class ScheduledTask
    {
        public string Name { get; }

        public bool Reschedule { get; }

        public Action Task { get; }

        /// <summary>
        /// Next time it will be executed. DateTime in UTC
        /// </summary>
        public DateTime ScheduledFor { get; private set; } = DateTime.UtcNow;

        public int NextExecuteInMS => (int)(this.ScheduledFor - DateTime.UtcNow).TotalMilliseconds;

        /// <summary>
        /// Returns the schedule Time which is used to calculated ScheduledFor
        /// </summary>
        public double ScheduleEvery { get; private set; }

        /// <summary>
        /// Creates new Scheduled Task for ScheduleManager
        /// </summary>
        /// <param name="name">Identifier</param>
        /// <param name="task">Action to be executed</param>
        /// <param name="scheduledFor">UTC DateTime in when the Task should be executed</param>
        public ScheduledTask(string name, Action task, DateTime scheduledFor)

            : this(name, task, 0, false)
        {
            this.ScheduledFor = scheduledFor;
        }

        /// <summary>
        /// Creates new Scheduled Task for ScheduleManager
        /// </summary>
        /// <param name="name">Identifier</param>
        /// <param name="task">Action to be executed</param>
        /// <param name="scheduleEvery">Time in Milliseconds Task should be scheduled for</param>
        /// <param name="reschedule">Reschedule Task afater execution</param>
        public ScheduledTask(string name, Action task, double scheduleEvery, bool reschedule = true)
        {
            this.Name = name ??
                    throw new ArgumentNullException(nameof(name));

            this.Task = task ??
                    throw new ArgumentNullException(nameof(task));

            if (scheduleEvery < 1 && reschedule)
            {
                throw new ArgumentException($"{nameof(scheduleEvery)} can't be less than 1 if reschedule is active");
            }

            this.ScheduleEvery = scheduleEvery;
            this.Reschedule = reschedule;

            this.RescheduleTask();
        }

        /// <summary>
        /// Reschedule Time with the given ScheduleEvery time
        /// </summary>
        public void RescheduleTask() => this.ScheduledFor = DateTime.UtcNow.AddMilliseconds(this.ScheduleEvery);
    }
}
