using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces
{
    /// <summary>
    /// 强类型配置中心服务。封装原 App.config 明文硬编码参数的配置化读取与运行时管理。
    /// 对应重构文档风险点：H1（相机 SN / 控制卡 IP / IO 端口）、H2/H3（速度→剔除延时）、D1（连接串加密）。
    /// </summary>
    public interface IConfigService
    {
        /// <summary>当前强类型配置快照（支持文件热重载）。</summary>
        AppSettings Current { get; }
        DatabaseSettings Database { get; }
        CameraSettings Camera { get; }
        MotionSettings Motion { get; }
        IoSettings Io { get; }
        VisionSettings Vision { get; }

        /// <summary>返回（已解密的）默认连接串。D1：明文 sa/1 改为配置 + 可选 DPAPI 加密。</summary>
        string GetConnectionString();

        /// <summary>按名称返回（已解密的）连接串。</summary>
        string GetConnectionString(string name);

        /// <summary>H2：根据给定线速度计算剔除延时(ms)。</summary>
        double ComputeRejectDelay(double beltSpeedMmPerSec);

        /// <summary>H2：使用当前配置中的线速度计算剔除延时(ms)。</summary>
        double ComputeRejectDelay();

        /// <summary>把内存中的配置写回 appsettings.json（供 Settings 模块 UI 保存生效）。</summary>
        void Save();

        /// <summary>把指定配置对象写回 appsettings.json。</summary>
        void Save(AppSettings settings);

        /// <summary>配置文件热重载时触发（订阅方据此刷新运行时参数）。</summary>
        event EventHandler<ConfigChangedEventArgs>? Changed;
    }
}
