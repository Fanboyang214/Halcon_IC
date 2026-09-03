using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces
{
    /// <summary>
    /// 运动控制服务接口
    /// 封装对运动控制卡（Leadshine LTSMC）的操作，包括连接、使能、运动、调速
    /// 通过接口抽象，ViewModel不依赖具体的硬件实现，便于测试和替换
    /// </summary>
    public interface IMotionControlService:IDisposable
    {
        /// <summary>
        /// 连接运动控制卡
        /// </summary>
        /// <returns>true=连接成功，false=连接失败或已连接</returns>
        bool Connect();

        /// <summary>
        /// 电机使能（伺服使能）
        /// 使能后电机才能运动
        /// </summary>
        /// <returns>true=使能成功</returns>
        bool Sevon();

        /// <summary>
        /// 启动传送带运动（连续运动模式）
        /// </summary>
        /// <returns>true=启动成功</returns>
        bool Vmove();

        /// <summary>
        /// 停止传送带运动
        /// </summary>
        /// <returns>true=停止成功</returns>
        bool Vstop();

        /// <summary>
        /// 调整传送带运行速度
        /// 需要先停止传送带再调速，重启传送带后生效
        /// </summary>
        /// <param name="targetSpeed">目标速度（单位/秒）</param>
        /// <returns>true=调速成功</returns>
        bool ChangeSpeed(double targetSpeed);

        /// <summary>
        /// 最近一次操作的错误码
        /// 当操作返回false时，可通过此属性获取具体错误码
        /// </summary>
        short LastErrorCode { get; }
    }
}
