using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces
{
    public interface IPollTaskFactory
    {
        /// <param name="pollAction">业务轮询逻辑，异步委托</param>
        /// <param name="intervalMs">轮询间隔毫秒</param>
        IPollTask CreatePollTask(Func<Task> pollAction, int intervalMs);
    }
}
