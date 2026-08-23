using Core.Models;
using Prism.Events;

namespace Core.Events
{
    /// <summary>
    /// 相机帧采集事件。CameraService 每帧 Clone 后发布，
    /// 订阅方（MainViewModel 入队、DetectionService 消费）通过 IEventAggregator 订阅。
    /// 默认 ThreadOption.UIThread，订阅方按需选择 PublisherThread 或 BackgroundThread。
    /// </summary>
    public class ImageGrabbedEvent : PubSubEvent<ImageGrabbedPayload>
    {
    }
}
