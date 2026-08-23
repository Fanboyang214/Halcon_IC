using Core.Interfaces;
using Core.Models;
using HalconDotNet;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Services
{
    /// <summary>
    /// <see cref="IDetectionService"/> 的 Halcon 24.11 实现。
    /// 内部 BlockingCollection 生产者-消费者：相机帧入队，独立消费者线程执行 Process。
    /// </summary>
    /// <remarks>
    /// 治理点：
    ///  L2：不依赖 ICameraService，帧由 EnqueueFrame 入参；
    ///  L4：BlockingCollection bounded 容量，入队 TryAdd 非阻塞，满即丢帧；
    ///  M1：消费者线程 catch 单帧异常，记录后继续下一帧，不传播；
    ///  M3：Process 内部所有 HObject 中间量在 finally 释放；返回的 DisplayImage 所有权归调用方；
    ///  T2：_running 用 volatile；_consumerTask 引用在锁内修改；
    ///  T3：_startStopLock 覆盖 Start/Stop 全流程；
    ///  X1：Stop 通过 Task.Wait(timeoutMs) 等待消费者真正退出。
    /// </remarks>
    public sealed class DetectionService : IDetectionService
    {
        // 队列容量上限：超过即丢帧，避免相机线程阻塞（L4）
        private const int QueueCapacity = 2;

        // 最小匹配分阈值（0~1），低于此值视为未匹配到芯片
        private const double MinMatchScore = 0.65;

        // 消费者线程退出等待超时
        private const int ConsumerExitWaitMs = 3000;

        private readonly ILogService _logger;

        // 启停互斥锁（T3）
        private readonly object _startStopLock = new();
        // 队列消费锁（保证清空与入队/消费互斥）
        private readonly object _queueLock = new();

        // 帧队列（L4），构造时固定容量
        private BlockingCollection<HObject>? _queue;

        // 消费者线程取消令牌
        private CancellationTokenSource? _cts;
        private Task? _consumerTask;

        // 运行标志（T2 跨线程可见性）
        private volatile bool _running;
        private bool _disposed;

        // Start 时绑定的模板与配置（消费者线程访问）
        private ITemplateService? _template;
        private InspectionConfig? _config;

        /// <summary>构造。</summary>
        /// <param name="logger">日志服务，用于记录丢帧、处理异常等。</param>
        public DetectionService(ILogService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public bool IsRunning => _running;

        /// <inheritdoc/>
        public int PendingCount => _queue?.Count ?? 0;

        /// <inheritdoc/>
        public void Start(ITemplateService template, InspectionConfig config)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!template.IsTemplateCreated)
                throw new InvalidOperationException("模板未创建，无法启动检测。");

            lock (_startStopLock)
            {
                if (_running) return; // 幂等

                _template = template;
                _config = config;

                _queue?.Dispose();
                _queue = new BlockingCollection<HObject>(QueueCapacity);
                _cts = new CancellationTokenSource();

                _running = true;
                _consumerTask = Task.Run(ConsumerLoop);
            }
        }

        /// <inheritdoc/>
        public bool Stop(int timeoutMs = 3000)
        {
            Task? task;
            lock (_startStopLock)
            {
                if (!_running) return true;

                _running = false;
                _cts?.Cancel();
                _queue?.CompleteAdding();
                task = _consumerTask;
            }

            if (task == null) return true;

            try
            {
                // X1：等待消费者线程真正退出
                bool exited = task.Wait(timeoutMs > 0 ? timeoutMs : ConsumerExitWaitMs);
                if (!exited)
                {
                    _logger?.AddLog("Warn", $"DetectionService.Stop: 消费者线程在 {timeoutMs}ms 内未退出");
                }
                return exited;
            }
            catch (AggregateException)
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public bool EnqueueFrame(HObject frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (!_running || _queue == null)
                throw new InvalidOperationException("检测未启动，无法入队。");

            // L4：TryAdd 非阻塞，队列满即丢帧
            bool added = _queue.TryAdd(frame);
            if (!added)
            {
                _logger?.AddLog("Warn", "DetectionService: 队列已满，丢弃当前帧");
            }
            return added;
        }

        /// <inheritdoc/>
        public DetectionResult Process(ITemplateService template, InspectionConfig config, HObject frame)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (!template.IsTemplateCreated)
                throw new InvalidOperationException("模板未创建，无法执行检测。");

            // 所有中间 HObject 在 finally 释放
            HObject? matchContours = null;
            HObject? rect1Region = null;
            HObject? rect1Reduced = null;
            HObject? rect1Thresh = null;
            HObject? rect1Conn = null;
            HObject? rect1Selected = null;
            HObject? rect2Region = null;
            HObject? rect2Reduced = null;
            HObject? rect2Thresh = null;
            HObject? rect2Conn = null;
            HObject? rect2Selected = null;
            HObject? displayImage = null;
            HObject? modelContoursAtPose = null;

            var result = new DetectionResult
            {
                ResultText = "No found"
            };

            try
            {
                // 1. 模板匹配
                //    AngleStart/Extent=0：只匹配 0°（若模板创建时已支持 360°，
                //    这里仍用 0 起始，实际匹配范围由模板自身决定）
                HOperatorSet.FindShapeModel(
                    frame, template.ModelID,
                    0.0, new HTuple(360).TupleRad(),
                    MinMatchScore, 1, 1,
                    "least_squares_high", 0, 0.7,
                    out HTuple row, out HTuple column, out HTuple angle, out HTuple score);

                if (score.Length == 0 || score[0].D < MinMatchScore)
                {
                    result.ResultText = "No found";
                    result.MatchScore = score.Length > 0 ? (int)(score[0].D * 100) : 0;
                    // 仍生成显示图（原图）
                    displayImage = frame.CopyObj(1, -1);
                    result.DisplayImage = displayImage;
                    displayImage = null; // 所有权转移
                    return result;
                }

                double matchScore = score[0].D;
                double matchAngle = angle[0].D;
                double matchRow = row[0].D;
                double matchCol = column[0].D;
                result.MatchScore = (int)(matchScore * 100);

                // 2. 生成模板轮廓在匹配位姿下的副本（用于显示）
                //    vector_angle_to_rigid 计算旋转平移矩阵
                HOperatorSet.VectorAngleToRigid(0, 0, 0, matchRow, matchCol, matchAngle, out HTuple homMat2d);
                HOperatorSet.AffineTransContourXld(template.ModelContours, out modelContoursAtPose, homMat2d);

                // 3. 从 TemplateConfig 导出检测区域坐标（旋转矩形）
                //    检测区域在模板创建时已保存，随模板一起旋转
                var tplCfg = template.ExportConfig();

                // 检测区域 1：中心(row1,col1)，旋转 angle，半长 Length1/Length2
                double rect1CenterRow = tplCfg.CheckRect1Row;
                double rect1CenterCol = tplCfg.CheckRect1Column;
                double rect1Phi = tplCfg.CheckRect1Phi + matchAngle;
                double rect1L1 = tplCfg.CheckRect1Length1;
                double rect1L2 = tplCfg.CheckRect1Length2;

                // 检测区域 2
                double rect2CenterRow = tplCfg.CheckRect2Row;
                double rect2CenterCol = tplCfg.CheckRect2Column;
                double rect2Phi = tplCfg.CheckRect2Phi + matchAngle;
                double rect2L1 = tplCfg.CheckRect2Length1;
                double rect2L2 = tplCfg.CheckRect2Length2;

                // 4. 生成检测区域 1 并在原图上做仿射变换（随芯片旋转）
                HOperatorSet.GenRectangle2(out rect1Region, rect1CenterRow, rect1CenterCol, rect1Phi, rect1L1, rect1L2);
                HOperatorSet.AffineTransRegion(rect1Region, out var rect1Trans, homMat2d, "nearest_neighbor");
                HOperatorSet.ReduceDomain(frame, rect1Trans, out rect1Reduced);
                rect1Trans.Dispose();

                // 5. 检测区域 1 内针脚计数
                int pinCount1 = CountPins(rect1Reduced, out rect1Thresh, out rect1Conn, out rect1Selected);

                // 6. 检测区域 2 同理
                HOperatorSet.GenRectangle2(out rect2Region, rect2CenterRow, rect2CenterCol, rect2Phi, rect2L1, rect2L2);
                HOperatorSet.AffineTransRegion(rect2Region, out var rect2Trans, homMat2d, "nearest_neighbor");
                HOperatorSet.ReduceDomain(frame, rect2Trans, out rect2Reduced);
                rect2Trans.Dispose();
                int pinCount2 = CountPins(rect2Reduced, out rect2Thresh, out rect2Conn, out rect2Selected);

                result.PinCount = pinCount1;
                result.PinCount2 = pinCount2;

                // 7. 合格判定
                bool ok1 = pinCount1 >= config.PinCountMin && pinCount1 <= config.PinCountMax;
                bool ok2 = pinCount2 >= config.PinCount2Min && pinCount2 <= config.PinCount2Max;
                result.IsOK = ok1 && ok2;
                result.ResultText = result.IsOK ? "OK" : "NG";
                result.ShouldTrigger = true;
                result.IsRisingEdge = true;

                // 8. 生成显示图（原图 + 匹配轮廓 + 检测区域叠加）
                displayImage = frame.CopyObj(1, -1);
                result.DisplayImage = displayImage;
                displayImage = null;

                result.ModelContours = modelContoursAtPose;
                modelContoursAtPose = null;

                result.DetectionRegion1 = rect1Region;
                rect1Region = null;

                result.DetectionRegion2 = rect2Region;
                rect2Region = null;

                return result;
            }
            catch (HOperatorException ex)
            {
                // M1：算法异常不传播，记录到 result
                result.IsError = true;
                result.ErrorMessage = $"Halcon 异常: {ex.Message}";
                result.ResultText = "检测出错";
                // 兜底显示图
                if (result.DisplayImage == null)
                {
                    try
                    {
                        displayImage = frame.CopyObj(1, -1);
                        result.DisplayImage = displayImage;
                        displayImage = null;
                    }
                    catch { /* 极端情况忽略 */ }
                }
                _logger?.AddLog("Error", $"DetectionService.Process Halcon 异常: {ex.Message}");
                return result;
            }
            catch (Exception ex)
            {
                result.IsError = true;
                result.ErrorMessage = $"检测异常: {ex.Message}";
                result.ResultText = "检测出错";
                _logger?.AddLog("Error", $"DetectionService.Process 异常: {ex.Message}");
                return result;
            }
            finally
            {
                // M3：所有中间对象释放，已转移到 result.DisplayImage 的除外
                matchContours?.Dispose();
                modelContoursAtPose?.Dispose();
                rect1Region?.Dispose();
                rect1Reduced?.Dispose();
                rect1Thresh?.Dispose();
                rect1Conn?.Dispose();
                rect1Selected?.Dispose();
                rect2Region?.Dispose();
                rect2Reduced?.Dispose();
                rect2Thresh?.Dispose();
                rect2Conn?.Dispose();
                rect2Selected?.Dispose();
                displayImage?.Dispose();
            }
        }

        /// <inheritdoc/>
        public void ClearPendingFrames()
        {
            lock (_queueLock)
            {
                if (_queue == null) return;
                while (_queue.TryTake(out HObject? frame))
                {
                    frame?.Dispose();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop(ConsumerExitWaitMs);
            ClearPendingFrames();
            _queue?.Dispose();
            _queue = null;
            _cts?.Dispose();
            _cts = null;
        }

        // ===== 内部方法 =====

        /// <summary>
        /// 消费者线程主循环：从队列取帧 → Process → 发布结果。
        /// </summary>
        private void ConsumerLoop()
        {
            if (_queue == null || _cts == null || _template == null || _config == null) return;

            try
            {
                foreach (var frame in _queue.GetConsumingEnumerable(_cts.Token))
                {
                    if (!_running) break;

                    DetectionResult? result = null;
                    try
                    {
                        result = Process(_template, _config, frame);
                    }
                    catch (Exception ex)
                    {
                        _logger?.AddLog("Error", $"消费者线程处理异常: {ex.Message}");
                    }
                    finally
                    {
                        frame?.Dispose();
                    }

                    if (result != null)
                    {
                        result.DisplayImage?.Dispose();
                        result.ModelContours?.Dispose();
                        result.DetectionRegion1?.Dispose();
                        result.DetectionRegion2?.Dispose();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger?.AddLog("Error", $"消费者线程异常退出: {ex.Message}");
            }
        }

        /// <summary>
        /// 在检测区域内计数针脚：阈值化 → 连通域 → 形状筛选。
        /// </summary>
        /// <param name="reducedImage">已 ReduceDomain 的子图像。</param>
        /// <param name="thresh">输出：阈值化结果（调用方释放）。</param>
        /// <param name="conn">输出：连通域结果（调用方释放）。</param>
        /// <param name="selected">输出：筛选后区域（调用方释放）。</param>
        /// <returns>针脚数量。</returns>
        private static int CountPins(HObject reducedImage,
            out HObject thresh, out HObject conn, out HObject selected)
        {
            thresh = null!; conn = null!; selected = null!;
            try
            {
                // 阈值化：提取明亮区域（针脚通常较亮）
                HOperatorSet.Threshold(reducedImage, out thresh, 80, 255);
                // 连通域
                HOperatorSet.Connection(thresh, out conn);
                // 形状筛选：面积范围 + 高度范围（典型针脚尺寸）
                HOperatorSet.SelectShape(conn, out selected, "area", "and", 50, 5000);

                // 计数
                return selected.CountObj();
            }
            catch
            {
                return 0;
            }
        }

       
    }
}
