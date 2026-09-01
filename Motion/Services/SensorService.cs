using Core.Interfaces;
using Device_Link_LTSMC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Motion.Services
{
    public class SensorService : ISensorService
    {
        public bool ReadSensorState()
        {
            int res = LTSMC.smc_read_inbit(0, 0);
            return res == 0;
        }
    }
}
