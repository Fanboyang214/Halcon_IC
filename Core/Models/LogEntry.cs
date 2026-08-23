using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    /// <summary>
    /// 日志条目数据模型
    /// 表示一条系统日志记录，包含时间戳、级别、消息内容
    /// 用于界面日志列表显示和NLog文件持久化
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// 日志产生时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 日志级别：INFO/WARN/ERROR/DEBUG
        /// </summary>
        public string Level { get; set; }

        /// <summary>
        /// 日志消息内容
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 日志级别对应的显示颜色
        /// INFO=绿色, WARN=橙色, ERROR=红色, DEBUG=蓝色
        /// 用于界面日志列表中不同级别以不同颜色区分
        /// </summary>
        public string LevelColor
        {
            get
            {
                switch (Level)
                {
                    case "INFO":
                        return "#4CAF50";
                    case "WARN":
                        return "#FF9800";
                    case "ERROR":
                        return "#F44336";
                    case "DEBUG":
                        return "#2196F3";
                    default:
                        return "#9E9E9E";
                }
            }
        }
    }
}
