using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class VisionStatus
    {
        public int CameraStatus { get; set; } = 1;

        public int TemplateStatus { get; set; } = 1;

        public int DetectionStatus { get; set; } = 1;


    }
}
