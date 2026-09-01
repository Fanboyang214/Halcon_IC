using Motion.ViewModels;
using Motion.Views;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Motion
{
    public class MotionModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
          
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {

            containerRegistry.RegisterForNavigation<MotionControlView,MotionControlViewModel>();
        }
    }
}
