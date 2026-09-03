using Core.Events;
using Core.Interfaces;
using Core.Models;
using Data;
using ICApp.ViewModels;
using ICApp.Views;
using Infrastruce.Services;
using Motion;
using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Unity;
using System.Windows;
using Vision;

namespace ICApp
{
    public partial class App : PrismApplication
    {
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<DataModule>();
            moduleCatalog.AddModule<VisionModule>();
            moduleCatalog.AddModule<MotionModule>();
        }

        protected override Window CreateShell()
        {
            var shell = Container.Resolve<MainWindow>();
            shell.Closing += Shell_Closing;
            return shell;
        }

        private void Shell_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var eventAggregator = Container.Resolve<IEventAggregator>();
            // 广播退出通知，所有模块执行关闭逻辑
            eventAggregator.GetEvent<AppShutdownEvent>().Publish();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<ILogService, LogService>();
            containerRegistry.RegisterSingleton<IConfigService, ConfigService>();
            containerRegistry.RegisterSingleton<IPollTaskFactory, PollTaskFactory>();


            containerRegistry.RegisterForNavigation<DeviceStatusView, DeviceStatusViewModel>();

        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configService = Container.Resolve<IConfigService>();
            var halconBinPath = configService.Current.Vision.HalconRuntimePath;
            HalconRuntime.Initialize(halconBinPath);
        }
    }
}