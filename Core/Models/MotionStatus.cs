using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class MotionStatus
    {

        public int ConnectStatus
        {
            get; set;
        } = 1;

        public int SevonStatus
        {
            get; set;
        } = 1;

        public int MoveStatus
        {

            get; set;
        } = 1;

        public string Speed
        {
            get; set;
        } = "3000";



    }
}
