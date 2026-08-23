using Core.Models;
using HalconDotNet;
using System;

namespace Core.Interfaces
{
    /// <summary>
    /// 视觉检测核心算法服务契约。
    /// 对应重构文档治理点：
    ///   L2：解耦模板与相机——本服务依赖 <see cref="ITemplateService"/> 已创建的模板，
    ///       不直接调用 ICameraService；图像由调用方作为入参传入；
    ///   L4 漏帧处理：内部 BlockingCollection 生产者-消费者，入队非阻塞，队列满即丢帧；
    ///   M1 异常隔离：单帧检测异常不传播给调用方，仅记录并返回 IsError=true；
    ///   M3 所有权：返回的 DisplayImage 归调用方所有，调用方负责 Dispose；
    ///   T2 跨线程可见性：队列运行标志用 Interlocked/volatile；
    ///   T3 启停互斥：Start/Stop 内部加锁。
    /// </summary>
    public interface IDetectionService : IDisposable
    {
        /// <summary>检测是否正在运行（消费者线程是否在工作）。</summary>
        bool IsRunning { get; }

        /// <summary>当前待处理队列中的帧数（用于诊断漏帧压力）。</summary>
        int PendingCount { get; }

        /// <summary>
        /// 启动检测消费者线程。要求模板已创建，否则抛异常。
        /// 内部 BlockingCollection 开始接收帧；多次调用幂等（已在运行则直接返回）。
        /// </summary>
        /// <param name="template">已创建模板的模板服务实例。</param>
        /// <param name="config">检测配置（最小匹配分、针脚合格范围、下降沿超时等）。</param>
        /// <exception cref="ArgumentNullException">template 或 config 为 null。</exception>
        /// <exception cref="InvalidOperationException">模板未创建。</exception>
        void Start(ITemplateService template, InspectionConfig config);

        /// <summary>
        /// 停止检测消费者线程，清空待处理队列。
        /// 等待消费者线程真正退出后再返回（带超时），避免后台线程访问已释放资源。
        /// </summary>
        /// <param name="timeoutMs">等待退出的超时时间，默认 3000ms。</param>
        /// <returns>true 表示线程已退出；false 表示超时。</returns>
        bool Stop(int timeoutMs = 3000);

        /// <summary>
        /// 异步入队一帧图像（L4 生产者入口）。
        /// 非阻塞：若队列已满则丢弃当前帧并记录，保证相机采集线程不阻塞。
        /// 所有权约定：调用方必须在确认不再使用 frame 后自行 Dispose；
        /// 本服务内部会 Clone 一份用于处理，原始 frame 不在服务内释放。
        /// </summary>
        /// <param name="frame">待检测的灰度图像（HObject）。</param>
        /// <returns>true 表示入队成功；false 表示队列满已丢帧。</returns>
        /// <exception cref="InvalidOperationException">检测未启动。</exception>
        bool EnqueueFrame(HObject frame);

        /// <summary>
        /// 同步处理单帧检测（L4 之外的可选入口，用于单步调试或无队列模式）。
        /// 内部执行完整 Process 算法：FindShapeModel → 计算旋转角度 → 两个检测区域针脚计数 →
        /// 合格判定 → 生成 DisplayImage。
        /// </summary>
        /// <param name="template">已创建模板的模板服务实例。</param>
        /// <param name="config">检测配置。</param>
        /// <param name="frame">待检测的灰度图像。</param>
        /// <returns>检测结果，包含匹配分、合格判定、针脚数、叠加显示图等。</returns>
        /// <exception cref="ArgumentNullException">template/config/frame 为 null。</exception>
        /// <exception cref="InvalidOperationException">模板未创建。</exception>
        DetectionResult Process(ITemplateService template, InspectionConfig config, HObject frame);

        /// <summary>
        /// 清空待处理队列中尚未消费的帧（不影响正在处理的当前帧）。
        /// 用于切换产品或紧急停止时丢弃积压帧。
        /// </summary>
        void ClearPendingFrames();
    }
}
