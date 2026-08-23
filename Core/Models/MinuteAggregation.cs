using System;

namespace Core.Models
{
    /// <summary>
    /// 按分钟聚合的统计数据（治理 D3，端侧 GROUP BY）。
    /// 对应报表图表中每个数据点。
    /// </summary>
    public class MinuteAggregation
    {
        /// <summary>分钟起始时间（例如 14:30:00）。</summary>
        public DateTime Minute { get; set; }

        /// <summary>该分钟内总检测数。</summary>
        public int Total { get; set; }

        /// <summary>该分钟内合格数。</summary>
        public int OkCount { get; set; }

        /// <summary>该分钟内不合格数。</summary>
        public int NgCount { get; set; }

        /// <summary>该分钟内异常数。</summary>
        public int ErrorCount { get; set; }

        /// <summary>该分钟良率（0~100）。total=0 时为 0。</summary>
        public double Yield => Total == 0 ? 0 : (double)OkCount / Total * 100;
    }
}
