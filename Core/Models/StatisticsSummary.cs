using System;

namespace Core.Models
{
    /// <summary>
    /// 时间范围汇总统计。
    /// </summary>
    public class StatisticsSummary
    {
        /// <summary>起始时间。</summary>
        public DateTime Start { get; set; }

        /// <summary>结束时间。</summary>
        public DateTime End { get; set; }

        /// <summary>模板名称（null/空 表示所有产品）。</summary>
        public string? TemplateName { get; set; }

        /// <summary>总检测数。</summary>
        public int TotalCount { get; set; }

        /// <summary>合格数。</summary>
        public int OkCount { get; set; }

        /// <summary>不合格数。</summary>
        public int NgCount { get; set; }

        /// <summary>异常数。</summary>
        public int ErrorCount { get; set; }

        /// <summary>良率（0~100）。totalCount=0 时为 0。</summary>
        public double Yield => TotalCount == 0 ? 0 : (double)OkCount / TotalCount * 100;

        /// <summary>平均匹配得分（0~100）。</summary>
        public double AverageScore { get; set; }
    }
}
