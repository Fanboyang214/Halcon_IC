using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    /// <summary>配置变更事件参数。订阅方据此刷新硬件参数（如 H3：速度变更后重算剔除延时）。</summary>
    public sealed class ConfigChangedEventArgs : EventArgs
    {
        public string Section { get; }
        public ConfigChangedEventArgs(string section) => Section = section;
    }
}
