using Prism.Ioc;
using Prism.Regions;
using System;
using System.Windows;
using System.Windows.Threading;

namespace ICApp
{
    public partial class MainWindow : Window
    {
        private readonly IRegionManager _regionManager;
        private readonly DispatcherTimer _clockTimer;

        public MainWindow(IRegionManager regionManager)
        {
            InitializeComponent();
            _regionManager = regionManager;

            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += OnClockTick;
            _clockTimer.Start();

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _regionManager.RequestNavigate("VisionRegion", "VisionWindow");
            _regionManager.RequestNavigate("StatisticRegion", "StatisticView");
            _regionManager.RequestNavigate("MotionControlRegion", "MotionControlView");
            _regionManager.RequestNavigate("TemplatePanelRegion", "TemplatePanelView");
            _regionManager.RequestNavigate("DetectionModuleRegion", "DetectionModuleView");
            _regionManager.RequestNavigate("SystemLogRegion", "SystemLogView");
            _regionManager.RequestNavigate("DetectionRecordRegion", "DetectionRecordView");
            _regionManager.RequestNavigate("DeviceStatusRegion", "DeviceStatusView");
        }

        private void OnClockTick(object? sender, EventArgs e)
        {
            ClockText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
