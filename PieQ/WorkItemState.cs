using System;

namespace PieQ
{
    public abstract class WorkItemState
    {
        public virtual string Description { get { return this.GetType().Name.Replace("State", ""); } }
    }
    public class QueuedState : WorkItemState
    {
    }
    public class SucceededState : WorkItemState
    {
    }
    public class ProcessingState : WorkItemState
    {
    }

    public class FailedState : WorkItemState
    {
        public FailedState(string message)
        {
            this.ErrorMessage = message;
        }

        string ErrorMessage { get; set; }

        public override string Description
        {
            get
            {
                return ErrorMessage;
            }
        }
    }
    public class NotRecoveredAfterShutdownState : FailedState
    {
        public NotRecoveredAfterShutdownState()
            : base("Could not recover task after shutdown")
        {
        }
    } 
}