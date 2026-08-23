using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastruce.Services
{
    /// <summary>
    /// <see cref="IConfigService"/> 的默认实现。
    /// 自建 IConfiguration + 强类型绑定，提供配置访问、热重载、连接串解密与写回。
    /// </summary>
    public sealed class ConfigService : IConfigService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly AppSettings _appSettings;
        private readonly string _filePath;
        private readonly object _sync = new();
        private IDisposable? _changeToken;
        private bool _disposed;

        public event EventHandler<ConfigChangedEventArgs>? Changed;

        public IConfiguration Configuration => _configuration;

        public ConfigService()
            : this(Path.Combine(AppContext.BaseDirectory, "appsettings.json")) { }

        public ConfigService(string filePath)
        {
            _filePath = filePath;
            try
            {
                _configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile(filePath, optional: true, reloadOnChange: true)
                    .Build();
            }
            catch
            {
                _configuration = new ConfigurationBuilder().Build();
            }
            _appSettings = BindAppSettings(_configuration);

            if (!File.Exists(filePath))
            {
                try
                {
                    var json = JsonSerializer.Serialize(_appSettings, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(filePath, json);
                }
                catch { }
            }

            SubscribeToChangeToken();
        }

        private static AppSettings BindAppSettings(IConfiguration config)
        {
            const string section = AppSettings.SectionName;
            return new AppSettings
            {
                Environment = config[$"{section}:Environment"] ?? "Production",
                LogLevel = config[$"{section}:LogLevel"] ?? "Info",
                Culture = config[$"{section}:Culture"] ?? "zh-CN",
                Database = new DatabaseSettings
                {
                    ConnectionStringName = config[$"{section}:Database:ConnectionStringName"] ?? "Default"
                },
                Camera = new CameraSettings
                {
                    Interface = config[$"{section}:Camera:Interface"] ?? "GigEV2",
                    SerialNumber = config[$"{section}:Camera:SerialNumber"] ?? "",
                    ExposureMs = double.TryParse(config[$"{section}:Camera:ExposureMs"], out var exp) ? exp : 8.0,
                    Gain = double.TryParse(config[$"{section}:Camera:Gain"], out var gain) ? gain : 1.0,
                    FrameRate = int.TryParse(config[$"{section}:Camera:FrameRate"], out var fr) ? fr : 30
                },
                Motion = new MotionSettings
                {
                    ControllerIp = config[$"{section}:Motion:ControllerIp"] ?? "192.168.1.10",
                    ControllerPort = int.TryParse(config[$"{section}:Motion:ControllerPort"], out var cp) ? cp : 8000,
                    BeltSpeedMmPerSec = double.TryParse(config[$"{section}:Motion:BeltSpeedMmPerSec"], out var bs) ? bs : 200.0,
                    CameraToRejectMm = double.TryParse(config[$"{section}:Motion:CameraToRejectMm"], out var cr) ? cr : 350.0,
                    RejectDurationMs = double.TryParse(config[$"{section}:Motion:RejectDurationMs"], out var rd) ? rd : 40.0,
                    Vstop = double.TryParse(config[$"{section}:Motion:Vstop"], out var vs) ? vs : 50.0,
                    Vmove = double.TryParse(config[$"{section}:Motion:Vmove"], out var vm) ? vm : 200.0
                },
                Io = new IoSettings
                {
                    TriggerInputBit = int.TryParse(config[$"{section}:Io:TriggerInputBit"], out var tib) ? tib : 0,
                    RejectOutputBit = int.TryParse(config[$"{section}:Io:RejectOutputBit"], out var rob) ? rob : 1,
                    ReadyOutputBit = int.TryParse(config[$"{section}:Io:ReadyOutputBit"], out var roob) ? roob : 2,
                    AlarmOutputBit = int.TryParse(config[$"{section}:Io:AlarmOutputBit"], out var aob) ? aob : 3
                },
                Vision = new VisionSettings
                {
                    HalconRuntimePath = config[$"{section}:Vision:HalconRuntimePath"] ?? @"E:\ProgramFiles\MVTec\HALCON-24.11-Progress-Steady",
                    ModelDirectory = config[$"{section}:Vision:ModelDirectory"] ?? "Models",
                    DefaultModelName = config[$"{section}:Vision:DefaultModelName"] ?? "ic_shape.hdef",
                    MinScore = double.TryParse(config[$"{section}:Vision:MinScore"], out var ms) ? ms : 0.8,
                    NumMatches = int.TryParse(config[$"{section}:Vision:NumMatches"], out var nm) ? nm : 1,
                    TimeoutMs = int.TryParse(config[$"{section}:Vision:TimeoutMs"], out var to) ? to : 200
                }
            };
        }

        private void SubscribeToChangeToken()
        {
            _changeToken = _configuration.GetReloadToken().RegisterChangeCallback(_ =>
            {
                Changed?.Invoke(this, new ConfigChangedEventArgs("App"));
            }, null);
        }

        public AppSettings Current => _appSettings;
        public DatabaseSettings Database => Current.Database;
        public CameraSettings Camera => Current.Camera;
        public MotionSettings Motion => Current.Motion;
        public IoSettings Io => Current.Io;
        public VisionSettings Vision => Current.Vision;

        public string GetConnectionString() => GetConnectionString(Database.ConnectionStringName);

        public string GetConnectionString(string name)
        {
            var raw = _configuration.GetConnectionString(name)
                      ?? throw new InvalidOperationException($"未找到连接串 '{name}'，请检查 appsettings.json 的 ConnectionStrings 节。");
            return Unprotect(raw);
        }

        /// <summary>
        /// H2：剔除延时 = 相机到剔除口走行时间 + 半个剔除动作补偿。
        /// 速度越低延时越大；速度变化时必须重算（对应 H3 实时生效要求）。
        /// </summary>
        public double ComputeRejectDelay(double beltSpeedMmPerSec)
        {
            if (beltSpeedMmPerSec <= 0)
                throw new ArgumentOutOfRangeException(nameof(beltSpeedMmPerSec), "线速度必须大于 0。");
            return Motion.CameraToRejectMm / beltSpeedMmPerSec * 1000.0 + Motion.RejectDurationMs * 0.5;
        }

        public double ComputeRejectDelay() => ComputeRejectDelay(Motion.BeltSpeedMmPerSec);

        public void Save() => Save(Current);

        public void Save(AppSettings settings)
        {
            if (settings is null) throw new ArgumentNullException(nameof(settings));
            lock (_sync)
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
        }

        // D1：连接串以 "enc:" 前缀表示 DPAPI 密文，否则当作明文（首次运行兼容）。
        private static string Unprotect(string value)
        {
            const string prefix = "enc:";
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return value;
            var cipher = Convert.FromBase64String(value[prefix.Length..]);
            var plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }

        /// <summary>迁移工具：把明文连接串加密为可写入 appsettings.json 的密文（D1 整改）。</summary>
        public static string ProtectConnectionString(string plain)
        {
            var cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return "enc:" + Convert.ToBase64String(cipher);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _changeToken?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}