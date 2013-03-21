using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PieQ.Tests
{
    [Serializable]
    public class WaitForSignalWorkItem : WorkItem
    {
        public WaitForSignalWorkItem()
        {
            Waiter = new ManualResetEvent(false);
        }
        [JsonIgnore]
        public ManualResetEvent Waiter { get; set; }

        public override void Execute()
        {
            Waiter.WaitOne();
        }

    }
}
