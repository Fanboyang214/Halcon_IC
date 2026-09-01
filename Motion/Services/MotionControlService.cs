using Core.Interfaces;
using Device_Link_LTSMC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Motion.Services
{
    public class MotionControlService : IMotionControlService
    {
       

        /// <summary>
        /// 调整传送带运行速度
        /// 设置运动参数：起始速度3000，目标速度为targetSpeed
        /// 需要重启传送带后新速度才生效
        /// </summary>
        public bool ChangeSpeed(double targetSpeed)
        {
            int CH = LTSMC.smc_set_profile_unit(0, 0, 3000, targetSpeed, 0, 0, 0);
            LastErrorCode = (short)CH;
            return CH == 0;
        }


        public bool Connect()
        {
            int res = LTSMC.smc_board_init(0, 1, "COM3", 115200);
            return res == 0;
        }

        public bool Sevon()
        {
            int res = LTSMC.smc_write_sevon_pin(0, 0, 1);
            return res == 0;
        }

        public bool Vmove()
        {
            int res = LTSMC.smc_vmove(0,0,0);
            return res == 0;
        }

        public bool Vstop()
        {
            int res = LTSMC.smc_stop(0, 0, 0);
            return res == 0;
        }

         public short LastErrorCode
        {
            get;
            private set;
        }
    }

   
}
