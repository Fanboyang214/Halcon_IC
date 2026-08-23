using Core.Models;
using System;

namespace Core.Interfaces
{
    /// <summary>
    /// 相机服务契约。封装 GigE 相机的连接、参数配置与连续采集。
    /// 对应重构文档风险点：
    ///   H1：相机 SN/曝光/增益等参数从 appsettings.json 读取，不再硬编码；
    ///   T3：StartGrabbing/StopGrabbing 必须加锁，覆盖启停全流程；
    ///   T2：跨线程状态标志使用 volatile；
    ///   M3：每帧 Clone 后发布，所有权转移给订阅方；
    ///   X1：StopGrabbing 等待采集线程真正退出，不再硬编码 500ms。
    /// 图像帧通过 <see cref="ImageGrabbedEvent"/>（Prism EventAggregator）广播，
    /// 订阅方负责 Dispose 携带的 HObject。
    /// </summary>
    public interface ICameraService : IDisposable
    {
        /// <summary>相机是否已打开（连接成功）。</summary>
        bool IsOpen { get; }

        /// <summary>是否正在连续采集。</summary>
        bool IsGrabbing { get; }

        /// <summary>
        /// 按配置打开相机并应用曝光/增益/帧率参数。H1：参数全部来自 <see cref="CameraSettings"/>。
        /// </summary>
        /// <param name="settings">相机配置（序列号、曝光、增益、帧率）。</param>
        /// <exception cref="InvalidOperationException">相机已打开时重复打开。</exception>
        void Open(CameraSettings settings);

        /// <summary>
        /// 关闭相机并释放采集句柄。若正在采集，先停止采集线程再关闭。
        /// </summary>
        void Close();

        /// <summary>
        /// 启动后台采集线程，循环 GrabImage 并通过 <see cref="ImageGrabbedEvent"/> 发布帧。
        /// T3：内部加锁，与 StopGrabbing 互斥。
        /// </summary>
        /// <exception cref="InvalidOperationException">相机未打开或已在采集。</exception>
        void StartGrabbing();

        /// <summary>
        /// 停止采集线程。X1：等待采集 Task 真正退出后再返回（带超时），避免后台线程访问已释放句柄。
        /// T3：内部加锁。
        /// </summary>
        /// <param name="timeoutMs">等待退出的超时时间，默认 3000ms。</param>
        /// <returns>true 表示线程已退出；false 表示超时。</returns>
        bool StopGrabbing(int timeoutMs = 3000);

        /// <summary>
        /// 在不重开相机的情况下，更新曝光/增益/帧率等参数。
        /// 供 Settings 模块热更新使用（H1）。
        /// </summary>
        void ApplyParameters(CameraSettings settings);
    }
}
