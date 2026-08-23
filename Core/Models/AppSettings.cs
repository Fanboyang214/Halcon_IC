using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    /// <summary>
    /// 应用根配置，绑定自 appsettings.json 的 "App" 节。
    /// 取代原 App.config 中明文硬编码的相机 SN、控制卡 IP、IO 端口等（风险点 H1）。
    /// </summary>
    public class AppSettings
    {
        public const string SectionName = "App";

        public string Environment { get; set; } = "Production";
        public string LogLevel { get; set; } = "Info";
        public string Culture { get; set; } = "zh-CN";

        public DatabaseSettings Database { get; set; } = new();
        public CameraSettings Camera { get; set; } = new();
        public MotionSettings Motion { get; set; } = new();
        public IoSettings Io { get; set; } = new();
        public VisionSettings Vision { get; set; } = new();
    }

    /// <summary>数据库配置（D1：连接串不再硬编码 sa/1，改用配置 + 可选 DPAPI 加密）。</summary>
    public class DatabaseSettings
    {
        /// <summary>连接串在 "ConnectionStrings" 节中的名称。</summary>
        public string ConnectionStringName { get; set; } = "Default";
    }

    /// <summary>相机配置（H1：序列号等全部配置化，不再散落 App.config）。</summary>
    public class CameraSettings
    {
        public const string SectionName = "App:Camera";
        public string Interface { get; set; } = "GigEV2";
        public string SerialNumber { get; set; } = "";
        public double ExposureMs { get; set; } = 8.0;
        public double Gain { get; set; } = 1.0;
        public int FrameRate { get; set; } = 30;
    }

    /// <summary>运动/控制卡配置（H1 控制卡 IP、H2/H3 速度与剔除延时参数）。</summary>
    public class MotionSettings
    {
        public const string SectionName = "App:Motion";
        public string ControllerIp { get; set; } = "192.168.1.10";
        public int ControllerPort { get; set; } = 8000;

        /// <summary>H3：当前传送带线速度（mm/s），ChangeSpeed 时实时更新。</summary>
        public double BeltSpeedMmPerSec { get; set; } = 200.0;

        /// <summary>相机到剔除口的物理距离（mm），用于剔除延时换算。</summary>
        public double CameraToRejectMm { get; set; } = 350.0;

        /// <summary>剔除动作持续时长（ms）。</summary>
        public double RejectDurationMs { get; set; } = 40.0;

        /// <summary>H3：急停速度（Vstop）。</summary>
        public double Vstop { get; set; } = 50.0;

        /// <summary>H3：运行速度（Vmove）。</summary>
        public double Vmove { get; set; } = 200.0;
    }

    /// <summary>IO 端口映射（H1：触发/剔除/就绪/报警位配置化）。</summary>
    public class IoSettings
    {
        public const string SectionName = "App:Io";
        public int TriggerInputBit { get; set; } = 0;
        public int RejectOutputBit { get; set; } = 1;
        public int ReadyOutputBit { get; set; } = 2;
        public int AlarmOutputBit { get; set; } = 3;
    }

    /// <summary>视觉/Halcon 配置（L2/L3：运行时路径与模板路径配置化，支持离线加载）。</summary>
    public class VisionSettings
    {
        public const string SectionName = "App:Vision";
        public string HalconRuntimePath { get; set; } = @"E:\ProgramFiles\MVTec\HALCON-24.11-Progress-Steady";
        public string ModelDirectory { get; set; } = "Models";
        public string DefaultModelName { get; set; } = "ic_shape.hdef";
        public double MinScore { get; set; } = 0.8;
        public int NumMatches { get; set; } = 1;
        public int TimeoutMs { get; set; } = 200;
    }
}
