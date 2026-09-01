using Core.Interfaces;
using Infrastruce.Services;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Unity;
using System.Windows;
using Data;
using Vision;
using Motion;
using ICApp.Views;
using ICApp.ViewModels;

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
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<ILogService, LogService>();
            containerRegistry.RegisterSingleton<IConfigService, ConfigService>();

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