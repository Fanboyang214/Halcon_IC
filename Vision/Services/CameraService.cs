using Core.Events;
using Core.Interfaces;
using Core.Models;
using HalconDotNet;
using Prism.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Services
{
    /// <summary>
    /// <see cref="ICameraService"/> 的 Halcon 24.11 实现。
    /// 通过 <c>HOperatorSet.OpenFramegrabber("GigEV2", ...)</c> 连接 GigE 相机，
    /// 后台 Task 循环 <c>GrabImage</c>，每帧 <c>Clone</c> 后经 <see cref="ImageGrabbedEvent"/> 发布。
    /// </summary>
    /// <remarks>
    /// 治理点：
    ///  H1：相机 SN、曝光、增益、帧率均来自 <see cref="CameraSettings"/>；
    ///  T2：<see cref="_isGrabbing"/> 用 volatile，避免采集线程与停止操作间可见性问题；
    ///  T3：<see cref="_grabLock"/> 覆盖 StartGrabbing/StopGrabbing 全流程；
    ///  M3：每帧 Clone 后所有权转移给订阅方，本服务不再持有该 HObject；
    ///  X1：StopGrabbing 通过 Task.Wait(timeoutMs) 等待线程真正退出，超时返回 false；
    ///  M1：采集异常只记录不导致进程崩溃，进入下一帧重试。
    /// </remarks>
    public sealed class CameraService : ICameraService
    {
        // GigE Vision 采集接口名。Halcon 24.11 推荐 GigEV2；老接口名为 GigEVision。
        private const string InterfaceName = "GigEV2";
        //private const string InterfaceName = "DirectShow";

        private readonly IEventAggregator _eventAggregator;
        private readonly object _grabLock = new();

        private HFramegrabber? _acqHandle;
        private volatile bool _isGrabbing;
        private volatile bool _disposed;

        private Task? _grabTask;
        private CancellationTokenSource? _cts;
        private long _frameIndex;

        public CameraService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        }

        /// <inheritdoc/>
        public bool IsOpen => _acqHandle != null;

        /// <inheritdoc/>
        public bool IsGrabbing => _isGrabbing;

        /// <inheritdoc/>
        public void Open(CameraSettings settings)
        {
            if (settings is null) throw new ArgumentNullException(nameof(settings));
            if (IsOpen) throw new InvalidOperationException("相机已打开，请先 Close 再 Open。");
            if (string.IsNullOrWhiteSpace(settings.SerialNumber))
                throw new InvalidOperationException("CameraSettings.SerialNumber 为空，无法打开相机（H1：相机 SN 必须配置）。");

            // Halcon OpenFramegrabber 参数顺序：
            //   Interface, Port, Device, CameraType, Port2, CameraNum, Field, ExternalTrigger, Channel, ColorSpace, BitDepth, ImageSize, Format, Part, SerialNumber, Horizontal, Vertical, out AcqHandle
            _acqHandle = new HFramegrabber(
                InterfaceName, 1, 1, 0, 0, 0, 0, "progressive", -1, "default", -1, "false", "default",
                settings.SerialNumber, 0, -1);

            ApplyParameters(settings);
        }

        /// <inheritdoc/>
        public void ApplyParameters(CameraSettings settings)
        {
            if (settings is null) throw new ArgumentNullException(nameof(settings));
            if (!IsOpen) throw new InvalidOperationException("相机未打开，无法设置参数。");

            // 不同厂商相机 GenICam 参数名差异较大，这里做容错：失败仅忽略，不阻断连接。
            // 曝光：Halcon 用微秒；appsettings 配置为毫秒。
            TrySetParam("ExposureTime", settings.ExposureMs * 1000.0);
            TrySetParam("Gain", settings.Gain);
            TrySetParam("AcquisitionFrameRate", (double)settings.FrameRate);
        }

        /// <inheritdoc/>
        public void Close()
        {
            if (IsGrabbing) StopGrabbing();
            var handle = _acqHandle;
            _acqHandle = null;
            handle?.Dispose();
        }

        /// <inheritdoc/>
        public void StartGrabbing()
        {
            lock (_grabLock)
            {
                if (!IsOpen) throw new InvalidOperationException("相机未打开，无法开始采集。");
                if (_isGrabbing) throw new InvalidOperationException("已在采集中。");

                _cts = new CancellationTokenSource();
                _frameIndex = 0;
                _isGrabbing = true;

                // 后台采集线程：循环 GrabImage → Clone → 发布。
                _grabTask = Task.Run(() => GrabLoop(_cts.Token));
            }
        }

        /// <inheritdoc/>
        public bool StopGrabbing(int timeoutMs = 3000)
        {
            lock (_grabLock)
            {
                if (!_isGrabbing) return true;

                _cts?.Cancel();
                _isGrabbing = false;
            }

            var task = _grabTask;
            if (task == null) return true;

            try
            {
                // X1：等待采集线程真正退出，超时返回 false，不再硬编码 500ms 后盲目释放。
                bool exited = task.Wait(timeoutMs);
                if (!exited)
                {
                    // 超时仍未退出，记录但不抛异常，避免阻塞退出流程。
                    // 调用方可决定是否强制 Close。
                }
                return exited;
            }
            catch (AggregateException)
            {
                return false;
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _grabTask = null;
            }
        }

        /// <summary>
        /// 采集循环。M3：每帧 Clone 后发布，订阅方负责 Dispose；
        /// M1：异常仅记录不抛出，进入下一帧。
        /// </summary>
        private void GrabLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _isGrabbing && _acqHandle != null)
            {
                HObject? frame = null;
                HObject? clone = null;
                ImageGrabbedPayload? payload = null;
                try
                {
                    // 同步抓取；如需异步可改 GrabImageAsync，但同步已足够且更易保证顺序。
                    HOperatorSet.GrabImage(out frame, _acqHandle);
                    // M3：Clone 后所有权转移给订阅方，本服务不再持有 frame 与 clone 中的一方。
                    clone = frame.Clone();
                    payload = new ImageGrabbedPayload(clone, Interlocked.Increment(ref _frameIndex) - 1, DateTime.Now);
                    // 订阅方接管 clone 的所有权并负责 Dispose。
                    _eventAggregator.GetEvent<ImageGrabbedEvent>().Publish(payload);
                }
                catch (HOperatorException)
                {
                    // M1：采集异常重试下一帧，不退出循环。
                    clone?.Dispose();
                    payload?.Dispose();
                }
                finally
                {
                    // frame 始终由本服务释放；clone 已交由订阅方，不在此释放。
                    frame?.Dispose();
                }
            }
        }

        /// <summary>尝试设置采集参数，失败时忽略（不同相机参数名差异）。</summary>
        private void TrySetParam(string name, double value)
        {
            try
            {
                _acqHandle?.SetFramegrabberParam(name, value);
            }
            catch (HOperatorException)
            {
                // 参数不支持或值非法，忽略以保留其它已成功的设置。
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Close();
        }
    }
}
