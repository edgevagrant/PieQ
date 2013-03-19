using System;

namespace PieQ
{
    public abstract class MessageState
    {
        public virtual string Description { get { return this.GetType().Name.Replace("State", ""); } }
    }
    public class QueuedState : MessageState
    {
    }
    public class SucceededState : MessageState
    {
    }
    public class ProcessingState : MessageState
    {
    }

    public class FailedState : MessageState
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
}