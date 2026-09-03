using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    /// <summary>
    /// 检测配置数据模型
    /// 存储当前检测任务的全局配置参数
    /// </summary>
    public class InspectionConfig
    {
        /// <summary>当前检测的芯片型号，保存检测记录时写入ProductModel字段</summary>
        public string CurrentChipModel { get; set; } = "LM386";

        /// <summary>默认缺陷原因，芯片不合格时自动填入DefectReasons字段</summary>
        public string DefaultDefectReason { get; set; } = "针脚缺失";

        /// <summary>当前生产批次号，格式：BATCH+日期</summary>
        public string CurrentBatchNo { get; set; } = $"BATCH{DateTime.Now:yyyyMMdd}";

        /// <summary>设备编号，标识哪台设备执行的检测</summary>
        public string DeviceNo { get; set; } = "奶龙包";

        /// <summary>检测区域1针脚数合格下限，默认4</summary>
        public int PinCountMin { get; set; } = 4;

        /// <summary>检测区域1针脚数合格上限，默认4</summary>
        public int PinCountMax { get; set; } = 4;

        /// <summary>检测区域2针脚数合格下限，默认4</summary>
        public int PinCount2Min { get; set; } = 4;

        /// <summary>检测区域2针脚数合格上限，默认4</summary>
        public int PinCount2Max { get; set; } = 4;

        /// <summary>上升沿→下降沿自动复位超时(ms)，同一产品在此时间内不重复检测</summary>
        public int FallingEdgeTimeoutMs { get; set; } = 2500;
    }
}
