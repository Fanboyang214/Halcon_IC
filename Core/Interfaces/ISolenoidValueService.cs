using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces
{
    /// <summary>
    /// 电磁阀服务接口
    /// 定义控制电磁阀开关的标准契约
    /// 实现类：SolenoidValueService
    /// 用于剔除不合格芯片
    /// </summary>
    public interface ISolenoidValueService
    {

        /// <summary>
        /// 打开电磁阀
        /// 触发气缸动作，将不合格芯片从传送带上剔除
        /// </summary>
        /// <returns>true=操作成功，false=操作失败</returns>
        bool OpenValue();

        /// <summary>
        /// 关闭电磁阀
        /// 复位气缸，准备下一次剔除动作
        /// </summary>
        /// <returns>true=操作成功，false=操作失败</returns>
        bool CloseValue();
    }
}
