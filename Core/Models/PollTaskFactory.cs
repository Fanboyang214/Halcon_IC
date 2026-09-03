using Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class PollTaskFactory : IPollTaskFactory
    {
        public IPollTask CreatePollTask(Func<Task> pollAction, int intervalMs)
        {
            return new PollTask(pollAction, intervalMs);
        }
    }
}
