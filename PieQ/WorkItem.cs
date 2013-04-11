using System;
using System.Threading;

namespace PieQ
{
    public abstract class WorkItem
    {
        public string Type { get { return this.GetType().Name.Replace("WorkItem", ""); } }

        public DateTimeOffset ReceivedAt {  get; set; }
        public WorkItemState WorkItemState { get; set; }

        public TimeSpan? ExecutionDuration {  get; set; }
        public string WorkItemId { get; set; }

        public abstract void Execute();

        protected void Resume()
        {
        }

        protected void Recover()
        {
            if (!(this.WorkItemState is ProcessingState))
            {
                throw new Exception("Recovery can only occur when a task is not running and is Processing");
            }
            this.WorkItemState = new FailedState("");
        }

        protected internal void Suspend()
        {
        }
    }
}