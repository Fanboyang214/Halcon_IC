using Core.Events;
using Core.Interfaces;
using Infrastruce.Services;
using Motion.Services;
using Motion.ViewModels;
using Motion.Views;
using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation;
using System;

namespace Motion
{
    public class MotionModule : IModule, IDestructible
    {
        private IEventAggregator _eventAggregator;
        private SubscriptionToken _shutdownToken;
        private IMotionControlService _motion;

        public void OnInitialized(IContainerProvider containerProvider)
        {
            _eventAggregator = containerProvider.Resolve<IEventAggregator>();
            _motion = containerProvider.Resolve<IMotionControlService>();

            _shutdownToken = _eventAggregator.GetEvent<AppShutdownEvent>()
                .Subscribe(OnShutdown, ThreadOption.UIThread, keepSubscriberReferenceAlive: true);
        }

        private void OnShutdown()
        {
            _motion?.Dispose();
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<IMotionControlService, MotionControlService>();
            containerRegistry.RegisterSingleton<ISensorService, SensorService>();
            containerRegistry.RegisterSingleton<ISolenoidValueService, SolenoidValueService>();
            containerRegistry.RegisterSingleton<ILogService, LogService>();
            containerRegistry.RegisterForNavigation<MotionControlView, MotionControlViewModel>();
        }

        public void Destroy()
        {
        }
    }
}