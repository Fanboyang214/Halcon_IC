using Prism.Events;
using System;

namespace Core.Events
{
    public class SensorTriggeredEvent : PubSubEvent<SensorTriggeredPayload>
    {
    }

    public class SensorTriggeredPayload
    {
        public DateTime TriggerTime { get; set; }

        public int SensorStatue { get; set; }
        public double ConveyorPosition { get; set; }
    }
}