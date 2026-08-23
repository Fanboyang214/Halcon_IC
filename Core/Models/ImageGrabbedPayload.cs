using HalconDotNet;
using System;

namespace Core.Models
{
    /// <summary>
    /// 图像采集事件载荷。M3：发布方 Clone 一份 HObject 并转移所有权，订阅方负责 Dispose。
    /// 一个载荷实例对应一帧图像，禁止多订阅方共享同一实例后各自 Dispose。
    /// 若需多消费者，各自再 Clone。
    /// </summary>
    public sealed class ImageGrabbedPayload : IDisposable
    {
        /// <summary>采集到的图像（所有权归订阅方）。</summary>
        public HObject Image { get; }

        /// <summary>帧序号（从 0 递增）。</summary>
        public long FrameIndex { get; }

        /// <summary>采集时间戳。</summary>
        public DateTime Timestamp { get; }

        public ImageGrabbedPayload(HObject image, long frameIndex, DateTime timestamp)
        {
            Image = image ?? throw new ArgumentNullException(nameof(image));
            FrameIndex = frameIndex;
            Timestamp = timestamp;
        }

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Image?.Dispose();
        }
    }
}
