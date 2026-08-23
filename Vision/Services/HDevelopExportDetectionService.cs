using Core.Interfaces;
using Core.Models;
using HalconDotNet;
using System;

namespace Vision.Services
{
    /// <summary>
    /// 使用 HDevelop 导出代码风格的 DetectionService 实现对照版。
    /// 本文件演示"从 HDevelop 导出 C# 代码后最小改造集成"的方式，
    /// 与 <see cref="DetectionService"/> 的手写风格对照。
    /// </summary>
    /// <remarks>
    /// HDevelop 导出代码特征：
    ///  - 变量命名 ho_/hv_ 前缀（HDevelop 自动生成）；
    ///  - 大量 HOperatorSet.xxx 调用堆叠在一个长方法内；
    ///  - 无 try-finally 资源释放，无 null 检查；
    ///  - 算法步骤线性铺开，控制流简单。
    /// 改造原则：
    ///  - 保留 HDevelop 的变量命名与调用顺序，便于与 HDevelop 脚本对照；
    ///  - 在外层包一层 try-finally 集中释放；
    ///  - 适配 <see cref="IDetectionService"/> 接口签名。
    /// 注意：本实现不使用队列/消费者线程，仅保留同步 Process 入口，
    /// 用于演示导出代码的集成方式，生产环境请用 <see cref="DetectionService"/>。
    /// </summary>
    public sealed class HDevelopExportDetectionService : IDetectionService
    {
        private readonly ILogService _logger;
        private bool _running;
        private bool _disposed;

        public HDevelopExportDetectionService(ILogService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsRunning => _running;
        public int PendingCount => 0; // 同步模式无队列

        // ===== 接口方法：薄包装 =====

        public void Start(ITemplateService template, InspectionConfig config)
        {
            // HDevelop 导出方式无消费者线程，Start 仅置标志
            _running = true;
        }

        public bool Stop(int timeoutMs = 3000)
        {
            _running = false;
            return true; // 同步模式无需等待
        }

        public bool EnqueueFrame(HObject frame)
        {
            // 同步模式不支持入队，调用方应直接调用 Process
            throw new NotSupportedException("HDevelop 导出方式为同步处理，请直接调用 Process。");
        }

        public void ClearPendingFrames()
        {
            // 无队列，空实现
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _running = false;
        }

        // ===== HDevelop 导出代码：开始 =====
        // 以下代码模拟从 HDevelop 导出的 C# 代码，
        // 保留 ho_/hv_ 命名与堆叠式调用风格，仅在必要处加 try-finally。

        /// <inheritdoc/>
        public DetectionResult Process(ITemplateService template, InspectionConfig config, HObject frame)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            // HDevelop 导出的局部变量声明（全部 ho_/hv_ 前缀）
            HObject ho_GrayImage = null;
            HObject ho_TemplateRegion = null;
            HObject ho_ModelContours = null;
            HObject ho_ImageReduced = null;
            HObject ho_Rectangle = null;
            HObject ho_Rectangle1 = null;
            HObject ho_RegionTrans = null;
            HObject ho_ImageReduced1 = null;
            HObject ho_Regions = null;
            HObject ho_ConnectedRegions = null;
            HObject ho_SelectedRegions = null;
            HObject ho_Rectangle2 = null;
            HObject ho_RegionTrans1 = null;
            HObject ho_ImageReduced2 = null;
            HObject ho_Regions1 = null;
            HObject ho_ConnectedRegions1 = null;
            HObject ho_SelectedRegions1 = null;
            HObject ho_DisplayImage = null;
            HObject ho_ModelContoursAtPose = null;

            HTuple hv_ModelID = null;
            HTuple hv_Row = null;
            HTuple hv_Column = null;
            HTuple hv_Angle = null;
            HTuple hv_Score = null;
            HTuple hv_HomMat2D = null;
            HTuple hv_PinCount1 = null;
            HTuple hv_PinCount2 = null;
            HTuple hv_Area = null;

            var result = new DetectionResult { ResultText = "No found" };

            try
            {
                // 输入赋值
                ho_GrayImage = frame.CopyObj(1, -1);

                // 取模板资源（HDevelop 里这些是全局变量）
                ho_TemplateRegion = template.TemplateRegion.CopyObj(1, -1);
                ho_ModelContours = template.ModelContours.CopyObj(1, -1);
                hv_ModelID = template.ModelID.Clone();

                // ========== 步骤1：模板匹配 ==========
                // find_shape_model (GrayImage, ModelID, 0, rad(360), 0.65, 0, 0.5,
                //                  'least_squares_high', 0, 0.9, Row, Column, Angle, Score)
                HOperatorSet.FindShapeModel(
                    ho_GrayImage, hv_ModelID,
                    0.0, new HTuple(360).TupleRad(),
                    0.65, 0, 0.5,
                    "least_squares_high", 0, 0.9,
                    out hv_Row, out hv_Column, out hv_Angle, out hv_Score);

                if (hv_Score.Length == 0 || hv_Score[0].D < 0.65)
                {
                    result.ResultText = "No found";
                    result.MatchScore = hv_Score.Length > 0 ? (int)(hv_Score[0].D * 100) : 0;
                    ho_DisplayImage = ho_GrayImage.CopyObj(1, -1);
                    result.DisplayImage = ho_DisplayImage;
                    ho_DisplayImage = null;
                    return result;
                }

                result.MatchScore = (int)(hv_Score[0].D * 100);

                // ========== 步骤2：计算仿射变换矩阵 ==========
                // vector_angle_to_rigid (0, 0, 0, Row, Column, Angle, HomMat2D)
                HOperatorSet.VectorAngleToRigid(
                    0, 0, 0,
                    hv_Row[0].D, hv_Column[0].D, hv_Angle[0].D,
                    out hv_HomMat2D);

                HOperatorSet.AffineTransContourXld(ho_ModelContours, out ho_ModelContoursAtPose, hv_HomMat2D);

                // ========== 步骤3：检测区域1 ==========
                // 从 TemplateConfig 读坐标（HDevelop 里是写死的常量）
                var cfg = template.ExportConfig();

                // gen_rectangle2 (Rectangle, CheckRect1Row, CheckRect1Column,
                //                 CheckRect1Phi + Angle, CheckRect1Length1, CheckRect1Length2)
                HOperatorSet.GenRectangle2(
                    out ho_Rectangle,
                    cfg.CheckRect1Row, cfg.CheckRect1Column,
                    cfg.CheckRect1Phi + hv_Angle[0].D,
                    cfg.CheckRect1Length1, cfg.CheckRect1Length2);

                // affine_trans_region (Rectangle, RegionTrans, HomMat2D, 'nearest_neighbor')
                HOperatorSet.AffineTransRegion(
                    ho_Rectangle, out ho_RegionTrans, hv_HomMat2D, "nearest_neighbor");

                // reduce_domain (GrayImage, RegionTrans, ImageReduced)
                HOperatorSet.ReduceDomain(ho_GrayImage, ho_RegionTrans, out ho_ImageReduced);

                // threshold (ImageReduced, Regions, 80, 255)
                HOperatorSet.Threshold(ho_ImageReduced, out ho_Regions, 80, 255);

                // connection (Regions, ConnectedRegions)
                HOperatorSet.Connection(ho_Regions, out ho_ConnectedRegions);

                // select_shape (ConnectedRegions, SelectedRegions, 'area', 'and', 50, 5000)
                HOperatorSet.SelectShape(
                    ho_ConnectedRegions, out ho_SelectedRegions,
                    "area", "and", 50, 5000);

                // count_obj (SelectedRegions, PinCount1)
                hv_PinCount1 = ho_SelectedRegions.CountObj();
                result.PinCount = hv_PinCount1.I;

                // ========== 步骤4：检测区域2 ==========
                HOperatorSet.GenRectangle2(
                    out ho_Rectangle1,
                    cfg.CheckRect2Row, cfg.CheckRect2Column,
                    cfg.CheckRect2Phi + hv_Angle[0].D,
                    cfg.CheckRect2Length1, cfg.CheckRect2Length2);

                HOperatorSet.AffineTransRegion(
                    ho_Rectangle1, out ho_RegionTrans1, hv_HomMat2D, "nearest_neighbor");

                HOperatorSet.ReduceDomain(ho_GrayImage, ho_RegionTrans1, out ho_ImageReduced1);

                HOperatorSet.Threshold(ho_ImageReduced1, out ho_Regions1, 80, 255);
                HOperatorSet.Connection(ho_Regions1, out ho_ConnectedRegions1);
                HOperatorSet.SelectShape(
                    ho_ConnectedRegions1, out ho_SelectedRegions1,
                    "area", "and", 50, 5000);

                hv_PinCount2 = ho_SelectedRegions1.CountObj();
                result.PinCount2 = hv_PinCount2.I;

                // ========== 步骤5：合格判定 ==========
                bool ok1 = result.PinCount >= config.PinCountMin && result.PinCount <= config.PinCountMax;
                bool ok2 = result.PinCount2 >= config.PinCount2Min && result.PinCount2 <= config.PinCount2Max;
                result.IsOK = ok1 && ok2;
                result.ResultText = result.IsOK ? "OK" : "NG";
                result.ShouldTrigger = true;
                result.IsRisingEdge = true;

                // ========== 步骤6：生成显示图 ==========
                ho_DisplayImage = ho_GrayImage.CopyObj(1, -1);
                result.DisplayImage = ho_DisplayImage;
                ho_DisplayImage = null;

                result.ModelContours = ho_ModelContoursAtPose;
                ho_ModelContoursAtPose = null;

                result.DetectionRegion1 = ho_RegionTrans;
                ho_RegionTrans = null;

                result.DetectionRegion2 = ho_RegionTrans1;
                ho_RegionTrans1 = null;

                return result;
            }
            catch (HOperatorException ex)
            {
                result.IsError = true;
                result.ErrorMessage = $"Halcon 异常: {ex.Message}";
                result.ResultText = "检测出错";
                _logger.AddLog("Error", $"HDevelopExportDetectionService Halcon 异常: {ex.Message}");
                return result;
            }
            catch (Exception ex)
            {
                result.IsError = true;
                result.ErrorMessage = $"检测异常: {ex.Message}";
                result.ResultText = "检测出错";
                _logger.AddLog("Error", $"HDevelopExportDetectionService 异常: {ex.Message}");
                return result;
            }
            finally
            {
                // HDevelop 导出代码没有 finally，这里集中释放所有 HObject
                ho_GrayImage?.Dispose();
                ho_TemplateRegion?.Dispose();
                ho_ModelContours?.Dispose();
                ho_ImageReduced?.Dispose();
                ho_Rectangle?.Dispose();
                ho_Rectangle1?.Dispose();
                ho_RegionTrans?.Dispose();
                ho_ImageReduced1?.Dispose();
                ho_Regions?.Dispose();
                ho_ConnectedRegions?.Dispose();
                ho_SelectedRegions?.Dispose();
                ho_Rectangle2?.Dispose();
                ho_RegionTrans1?.Dispose();
                ho_ImageReduced2?.Dispose();
                ho_Regions1?.Dispose();
                ho_ConnectedRegions1?.Dispose();
                ho_SelectedRegions1?.Dispose();
                ho_DisplayImage?.Dispose();
                ho_ModelContoursAtPose?.Dispose();

                hv_ModelID?.Dispose();
            }
        }
    }
}
