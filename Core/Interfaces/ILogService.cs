using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces
{
    public interface ILogService
    {
        /// <summary>
        /// 添加一条日志记录
        /// </summary>
        /// <param name="level">日志级别：INFO/WARN/ERROR/DEBUG</param>
        /// <param name="message">日志消息内容</param>
        void AddLog(string level, string message);
    }
}
