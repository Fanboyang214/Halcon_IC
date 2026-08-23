using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Core.Interfaces;
using Core.Models;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Prism.Commands;
using Prism.Mvvm;

namespace Vision.ViewModels
{
    /// <summary>
    /// 统计页面 ViewModel：管理统计数据加载、LiveCharts 曲线绑定。
    /// </summary>
    public class StatisticViewModel : BindableBase
    {
        private readonly IStatisticsService _stats;
        private readonly ILogService _logger;

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

        public StatisticViewModel(IStatisticsService stats, ILogService logger)
        {
            _stats = stats;
            _logger = logger;
            RefreshCmd = new DelegateCommand(async () => await RefreshAsync());
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

                var data = await _stats.AggregateByMinuteAsync(start, end, null);

                TotalCount = _stats.TotalCount;
                OkCount = _stats.OkCount;
                NgCount = _stats.NgCount;
                Yield = _stats.Yield;

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
            // X 轴标签（时间）
            var labels = data.Select(d => d.Minute.ToString("HH:mm")).ToArray();

            // 总数数据（左轴）
            var totalValues = data.Select(d => (double?)d.Total).ToArray();

            // 良率数据（右轴，0~100）
            var yieldValues = data.Select(d => (double?)d.Yield).ToArray();

            // 更新曲线
            Series.Clear();
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

            // 更新 Y 轴：左轴(总数) + 右轴(良率0~100)
            YAxes.Clear();
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

            RaisePropertyChanged(nameof(Series));
            RaisePropertyChanged(nameof(YAxes));
        }
    }
}
