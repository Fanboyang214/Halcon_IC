using Core.Interfaces;
using Core.Models;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Services
{
    /// <summary>
    /// <see cref="ITemplateService"/> 的 Halcon 24.11 实现。
    /// 使用 XLD 边缘子像素提取 + 基于轮廓的形状模板创建（CreateShapeModelXld）。
    /// </summary>
    /// <remarks>
    /// 治理点：
    ///  L2：不依赖 ICameraService，所有创建/加载方法均以 HObject 图像为入参；
    ///  L3：LoadTemplate 从 TemplateConfig 坐标 + 参考图重建模板；ExportConfig 导出当前坐标；
    ///  M2/M3：TemplateRegion/ModelContours/TemplateRegionXld/ModelID 所有权归本服务，
    ///        ClearTemplate 是唯一显式释放入口；Dispose 也会全量释放。
    /// </remarks>
    public sealed class TemplateService : ITemplateService
    {
        private HObject? _templateRegion;
        private HObject? _templateRegionXld;
        private HObject? _modelContours;
        private HTuple? _modelID;
        private HObject? _drawingObject;

        private double _row1, _column1, _row2, _column2;
        private string _templateName = string.Empty;
        private bool _disposed;

        /// <inheritdoc/>
        public bool IsTemplateCreated => _modelID != null;

        /// <inheritdoc/>
        public string TemplateName => _templateName;

        /// <inheritdoc/>
        public HObject TemplateRegion
        {
            get
            {
                if (!IsTemplateCreated && _templateRegion == null)
                    throw new InvalidOperationException("模板未创建。");
                return _templateRegion!;
            }
        }

        /// <inheritdoc/>
        public HTuple ModelID
        {
            get
            {
                if (_modelID == null)
                    throw new InvalidOperationException("模板未创建。");
                return _modelID;
            }
        }

        /// <inheritdoc/>
        public HObject ModelContours
        {
            get
            {
                if (!IsTemplateCreated)
                    throw new InvalidOperationException("模板未创建。");
                return _modelContours!;
            }
        }

        /// <inheritdoc/>
        public HObject TemplateRegionXld
        {
            get
            {
                if (_templateRegionXld == null)
                    throw new InvalidOperationException("模板区域未设置。");
                return _templateRegionXld;
            }
        }

        /// <inheritdoc/>
        public void CreateTemplate(HObject image, double row1, double column1, double row2, double column2)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (IsTemplateCreated)
                throw new InvalidOperationException("模板已创建，请先调用 ClearTemplate。");

            _row1 = row1; _column1 = column1; _row2 = row2; _column2 = column2;

            HObject? region = null;
            HObject? imageReduced = null;
            HObject? edges = null;
            HObject? contours = null;
            HTuple modelID;
            try
            {
                // 1. 生成矩形 ROI
                HOperatorSet.GenRectangle1(out region, row1, column1, row2, column2);
                _templateRegion = region;
                region = null; // 所有权转移

                // 2. 缩减到 ROI 区域
                HOperatorSet.ReduceDomain(image, _templateRegion, out imageReduced);

                // 3. 亚像素边缘提取（基于 XLD）
                HOperatorSet.EdgesSubPix(imageReduced, out edges, "canny", 1, 20, 40);

                // 4. 基于 XLD 轮廓创建形状模板
                //    参数顺序: contours, numLevels, angleStart, angleExtent, angleStep,
                //             optimization, metric, minContrast, modelID
                HOperatorSet.CreateShapeModelXld(
                    edges,
                    "auto",
                    0.0,
                    new HTuple(360).TupleRad(),
                    new HTuple(5).TupleRad(),
                    "auto",
                    "use_polarity",
                    10,
                    out modelID);
                _modelID = modelID;

                // 5. 获取模板轮廓用于可视化
                HOperatorSet.GetShapeModelContours(out contours, modelID, 1);
                _modelContours = contours;
                contours = null; // 所有权转移

                // 6. 生成模板矩形区域的 XLD 轮廓（4 个角点的闭合多边形）
                BuildTemplateRegionXld(row1, column1, row2, column2);
            }
            finally
            {
                // 中间对象释放，已转移到字段的除外
                imageReduced?.Dispose();
                edges?.Dispose();
                // region/contours 已转移为字段，不在此释放
            }
        }

        /// <inheritdoc/>
        public void SetTemplateRegion(double row1, double column1, double row2, double column2)
        {
            // 仅更新显示用的矩形区域与 XLD，不影响已创建的形状模板
            var oldRegion = _templateRegion;
            var oldXld = _templateRegionXld;

            HObject? region = null;
            try
            {
                HOperatorSet.GenRectangle1(out region, row1, column1, row2, column2);
                _templateRegion = region;
                region = null; // 所有权转移
                _row1 = row1; _column1 = column1; _row2 = row2; _column2 = column2;

                BuildTemplateRegionXld(row1, column1, row2, column2);
            }
            finally
            {
                oldRegion?.Dispose();
                oldXld?.Dispose();
            }
        }

        /// <inheritdoc/>
        public void LoadTemplate(TemplateConfig config, HObject referenceImage)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (referenceImage == null) throw new ArgumentNullException(nameof(referenceImage));
            if (IsTemplateCreated)
                throw new InvalidOperationException("模板已创建，请先调用 ClearTemplate。");

            _templateName = config.TemplateName;
            CreateTemplate(referenceImage,
                config.TemplateRow1, config.TemplateColumn1,
                config.TemplateRow2, config.TemplateColumn2);
            // 模板名称在 CreateTemplate 中会被重置为空，这里恢复
            _templateName = config.TemplateName;
        }

        /// <inheritdoc/>
        public TemplateConfig ExportConfig()
        {
            if (!IsTemplateCreated)
                throw new InvalidOperationException("模板未创建，无法导出配置。");

            return new TemplateConfig
            {
                TemplateName = _templateName,
                TemplateRow1 = _row1,
                TemplateColumn1 = _column1,
                TemplateRow2 = _row2,
                TemplateColumn2 = _column2
            };
        }

        /// <inheritdoc/>
        public void ClearTemplate()
        {
            // 顺序释放：先释放模板 ID，再释放图像对象
            if (_modelID != null)
            {
                try { HOperatorSet.ClearShapeModel(_modelID); }
                catch (HOperatorException) { /* 忽略已释放或无效 */ }
                _modelID = null;
            }
            _modelContours?.Dispose();
            _modelContours = null;

            _templateRegion?.Dispose();
            _templateRegion = null;

            _templateRegionXld?.Dispose();
            _templateRegionXld = null;

            _templateName = string.Empty;
            _row1 = _column1 = _row2 = _column2 = 0;
        }

        /// <summary>
        /// 由 4 个角点构建矩形闭合 XLD 轮廓，所有权转给 <see cref="_templateRegionXld"/>。
        /// </summary>
        private void BuildTemplateRegionXld(double row1, double column1, double row2, double column2)
        {
            var oldXld = _templateRegionXld;
            try
            {
                // 闭合矩形的 5 个点（首尾重合）
                var rows = new HTuple(row1, row1, row2, row2, row1);
                var cols = new HTuple(column1, column2, column2, column1, column1);
                HOperatorSet.GenContourPolygonXld(out var xld, rows, cols);
                _templateRegionXld = xld;
            }
            finally
            {
                oldXld?.Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ClearTemplate();
        }
    }
}
