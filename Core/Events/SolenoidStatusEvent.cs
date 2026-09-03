using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Events
{
    public class SolenoidStatusEvent:PubSubEvent<SolenoidStatus>
    {
    }
    public class SolenoidStatus
    {
        public int solenoidStatus { get; set; } = 1;
    }
}
