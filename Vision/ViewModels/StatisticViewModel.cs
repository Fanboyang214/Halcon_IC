using Core.Interfaces;
using Core.Models;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Vision.ViewModels
{
    /// <summary>
    /// 统计页面 ViewModel：管理统计数据加载、LiveCharts 曲线绑定、自动刷新。
    /// </summary>
    public class StatisticViewModel : BindableBase
    {
        private readonly IStatisticsService _stats;
        private readonly ILogService _logger;
        private readonly IPollTask _autoRefreshTask;

        private int _totalCount;
        private int _okCount;
        private int _ngCount;
        private double _yield;
        private int _selectedRangeIndex;
        private bool _isLoading;

        /// <summary>
        /// LiveCharts 曲线集合，绑定到 CartesianChart.Series。
        /// </summary>
        public ObservableCollection<ISeries> Series { get; } = new();

        /// <summary>
        /// Y 轴集合：左轴=总数，右轴=良率。
        /// </summary>
        public ObservableCollection<ICartesianAxis> YAxes { get; } = new();

        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        public int OkCount
        {
            get => _okCount;
            set => SetProperty(ref _okCount, value);
        }

        public int NgCount
        {
            get => _ngCount;
            set => SetProperty(ref _ngCount, value);
        }

        public double Yield
        {
            get => _yield;
            set => SetProperty(ref _yield, value);
        }

        public int SelectedRangeIndex
        {
            get => _selectedRangeIndex;
            set
            {
                if (SetProperty(ref _selectedRangeIndex, value))
                    _ = RefreshAsync();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public DelegateCommand RefreshCmd { get; }

        public StatisticViewModel(IStatisticsService stats, ILogService logger, IPollTaskFactory pollTaskFactory)
        {
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ = pollTaskFactory ?? throw new ArgumentNullException(nameof(pollTaskFactory));

            RefreshCmd = new DelegateCommand(async () => await RefreshAsync());

            // 初始化 Y 轴
            YAxes.Add(new Axis
            {
                Name = "总数",
                Position = LiveChartsCore.Measure.AxisPosition.Start,
                LabelsPaint = new SolidColorPaint(new SKColor(0x46, 0x82, 0xB4))  // SteelBlue
            });
            YAxes.Add(new Axis
            {
                Name = "良率(%)",
                Position = LiveChartsCore.Measure.AxisPosition.End,
                MinLimit = 0,
                MaxLimit = 100,
                LabelsPaint = new SolidColorPaint(new SKColor(0xFF, 0xA5, 0x00))  // Orange
            });

            // 启动自动刷新（每 5 秒刷新一次实时计数）
            _autoRefreshTask = pollTaskFactory.CreatePollTask(OnAutoRefreshAsync, 5000);
            _autoRefreshTask.StartPoll();
        }

        /// <summary>
        /// 自动刷新回调（后台线程）：仅更新实时计数，图表查询由用户手动或切换时间范围触发。
        /// </summary>
        private async Task OnAutoRefreshAsync()
        {
            try
            {
                // 实时计数已在内存中由 StatisticsService 累加，直接读取属性
                var total = _stats.TotalCount;
                var ok = _stats.OkCount;
                var ng = _stats.NgCount;
                var yield = _stats.Yield;

                // 调度到 UI 线程更新属性
                await System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    TotalCount = total;
                    OkCount = ok;
                    NgCount = ng;
                    Yield = yield;
                });
            }
            catch
            {
                // 静默处理
            }
        }

        private int GetRangeHours()
        {
            return _selectedRangeIndex switch
            {
                0 => 1,
                1 => 4,
                2 => 24,
                _ => 1
            };
        }

        public async Task RefreshAsync()
        {
            try
            {
                IsLoading = true;

                var hours = GetRangeHours();
                var end = DateTime.Now;
                var start = end.AddHours(-hours);

                // 1. 加载实时计数
                TotalCount = _stats.TotalCount;
                OkCount = _stats.OkCount;
                NgCount = _stats.NgCount;
                Yield = _stats.Yield;

                // 2. 加载时间范围聚合数据（图表）
                var data = await _stats.AggregateByMinuteAsync(start, end, null);

                // 3. 若无数据且时间范围≥24h，尝试加载概要
                if (data.Count == 0 && hours >= 24)
                {
                    var summary = await _stats.GetSummaryAsync(start, end, null);
                    _logger.AddLog("Info", $"统计查询: {start:MM-dd HH:mm}~{end:HH:mm} 总{summary.TotalCount} 合格{summary.OkCount} 不合格{summary.NgCount} 良率{summary.Yield:F1}%");
                }

                UpdateChart(data);
            }
            catch (Exception ex)
            {
                _logger.AddLog("Error", $"统计刷新失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateChart(IReadOnlyList<MinuteAggregation> data)
        {
            Series.Clear();

            // 无数据时显示空图
            if (data.Count == 0)
            {
                RaisePropertyChanged(nameof(Series));
                return;
            }

            // 总数数据（左轴）
            var totalValues = data.Select(d => (double?)d.Total).ToArray();

            // 良率数据（右轴，0~100）
            var yieldValues = data.Select(d => (double?)d.Yield).ToArray();

            Series.Add(new LineSeries<double?>
            {
                Name = "总数",
                Values = totalValues,
                Stroke = new SolidColorPaint(new SKColor(0x46, 0x82, 0xB4)) { StrokeThickness = 2 },  // SteelBlue
                Fill = null,
                GeometrySize = 4,
                ScalesXAt = 0,
                ScalesYAt = 0  // 左轴
            });
            Series.Add(new LineSeries<double?>
            {
                Name = "良率(%)",
                Values = yieldValues,
                Stroke = new SolidColorPaint(new SKColor(0xFF, 0xA5, 0x00)) { StrokeThickness = 2 },  // Orange
                Fill = null,
                GeometrySize = 4,
                ScalesXAt = 0,
                ScalesYAt = 1  // 右轴
            });

            RaisePropertyChanged(nameof(Series));
        }
    }
}