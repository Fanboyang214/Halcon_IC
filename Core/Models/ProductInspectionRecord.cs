using System;

namespace Core.Models
{
    /// <summary>
    /// 单芯片检测记录实体（对应数据库 InspectionRecords 表的一行）。
    /// 每完成一次检测写入一条，用于按分钟聚合、分页明细查询与汇总报表（治理 D3）。
    /// </summary>
    /// <remarks>
    /// 重构说明：
    ///  原字段 TotalInspectedCount/QualifiedCount/DefectiveCount 属于"批次汇总"语义，
    ///  与"单次检测记录"混在一起导致语义混乱，已移除；
    ///  新增 MatchScore/PinCount/PinCount2/IsError/ErrorMessage 用于支撑算法级报表。
    /// </remarks>
    public class ProductInspectionRecord
    {
        /// <summary>自增主键。</summary>
        public int RecordId { get; set; }

        /// <summary>检测时间（本地时区）。</summary>
        public DateTime InspectionTime { get; set; }

        /// <summary>产品型号（对应模板名称 TemplateName）。</summary>
        public string ProductModel { get; set; } = string.Empty;

        /// <summary>批次号（可选，用于按批次汇总）。</summary>
        public string? BatchNo { get; set; }

        /// <summary>模板匹配得分（0~100）。</summary>
        public int MatchScore { get; set; }

        /// <summary>检测区域1针脚数。</summary>
        public int PinCount { get; set; }

        /// <summary>检测区域2针脚数。</summary>
        public int PinCount2 { get; set; }

        /// <summary>是否合格。</summary>
        public bool IsOk { get; set; }

        /// <summary>结果文本：OK / NG / No found / 检测出错。</summary>
        public string ResultText { get; set; } = string.Empty;

        /// <summary>是否检测异常（与 IsOk 区分，异常单独统计）。</summary>
        public bool IsError { get; set; }

        /// <summary>异常错误消息（仅 IsError=true 时有值）。</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>缺陷原因（NG 时填写，可选）。</summary>
        public string? DefectReasons { get; set; }

        /// <summary>操作员 ID（可选，登录后传入）。</summary>
        public int? OperatorId { get; set; }

        /// <summary>设备编号。</summary>
        public string? DeviceNo { get; set; }

        /// <summary>备注。</summary>
        public string? Remark { get; set; }
    }
}
