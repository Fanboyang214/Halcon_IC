using Core.Interfaces;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Vision.Services;
using Vision.ViewModels;
using Vision.ViewModels.Dialog;
using Vision.Views;
using Vision.Views.Dialog;

namespace Vision
{
    public class VisionModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            var isDirectShow = IsDirectShowConfigured();

            if (isDirectShow)
            {
                containerRegistry.RegisterSingleton<ICameraService, DirectShowCameraService>();
            }
            else
            {
                containerRegistry.RegisterSingleton<ICameraService, CameraService>();
            }

            containerRegistry.RegisterSingleton<ITemplateService, TemplateService>();
            containerRegistry.RegisterSingleton<IDetectionService, DetectionService>();

            containerRegistry.RegisterDialog<TemplateNameDialogView, TemplateNameDialogViewModel>();
            containerRegistry.RegisterDialog<FileListDialogView,FileListDialogViewModel>();

            containerRegistry.RegisterForNavigation<VisionWindow, VisionWindowViewModel>();
            containerRegistry.RegisterForNavigation<StatisticView, StatisticViewModel>();
            containerRegistry.RegisterForNavigation<MotionControlView, MotionControlViewModel>();
            containerRegistry.RegisterForNavigation<TemplatePanelView, TemplatePanelViewModel>();
            containerRegistry.RegisterForNavigation<DetectionModuleView, DetectionModuleViewModel>();
            containerRegistry.RegisterForNavigation<SystemLogView, SystemLogViewModel>();
            containerRegistry.RegisterForNavigation<DetectionRecordView, DetectionRecordViewModel>();
            containerRegistry.RegisterForNavigation<DeviceStatusView, DeviceStatusViewModel>();
        }

        private static bool IsDirectShowConfigured()
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(configPath)) return false;

            try
            {
                var text = File.ReadAllText(configPath);
                var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip };
                using var doc = JsonDocument.Parse(text, options);
                var root = doc.RootElement;
                if (root.TryGetProperty("App", out var app) &&
                    app.TryGetProperty("Camera", out var camera) &&
                    camera.TryGetProperty("Interface", out var iface))
                {
                    return string.Equals(iface.GetString(), "MediaFoundation", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
            }

            return false;
        }
    }
}