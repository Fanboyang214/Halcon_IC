using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;


namespace Core.Models
{
    /// <summary>
    /// 模板配置数据模型
    /// 保存模板区域和两个检测区域的坐标参数
    /// 序列化为JSON文件存储在Templates目录下
    /// </summary>
    public class TemplateConfig
    {
        /// <summary>模板名称，保存时由用户输入，用于在列表中显示</summary>
        public string TemplateName { get; set; } = "默认模板";

        /// <summary>模板矩形区域左上角行坐标</summary>
        public double TemplateRow1 { get; set; }
        /// <summary>模板矩形区域左上角列坐标</summary>
        public double TemplateColumn1 { get; set; }
        /// <summary>模板矩形区域右下角行坐标</summary>
        public double TemplateRow2 { get; set; }
        /// <summary>模板矩形区域右下角列坐标</summary>
        public double TemplateColumn2 { get; set; }

        /// <summary>检测区域1中心行坐标</summary>
        public double CheckRect1Row { get; set; }
        /// <summary>检测区域1中心列坐标</summary>
        public double CheckRect1Column { get; set; }
        /// <summary>检测区域1旋转角度（弧度）</summary>
        public double CheckRect1Phi { get; set; }
        /// <summary>检测区域1半长轴长度</summary>
        public double CheckRect1Length1 { get; set; }
        /// <summary>检测区域1半短轴长度</summary>
        public double CheckRect1Length2 { get; set; }

        /// <summary>检测区域2中心行坐标</summary>
        public double CheckRect2Row { get; set; }
        /// <summary>检测区域2中心列坐标</summary>
        public double CheckRect2Column { get; set; }
        /// <summary>检测区域2旋转角度（弧度）</summary>
        public double CheckRect2Phi { get; set; }
        /// <summary>检测区域2半长轴长度</summary>
        public double CheckRect2Length1 { get; set; }
        /// <summary>检测区域2半短轴长度</summary>
        public double CheckRect2Length2 { get; set; }

        /// <summary>将配置对象序列化为JSON字符串</summary>
        public string ToJson()
        {
            string jsonString = JsonSerializer.Serialize(this);
            return  jsonString;

        }

        /// <summary>从JSON字符串反序列化为配置对象</summary>
        public static TemplateConfig FromJson(string json)
        {
            TemplateConfig  config = JsonSerializer.Deserialize<TemplateConfig>(json);
            return config;
        }
    }
}
