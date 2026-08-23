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
    /// <see cref="ICameraService"/> 的 MediaFoundation 实现，用于笔记本内置摄像头等 USB 相机。
    /// 通过 <c>HOperatorSet.OpenFramegrabber("MediaFoundation", ...)</c> 连接，
    /// 后台 Task 循环 <c>GrabImage</c>，每帧 <c>Clone</c> 后经 <see cref="ImageGrabbedEvent"/> 发布。
    /// </summary>
    /// <remarks>
    /// 与 GigE 版本的差异：
    ///  1. 接口名为 "MediaFoundation"，使用 Windows Media Foundation 替代 DirectShow；
    ///  2. 使用设备索引（0=第一个摄像头）替代序列号；
    ///  3. USB 摄像头通常不支持 GenICam 曝光/增益参数，TrySetParam 失败会被静默忽略；
    ///  4. 其余采集循环、线程管理、资源所有权模型与 <see cref="CameraService"/> 保持一致。
    /// </remarks>
    public sealed class DirectShowCameraService : ICameraService
    {
        private const string InterfaceName = "MediaFoundation";

        private readonly IEventAggregator _eventAggregator;
        private readonly object _grabLock = new();

        private HFramegrabber? _acqHandle;
        private volatile bool _isGrabbing;
        private volatile bool _disposed;

        private Task? _grabTask;
        private CancellationTokenSource? _cts;
        private long _frameIndex;

        public DirectShowCameraService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        }

        public bool IsOpen => _acqHandle != null;

        public bool IsGrabbing => _isGrabbing;

        public void Open(CameraSettings settings)
        {
            if (settings is null) throw new ArgumentNullException(nameof(settings));
            if (IsOpen) throw new InvalidOperationException("相机已打开，请先 Close 再 Open。");

            int deviceIndex = 0;
            if (!string.IsNullOrWhiteSpace(settings.SerialNumber))
            {
                int.TryParse(settings.SerialNumber, out deviceIndex);
            }

            _acqHandle = new HFramegrabber(
                InterfaceName, 1, deviceIndex, 0, 0, 0, 0, "progressive", -1, "default", -1, "false", "default",
                "default", 0, -1);

            ApplyParameters(settings);
        }

        public void ApplyParameters(CameraSettings settings)
        {
            if (settings is null) throw new ArgumentNullException(nameof(settings));
            if (!IsOpen) throw new InvalidOperationException("相机未打开，无法设置参数。");

            TrySetParam("ExposureTime", settings.ExposureMs * 1000.0);
            TrySetParam("Gain", settings.Gain);
            TrySetParam("AcquisitionFrameRate", (double)settings.FrameRate);
        }

        public void Close()
        {
            if (IsGrabbing) StopGrabbing();
            var handle = _acqHandle;
            _acqHandle = null;
            handle?.Dispose();
        }

        public void StartGrabbing()
        {
            lock (_grabLock)
            {
                if (!IsOpen) throw new InvalidOperationException("相机未打开，无法开始采集。");
                if (_isGrabbing) throw new InvalidOperationException("已在采集中。");

                _cts = new CancellationTokenSource();
                _frameIndex = 0;
                _isGrabbing = true;

                _grabTask = Task.Run(() => GrabLoop(_cts.Token));
            }
        }

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
                bool exited = task.Wait(timeoutMs);
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

        private void GrabLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _isGrabbing && _acqHandle != null)
            {
                HObject? frame = null;
                HObject? clone = null;
                ImageGrabbedPayload? payload = null;
                try
                {
                    HOperatorSet.GrabImage(out frame, _acqHandle);
                    clone = frame.Clone();
                    payload = new ImageGrabbedPayload(clone, Interlocked.Increment(ref _frameIndex) - 1, DateTime.Now);
                    _eventAggregator.GetEvent<ImageGrabbedEvent>().Publish(payload);
                }
                catch (HOperatorException)
                {
                    clone?.Dispose();
                    payload?.Dispose();
                }
                finally
                {
                    frame?.Dispose();
                }
            }
        }

        private void TrySetParam(string name, double value)
        {
            try
            {
                _acqHandle?.SetFramegrabberParam(name, value);
            }
            catch (HOperatorException)
            {
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