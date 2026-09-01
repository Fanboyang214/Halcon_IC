using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    /// <summary>
    /// 视觉检测结果数据模型
    /// 封装一次完整检测的所有输出数据，包括匹配得分、合格判定、针脚计数、触发信号等
    /// 由DetectionService.Process()方法返回，供ViewModel使用
    /// </summary>
    public class DetectionResult
    {
        public DateTime Time
        {
            get; set;
        }

        /// <summary>
        /// 模板匹配得分（0-100）
        /// 分数越高表示匹配越精确，阈值0.6对应约60分
        /// </summary>
        public int MatchScore { get; set; }

        /// <summary>
        /// 芯片是否合格
        /// true=合格（两个检测区域各检测到4个针脚），false=不合格
        /// </summary>
        public bool IsOK { get; set; }

        /// <summary>
        /// 检测结果文本
        /// "OK"=合格, "NG"=不合格, "No found"=未匹配到芯片, "检测出错"=异常
        /// </summary>
        public string ResultText { get; set; } = "No found";

        /// <summary>
        /// 检测区域1的针脚数量
        /// 合格标准为4个
        /// </summary>
        public int PinCount { get; set; }

        /// <summary>
        /// 检测区域2的针脚数量
        /// 合格标准为4个
        /// </summary>
        public int PinCount2 { get; set; }

        /// <summary>
        /// 检测结果叠加显示图像
        /// 在原始灰度图上叠加了检测区域和匹配轮廓，用于界面显示
        /// </summary>
        public HObject DisplayImage { get; set; }

        /// <summary>
        /// 模板轮廓在匹配位姿下的 XLD 轮廓
        /// 用于在窗口上叠加显示匹配位置的芯片轮廓
        /// 所有权归调用方，调用方负责 Dispose
        /// </summary>
        public HObject ModelContours { get; set; }

        /// <summary>
        /// 检测区域 1（旋转矩形）
        /// 用于在窗口上叠加显示左侧针脚检测区域
        /// 所有权归调用方，调用方负责 Dispose
        /// </summary>
        public HObject DetectionRegion1 { get; set; }

        /// <summary>
        /// 检测区域 2（旋转矩形）
        /// 用于在窗口上叠加显示右侧针脚检测区域
        /// 所有权归调用方，调用方负责 Dispose
        /// </summary>
        public HObject DetectionRegion2 { get; set; }

        /// <summary>
        /// 检测过程是否发生异常
        /// true=检测过程中出现错误，需查看ErrorMessage
        /// </summary>
        public bool IsError { get; set; }

        /// <summary>
        /// 异常错误消息
        /// 仅当IsError=true时有值
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 是否为上升沿信号
        /// 当模板首次匹配到芯片时触发，表示新芯片进入检测区域
        /// 用于触发分拣逻辑的开始
        /// </summary>
        public bool IsRisingEdge { get; set; }

        /// <summary>
        /// 是否为下降沿信号
        /// 上升沿触发2秒后自动触发，表示当前芯片检测周期结束
        /// 用于重置触发状态，准备检测下一个芯片
        /// </summary>
        public bool IsFallingEdge { get; set; }

        /// <summary>
        /// 是否应该触发分拣动作
        /// 当检测到芯片（上升沿）且检测结果有效时为true
        /// ViewModel根据此标志和IsOK决定是否触发电磁阀剔除不合格品
        /// </summary>
        public bool ShouldTrigger { get; set; }
    }
}
