using Core.Interfaces;
using Device_Link_LTSMC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Motion.Services
{
    public class SolenoidValueService : ISolenoidValueService
    {
        public bool CloseValue()
        {
            int res = LTSMC.smc_write_outbit(0, 2, 1);
            return res == 0;
        }

        public void Dispose()
        {

        }

        public bool OpenValue()
        {
            int res = LTSMC.smc_write_outbit(0, 2, 0);
            return res == 0;
        }
    }
}
