using Core.Models;
using HalconDotNet;
using System;

namespace Core.Interfaces
{
    /// <summary>
    /// 形状模板创建与管理服务契约。
    /// 对应重构文档治理点：
    ///   L2：解耦相机与模板生命周期——本服务不依赖 ICameraService，
    ///       CreateTemplate/LoadTemplate 均以传入的 HObject 图像为输入；
    ///   L3：支持离线加载——从已保存的 <see cref="TemplateConfig"/> JSON 配置 + 参考图像重建模板；
    ///   M2/M3：资源所有权归本服务——TemplateRegion/ModelContours/ModelID
    ///       由本服务创建与释放，调用方不得自行 Dispose；
    ///       ClearTemplate 是唯一显式释放入口，Dispose 时也会全量释放。
    /// </summary>
    public interface ITemplateService : IDisposable
    {
        /// <summary>模板是否已创建（可用于判断是否允许启动检测）。</summary>
        bool IsTemplateCreated { get; }

        /// <summary>当前模板名称（与 <see cref="TemplateConfig.TemplateName"/> 对齐）。</summary>
        string TemplateName { get; }

        /// <summary>
        /// 模板区域（矩形 ROI，<see cref="HOperatorSet.GenRectangle1"/> 生成）。
        /// 所有权归本服务，调用方仅可用于显示，禁止 Dispose。
        /// </summary>
        HObject TemplateRegion { get; }

        /// <summary>
        /// 形状模板 ID（<see cref="HOperatorSet.CreateShapeModelXld"/> 生成）。
        /// 所有权归本服务，<see cref="ClearTemplate"/> 时通过 ClearShapeModel 释放。
        /// </summary>
        HTuple ModelID { get; }

        /// <summary>
        /// 模板轮廓（<see cref="HOperatorSet.GetShapeModelContours"/> 生成，用于可视化）。
        /// 所有权归本服务，调用方仅可用于显示，禁止 Dispose。
        /// </summary>
        HObject ModelContours { get; }

        /// <summary>
        /// 模板区域的 XLD 轮廓（<see cref="HOperatorSet.GenContourPolygonXld"/> 生成）。
        /// 仅用于界面叠加显示；通过 <see cref="SetTemplateRegion"/> 或
        /// <see cref="CreateTemplate"/> 间接设置，调用方不得直接赋值。
        /// </summary>
        HObject TemplateRegionXld { get; }

        /// <summary>
        /// 在线创建模板：从模板图像与矩形 ROI 区域生成形状模板。
        /// 内部流程：GenRectangle1 → EdgesSubPix → CreateShapeModelXld → GetShapeModelContours。
        /// </summary>
        /// <param name="image">模板图像（通常是首帧或离线参考图）。</param>
        /// <param name="row1">矩形区域左上角行坐标。</param>
        /// <param name="column1">矩形区域左上角列坐标。</param>
        /// <param name="row2">矩形区域右下角行坐标。</param>
        /// <param name="column2">矩形区域右下角列坐标。</param>
        /// <exception cref="ArgumentNullException">image 为 null。</exception>
        /// <exception cref="InvalidOperationException">模板已创建，需先 ClearTemplate。</exception>
        void CreateTemplate(HObject image, double row1, double column1, double row2, double column2);

        /// <summary>
        /// 仅设置/更新模板矩形区域，不创建形状模板。
        /// 供界面交互绘制 ROI 时使用，显示模板位置。
        /// </summary>
        void SetTemplateRegion(double row1, double column1, double row2, double column2);

        /// <summary>
        /// 离线加载模板（L3）：从已保存的 <see cref="TemplateConfig"/> 配置 + 参考图像重建模板。
        /// 适用场景：换产品时无需重新绘制 ROI，直接加载上次保存的坐标与参考图。
        /// </summary>
        /// <param name="config">模板配置（含 ROI 坐标与模板名称）。</param>
        /// <param name="referenceImage">参考图像（用于在指定 ROI 内重新提取边缘与创建模板）。</param>
        /// <exception cref="ArgumentNullException">config 或 referenceImage 为 null。</exception>
        /// <exception cref="InvalidOperationException">模板已创建，需先 ClearTemplate。</exception>
        void LoadTemplate(TemplateConfig config, HObject referenceImage);

        /// <summary>
        /// 导出当前模板坐标配置，供 JSON 持久化（L3）。
        /// 调用方拿到 <see cref="TemplateConfig"/> 后可调用其 <see cref="TemplateConfig.ToJson"/> 写文件。
        /// </summary>
        /// <exception cref="InvalidOperationException">模板未创建。</exception>
        TemplateConfig ExportConfig();

        /// <summary>
        /// 清除模板资源（M2/M3）。
        /// 释放 TemplateRegion/ModelContours/TemplateRegionXld 与 ModelID（ClearShapeModel），
        /// 重置 <see cref="IsTemplateCreated"/>。切换产品或关闭程序时调用。
        /// </summary>
        void ClearTemplate();
    }
}
