namespace YetAnotherLiteTaskScheduler.Enums
{
    public enum SchedulerErrorCode
    {
        Success = 0,
        Unspecified = 1,

        // Manager
        AddingTaskFailedWithUnkownError = 100,
        RemovingTaskFailedWithUnkownError = 101,
        TaskNameAlreadyRegistered = 102,

        // Runner


        // Tasks
        ExceptionWhileExecutingTask = 200,
    }
}
