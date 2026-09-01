using Core.Interfaces;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Data.Services
{
    /// <summary>
    /// <see cref="IStatisticsService"/> 的 EF Core 实现。
    /// 使用 <see cref="IRepository{ProductInspectionRecord}"/> 入库与查询，
    /// 内存计数器用 <see cref="Interlocked"/> 维护线程安全（T2）。
    /// </summary>
    /// <remarks>
    /// 治理点：
    ///  D3 端侧聚合：AggregateByMinuteAsync 用 LINQ GroupBy 在数据库侧聚合；
    ///  M1 异常隔离：RecordAsync 写库失败仅日志不抛，不阻塞调用方；
    ///  T2 可见性：计数器用 volatile + Interlocked；
    ///  生命周期：Scoped，与 DbContext 一致。
    /// </remarks>
    public class StatisticsService : IStatisticsService
    {
        private readonly IRepository<ProductInspectionRecord> _repository;
        private readonly ILogService _logger;
        private readonly string _deviceNo;

        // 实时累计计数器（T2：volatile + Interlocked）
        private volatile int _totalCount;
        private volatile int _okCount;
        private volatile int _ngCount;
        private volatile int _errorCount;

        public StatisticsService(
            IRepository<ProductInspectionRecord> repository,
            ILogService logger,
            IConfigService configService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ = configService ?? throw new ArgumentNullException(nameof(configService));
            _deviceNo = configService.Camera?.SerialNumber ?? "unknown";
        }

        /// <inheritdoc />
        public int TotalCount => _totalCount;

        /// <inheritdoc />
        public int OkCount => _okCount;

        /// <inheritdoc />
        public int NgCount => _ngCount;

        /// <inheritdoc />
        public int ErrorCount => _errorCount;

        /// <inheritdoc />
        public double Yield => _totalCount == 0 ? 0 : (double)_okCount / _totalCount * 100;

        /// <summary>
        /// 从 <see cref="DetectionResult"/> 构造一条检测记录。
        /// </summary>
        public async Task RecordAsync(DetectionResult result, string templateName, CancellationToken cancellationToken = default)
        {
            // 1. 构造实体
            var record = new ProductInspectionRecord
            {
                InspectionTime =result.Time,
                ProductModel = templateName ?? string.Empty,
                MatchScore = result.MatchScore,
                PinCount = result.PinCount,
                PinCount2 = result.PinCount2,
                IsOk = result.IsOK,
                ResultText = result.ResultText ?? string.Empty,
                IsError = result.IsError,
                ErrorMessage = result.IsError ? (result.ErrorMessage ?? null) : null,
                DefectReasons = DeriveDefectReasons(result),
                DeviceNo = _deviceNo,
            };

            // 2. 入库（失败仅日志不抛 - M1）
            await RecordAsync(record, cancellationToken);

            // 3. 累加内存计数器（Interlocked 保证线程安全 - T2）
            //     放在 try 外：实时良率反映"已发生的检测"，与入库是否成功无关
            Interlocked.Increment(ref _totalCount);
            if (result.IsError)
                Interlocked.Increment(ref _errorCount);
            else if (result.IsOK)
                Interlocked.Increment(ref _okCount);
            else
                Interlocked.Increment(ref _ngCount);
        }

        /// <summary>
        /// 直接写入一条已构造的检测记录（供批量导入或迁移使用）。
        /// 写库失败仅记录日志，不抛异常（M1）。
        /// </summary>
        public async Task RecordAsync(ProductInspectionRecord record, CancellationToken cancellationToken = default)
        {
            try
            {
                _repository.Add(record);
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // M1：写库失败不传播异常，避免拖慢检测主线程
                _logger.AddLog("Error", $"统计记录入库失败: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public void ResetCounters()
        {
            Interlocked.Exchange(ref _totalCount, 0);
            Interlocked.Exchange(ref _okCount, 0);
            Interlocked.Exchange(ref _ngCount, 0);
            Interlocked.Exchange(ref _errorCount, 0);
        }

        /// <inheritdoc />
        public async Task<PagedResult<ProductInspectionRecord>> QueryAsync(
            DateTime? start,
            DateTime? end,
            string? templateName,
            bool? isOk,
            int pageIndex,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            if (pageIndex < 0) pageIndex = 0;
            if (pageSize <= 0) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var query = _repository.GetAll();

            if (start.HasValue)
                query = query.Where(r => r.InspectionTime >= start.Value);
            if (end.HasValue)
                query = query.Where(r => r.InspectionTime <= end.Value);
            if (!string.IsNullOrEmpty(templateName))
                query = query.Where(r => r.ProductModel == templateName);
            if (isOk.HasValue)
                query = query.Where(r => r.IsOk == isOk.Value);

            // 注意：IQueryable 来自 Repository，需在 DbContext 范围内执行
            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(r => r.InspectionTime)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<ProductInspectionRecord>
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = items
            };
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MinuteAggregation>> AggregateByMinuteAsync(
            DateTime start,
            DateTime end,
            string? templateName,
            CancellationToken cancellationToken = default)
        {
            var query = _repository.GetAll()
                .Where(r => r.InspectionTime >= start && r.InspectionTime <= end);
            if (!string.IsNullOrEmpty(templateName))
                query = query.Where(r => r.ProductModel == templateName);

            // D3：数据库侧 GROUP BY 按分钟聚合
            var groups = await query
                .GroupBy(r => new
                {
                    r.InspectionTime.Year,
                    r.InspectionTime.Month,
                    r.InspectionTime.Day,
                    r.InspectionTime.Hour,
                    r.InspectionTime.Minute
                })
                .Select(g => new MinuteAggregation
                {
                    Minute = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, g.Key.Minute, 0),
                    Total = g.Count(),
                    OkCount = g.Count(r => r.IsOk),
                    NgCount = g.Count(r => !r.IsOk && !r.IsError),
                    ErrorCount = g.Count(r => r.IsError)
                })
                .OrderBy(a => a.Minute)
                .ToListAsync(cancellationToken);

            return groups;
        }

        /// <inheritdoc />
        public async Task<StatisticsSummary> GetSummaryAsync(
            DateTime start,
            DateTime end,
            string? templateName,
            CancellationToken cancellationToken = default)
        {
            var query = _repository.GetAll()
                .Where(r => r.InspectionTime >= start && r.InspectionTime <= end);
            if (!string.IsNullOrEmpty(templateName))
                query = query.Where(r => r.ProductModel == templateName);

            var summary = await query
                .GroupBy(r => 1) // 单组聚合
                .Select(g => new StatisticsSummary
                {
                    Start = start,
                    End = end,
                    TemplateName = templateName,
                    TotalCount = g.Count(),
                    OkCount = g.Count(r => r.IsOk),
                    NgCount = g.Count(r => !r.IsOk && !r.IsError),
                    ErrorCount = g.Count(r => r.IsError),
                    AverageScore = g.Average(r => (double?)r.MatchScore) ?? 0
                })
                .FirstOrDefaultAsync(cancellationToken) ?? new StatisticsSummary
                {
                    Start = start,
                    End = end,
                    TemplateName = templateName
                };

            return summary;
        }

        /// <summary>
        /// 从 <see cref="DetectionResult"/> 派生缺陷原因文本。
        /// </summary>
        private static string? DeriveDefectReasons(DetectionResult result)
        {
            if (result.IsError)
                return $"检测异常: {result.ErrorMessage}";
            if (result.ResultText == "No found")
                return "未匹配到芯片";
            if (result.ResultText == "NG")
                return $"区域1针脚: {result.PinCount}; 区域2针脚: {result.PinCount2}";
            return null; // OK 或其他
        }
    }
}
