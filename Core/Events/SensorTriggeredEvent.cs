using Prism.Events;
using System;

namespace Core.Events
{
    public class SensorTriggeredEvent : PubSubEvent<SensorTriggeredPayload>
    {
    }

    public class SensorTriggeredPayload
    {


        public int SensorStatue { get; set; } = 1;
        
    }
}