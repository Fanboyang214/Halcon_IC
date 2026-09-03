using Core.Interfaces;
using Device_Link_LTSMC;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;

namespace Motion.Services
{
    public class SensorService : ISensorService
    {
        private const int QueueCapacity = 2;

        private BlockingCollection<int> _sensorsQueue;

        public SensorService()
        {
            _sensorsQueue = new BlockingCollection<int>(QueueCapacity);
        }

        public void Dispose()
        {
            
        }

        public bool ReadSensorState()
        {
            int res = LTSMC.smc_read_inbit(0, 0);
            
            return res == 0;
        }

        public double ReadSensorPosition()
        {
            return LTSMC.smc_get_encoder(0, 0);
        }
    }
}
