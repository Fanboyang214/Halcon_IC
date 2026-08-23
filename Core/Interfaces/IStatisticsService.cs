using Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Interfaces
{
    /// <summary>
    /// 统计服务契约。负责检测记录的入库、按时间范围查询与端侧聚合（治理 D3）。
    /// </summary>
    /// <remarks>
    /// 设计目标（对照重构文档）：
    ///  D3 端侧聚合：按分钟 GROUP BY 聚合 OK/NG 数量，避免全表拉取到客户端再算；
    ///  分页查询：长时间范围记录数可能达百万级，必须分页；
    ///  写入解耦：Record 异步写库，失败仅日志不抛，避免拖慢检测主线程（M1）。
    ///
    /// 本接口仅定义契约，不绑定具体 ORM（EF Core / Dapper 均可）。
    /// 实现端建议 Scoped 生命周期，与 DbContext 一致。
    /// </remarks>
    public interface IStatisticsService
    {
        /// <summary>
        /// 实时累计统计（自进程启动以来）。
        /// 线程安全：内部使用 Interlocked 维护计数，多线程写入安全。
        /// </summary>
        int TotalCount { get; }

        /// <summary>合格总数。</summary>
        int OkCount { get; }

        /// <summary>不合格总数（含 NG 与 No found）。</summary>
        int NgCount { get; }

        /// <summary>异常总数（检测过程出错，与 NG 区分）。</summary>
        int ErrorCount { get; }

        /// <summary>当前良率（百分比，0~100）。totalCount=0 时返回 0。</summary>
        double Yield { get; }

        /// <summary>
        /// 记录一次检测结果到数据库。
        /// 异步执行，调用方无需 await；写库失败仅记录日志，不抛异常（M1）。
        /// 内部同时累加 TotalCount/OkCount/NgCount/ErrorCount（Interlocked）。
        /// </summary>
        /// <param name="result">检测结果。</param>
        /// <param name="templateName">使用的模板名称（产品型号）。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步写库操作的任务。</returns>
        Task RecordAsync(DetectionResult result, string templateName, CancellationToken cancellationToken = default);

        /// <summary>
        /// 直接写入一条检测记录（供批量导入或迁移使用）。
        /// </summary>
        /// <param name="record">已构造的记录实体。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        Task RecordAsync(ProductInspectionRecord record, CancellationToken cancellationToken = default);

        /// <summary>
        /// 重置实时累计统计（TotalCount/OkCount/NgCount/ErrorCount 归零）。
        /// 用于切换产品或用户手动清零。不影响已入库的历史记录。
        /// </summary>
        void ResetCounters();

        /// <summary>
        /// 分页查询指定时间范围内的检测记录（明细）。
        /// </summary>
        /// <param name="start">起始时间（包含），null 表示不限制下界。</param>
        /// <param name="end">结束时间（包含），null 表示不限制上界。</param>
        /// <param name="templateName">模板名称过滤，null/空 表示所有产品。</param>
        /// <param name="isOk">合格状态过滤：null=全部，true=仅 OK，false=仅 NG/异常。</param>
        /// <param name="pageIndex">页码（从 0 开始）。</param>
        /// <param name="pageSize">每页条数（建议 ≤ 100）。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>分页结果集。</returns>
        Task<PagedResult<ProductInspectionRecord>> QueryAsync(
            DateTime? start,
            DateTime? end,
            string? templateName,
            bool? isOk,
            int pageIndex,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 按分钟聚合指定时间范围内的检测记录（端侧 GROUP BY，治理 D3）。
        /// 用于报表图表展示，避免全表拉取到客户端。
        /// </summary>
        /// <param name="start">起始时间（包含）。</param>
        /// <param name="end">结束时间（包含）。</param>
        /// <param name="templateName">模板名称过滤，null/空 表示所有产品。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>按分钟聚合的统计序列，按时间升序排列。</returns>
        Task<IReadOnlyList<MinuteAggregation>> AggregateByMinuteAsync(
            DateTime start,
            DateTime end,
            string? templateName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取指定时间范围内的汇总统计。
        /// </summary>
        /// <param name="start">起始时间（包含）。</param>
        /// <param name="end">结束时间（包含）。</param>
        /// <param name="templateName">模板名称过滤，null/空 表示所有产品。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>汇总数据（总数、合格、不合格、良率）。</returns>
        Task<StatisticsSummary> GetSummaryAsync(
            DateTime start,
            DateTime end,
            string? templateName,
            CancellationToken cancellationToken = default);
    }
}
