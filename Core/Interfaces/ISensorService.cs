using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces
{
    public interface ISensorService:IDisposable
    {

        /// <summary>
        /// 读取传感器状态
        /// 读取控制卡0的输入端口0（IN0）
        /// </summary>
        /// <returns>true=芯片经过（传感器触发），false=无芯片（传感器常态）</returns>
        public bool ReadSensorState();

        public double ReadSensorPosition();
       
    }
}
