using Core.Interfaces;
using Infrastruce.Services;
using Motion.Services;
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
            containerRegistry.RegisterSingleton<IMotionControlService, MotionControlService>();
            containerRegistry.RegisterSingleton<ILogService,LogService>();
            containerRegistry.RegisterForNavigation<MotionControlView, MotionControlViewModel>();
        }
    }
}
