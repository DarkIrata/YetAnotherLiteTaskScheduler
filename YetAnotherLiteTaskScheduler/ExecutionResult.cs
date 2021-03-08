using System;
using YetAnotherLiteTaskScheduler.Enums;

namespace YetAnotherLiteTaskScheduler
{
    public class ExecutionResult
    {
        public SchedulerErrorCode ErrorCode { get; }

        public Exception Exception { get; }

        public ExecutionResult(SchedulerErrorCode errorCode, Exception exception)
        {
            this.ErrorCode = errorCode;
            this.Exception = exception;
        }
    }
}
