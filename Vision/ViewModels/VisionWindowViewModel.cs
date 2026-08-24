using Core.Events;
using Core.Interfaces;
using Core.Models;
using HalconDotNet;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.IO;

namespace Vision.ViewModels
{
    /// <summary>
    /// VisionWindow 的 ViewModel。
    /// 编排相机采集→模板创建/加载→检测→结果显示。
    /// 使用 HSmartWindowControlWPF + HWindow.DispObj 显示。
    /// </summary>
    public class VisionWindowViewModel : BindableBase
    {
        private readonly ICameraService _camera;
        private readonly ITemplateService _template;
        private readonly IDetectionService _detection;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogService _logger;
        private readonly IConfigService _config;

        // ROI 框选结果
        private double _roiRow1, _roiCol1, _roiRow2, _roiCol2;
        private bool _hasRoi;
        private bool _isRoiDrawing;

        // 绑定属性
        private string _statusText = "就绪";
        private string _resultText = "";
        private int _matchScore;
        private int _pinCount;
        private int _pinCount2;
        private bool _isDetecting;
        private bool _isCameraOpen;
        private bool _isTemplateCreated;

        // 当前帧缓存
        private HObject? _lastFrame;
        private readonly object _frameLock = new();

        // 检测配置
        private readonly InspectionConfig _inspectionConfig = new();

        // 订阅令牌
        private Prism.Events.SubscriptionToken? _grabSubToken;

        /// <summary>
        /// Halcon 窗口引用（由 VisionWindow.xaml.cs 在 Loaded 时注入）。
        /// </summary>
        public HWindow? HalconWindow { get; set; }

        #region 绑定属性

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string ResultText
        {
            get => _resultText;
            set => SetProperty(ref _resultText, value);
        }

        public int MatchScore
        {
            get => _matchScore;
            set => SetProperty(ref _matchScore, value);
        }

        public int PinCount
        {
            get => _pinCount;
            set => SetProperty(ref _pinCount, value);
        }

        public int PinCount2
        {
            get => _pinCount2;
            set => SetProperty(ref _pinCount2, value);
        }

        public bool IsDetecting
        {
            get => _isDetecting;
            set
            {
                SetProperty(ref _isDetecting, value);
                RefreshCommandStates();
            }
        }

        public bool IsCameraOpen
        {
            get => _isCameraOpen;
            set
            {
                SetProperty(ref _isCameraOpen, value);
                RefreshCommandStates();
            }
        }

        public bool IsTemplateCreated
        {
            get => _isTemplateCreated;
            set
            {
                SetProperty(ref _isTemplateCreated, value);
                RefreshCommandStates();
            }
        }

        #endregion

        #region 命令

        public DelegateCommand OpenCameraCmd { get; }
        public DelegateCommand CloseCameraCmd { get; }
        public DelegateCommand StartGrabCmd { get; }
        public DelegateCommand StopGrabCmd { get; }
        public DelegateCommand DrawRoiCmd { get; }
        public DelegateCommand CreateTemplateCmd { get; }
        public DelegateCommand LoadTemplateCmd { get; }
        public DelegateCommand SaveTemplateCmd { get; }
        public DelegateCommand LoadReferenceImageCmd { get; }
        public DelegateCommand StartDetectCmd { get; }
        public DelegateCommand StopDetectCmd { get; }

        /// <summary>
        /// 触发 ROI 绘制（由 View 订阅，使用 HDrawingObject 创建交互绘图对象）。
        /// </summary>
        public event Action? RequestDrawRoi;

        /// <summary>
        /// 请求清除 ROI 绘图对象（由 View 订阅，分离 HDrawingObject 并恢复显示）。
        /// </summary>
        public event Action? RequestClearRoi;

        #endregion

        public VisionWindowViewModel(
            ICameraService camera,
            ITemplateService template,
            IDetectionService detection,
            IEventAggregator eventAggregator,
            ILogService logger,
            IConfigService config)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _template = template ?? throw new ArgumentNullException(nameof(template));
            _detection = detection ?? throw new ArgumentNullException(nameof(detection));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

<<<<<<< Updated upstream
            OpenCameraCmd = new DelegateCommand(ExecuteOpenCamera, () => !IsCameraOpen);
            CloseCameraCmd = new DelegateCommand(ExecuteCloseCamera, () => IsCameraOpen);
            StartGrabCmd = new DelegateCommand(ExecuteStartGrab, () => IsCameraOpen && !IsDetecting);
            StopGrabCmd = new DelegateCommand(ExecuteStopGrab, () => IsCameraOpen && !IsDetecting);
=======
            OpenCameraCmd = new DelegateCommand(async()=>await ExecuteOpenCamera(), () => !IsCameraOpen);
            CloseCameraCmd = new DelegateCommand( async() =>await  ExecuteCloseCamera(), () => IsCameraOpen);
            StartGrabCmd = new DelegateCommand(async() => await ExecuteStartGrab(), () => IsCameraOpen && !IsDetecting);
<<<<<<< HEAD
            StopGrabCmd = new DelegateCommand(async() =>await ExecuteStopGrab(), () => IsCameraOpen && !IsDetecting);
=======
            StopGrabCmd = new DelegateCommand(async() => ExecuteStopGrab(), () => IsCameraOpen && !IsDetecting);
>>>>>>> 60f0d7d9a62666162f30d5ddd48345f47e8ff02d
>>>>>>> Stashed changes
            DrawRoiCmd = new DelegateCommand(ExecuteDrawRoi, () => !IsDetecting);
            CreateTemplateCmd = new DelegateCommand(ExecuteCreateTemplate, () => IsCameraOpen && !IsDetecting);
            LoadTemplateCmd = new DelegateCommand(ExecuteLoadTemplate, () => !IsDetecting);
            SaveTemplateCmd = new DelegateCommand(ExecuteSaveTemplate, () => IsTemplateCreated);
            LoadReferenceImageCmd = new DelegateCommand(ExecuteLoadReferenceImage, () => !IsDetecting);
            StartDetectCmd = new DelegateCommand(ExecuteStartDetect, () => IsTemplateCreated && !IsDetecting);
            StopDetectCmd = new DelegateCommand(ExecuteStopDetect, () => IsDetecting);
        }

        /// <summary>
        /// 由 VisionWindow.xaml.cs 鼠标框选后调用。
        /// row/col 为 Halcon 像素坐标。
        /// </summary>
        public void SetRoi(double row1, double col1, double row2, double col2)
        {
            _roiRow1 = row1; _roiCol1 = col1;
            _roiRow2 = row2; _roiCol2 = col2;
            _hasRoi = true;
            StatusText = $"ROI: ({row1:F0},{col1:F0})-({row2:F0},{col2:F0})";
            RefreshCommandStates();
        }

        /// <summary>
        /// 触发 View 创建 HDrawingObject 进行交互式 ROI 绘制。
        /// </summary>
        private void ExecuteDrawRoi()
        {
            if (IsDetecting) return;
            StatusText = "请在图像窗口中绘制/调整 ROI（HALCON 交互对象）";
            RequestDrawRoi?.Invoke();
        }

        public void SetRoiDrawing(bool isDrawing)
        {
            _isRoiDrawing = isDrawing;
            if (!isDrawing)
            {
                lock (_frameLock)
                {
                    if (_lastFrame != null && HalconWindow != null)
                    {
                        HalconWindow.SetPart(0, 0, -1, -1);
                        HalconWindow.DispObj(_lastFrame);
                    }
                }
            }
        }

        public bool TryGetLastFrame(out HObject? frame)
        {
            lock (_frameLock)
            {
                frame = _lastFrame;
                return _lastFrame != null;
            }
        }

        /// <summary>
        /// 显示一帧图像到 Halcon 窗口。
        /// </summary>
        public void ShowImage(HObject image)
        {
            if (HalconWindow == null || image == null) return;
            if (_isRoiDrawing) return;
            try
            {
                HalconWindow.SetPart(0, 0, -1, -1);
                HalconWindow.DispObj(image);
            }
            catch (Exception ex)
            {
                _logger.AddLog("Error", $"显示图像异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 在 Halcon 窗口叠加显示轮廓/区域。
        /// </summary>
        public void ShowOverlay(HObject overlay)
        {
            if (HalconWindow == null || overlay == null) return;
            try
            {
                HalconWindow.SetColor("green");
                HalconWindow.DispObj(overlay);
            }
            catch { }
        }

        /// <summary>
        /// 重新显示最后一帧（用于 ROI 清除后恢复正常显示）。
        /// </summary>
        private void RedisplayLastFrame()
        {
            if (HalconWindow == null) return;
            lock (_frameLock)
            {
                if (_lastFrame != null)
                {
                    try
                    {
                        HalconWindow.SetPart(0, 0, -1, -1);
                        HalconWindow.DispObj(_lastFrame);
                    }
                    catch { }
                }
            }
        }

        #region 命令实现

        private void ExecuteOpenCamera()
        {
            try
            {
                var camSettings = _config.Camera;
                _camera.Open(camSettings);
                IsCameraOpen = true;
                StatusText = $"相机已连接 SN={camSettings.SerialNumber}";

                _grabSubToken?.Dispose();
                _grabSubToken = _eventAggregator.GetEvent<ImageGrabbedEvent>()
                    .Subscribe(OnImageGrabbed, ThreadOption.UIThread);
            }
            catch (Exception ex)
            {
                StatusText = $"相机打开失败: {ex.Message}";
                _logger.AddLog("Error", $"相机打开失败: {ex.Message}");
            }
        }

        private void ExecuteCloseCamera()
        {
            try
            {
                if (IsDetecting) ExecuteStopDetect();
                _camera.Close();
                IsCameraOpen = false;
                StatusText = "相机关闭";
            }
            catch (Exception ex)
            {
                StatusText = $"相机关闭异常: {ex.Message}";
            }
        }

        private void ExecuteStartGrab()
        {
            try
            {
                _camera.StartGrabbing();
                StatusText = "采集中...";
            }
            catch (Exception ex)
            {
                StatusText = $"启动采集失败: {ex.Message}";
            }
        }

        private void ExecuteStopGrab()
        {
            try
            {
                _camera.StopGrabbing();
                StatusText = "采集停止";
            }
            catch (Exception ex)
            {
                StatusText = $"停止采集异常: {ex.Message}";
            }
        }

        private void ExecuteCreateTemplate()
        {
            HObject? frame;
            lock (_frameLock) { frame = _lastFrame?.Clone(); }
            if (frame == null)
            {
                StatusText = "无可用帧，请先采集或加载参考图";
                return;
            }
            if (!_hasRoi)
            {
                StatusText = "请先在图像上框选 ROI";
                frame.Dispose();
                return;
            }

            try
            {
                _template.CreateTemplate(frame, _roiRow1, _roiCol1, _roiRow2, _roiCol2);
                IsTemplateCreated = _template.IsTemplateCreated;
                StatusText = $"模板创建成功 {_template.TemplateName}";
                RefreshCommandStates();

                // 清除 ROI 绘图对象，恢复正常采集显示
                _isRoiDrawing = false;
                RequestClearRoi?.Invoke();
                RedisplayLastFrame();

                if (_template.ModelContours != null)
                    ShowOverlay(_template.ModelContours);
            }
            catch (Exception ex)
            {
                StatusText = $"模板创建失败: {ex.Message}";
                _logger.AddLog("Error", $"模板创建失败: {ex.Message}");
            }
            finally
            {
                frame.Dispose();
            }
        }

        private void ExecuteLoadTemplate()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "模板配置 (*.json)|*.json",
                InitialDirectory = GetModelDirectory()
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                string json = File.ReadAllText(dlg.FileName);
                var cfg = TemplateConfig.FromJson(json);

                string refImagePath = Path.ChangeExtension(dlg.FileName, ".png");
                if (!File.Exists(refImagePath))
                    refImagePath = Path.Combine(Path.GetDirectoryName(dlg.FileName)!, "ref.png");

                if (!File.Exists(refImagePath))
                {
                    StatusText = "找不到参考图，请用\"加载参考图\"手动选择";
                    return;
                }

                var refImage = new HObject();
                HOperatorSet.ReadImage(out refImage, refImagePath);

                _template.LoadTemplate(cfg, refImage);
                IsTemplateCreated = _template.IsTemplateCreated;
                StatusText = $"模板已加载: {cfg.TemplateName}";
                RefreshCommandStates();

                ShowImage(refImage);
                if (_template.ModelContours != null)
                    ShowOverlay(_template.ModelContours);
            }
            catch (Exception ex)
            {
                StatusText = $"加载模板失败: {ex.Message}";
                _logger.AddLog("Error", $"加载模板失败: {ex.Message}");
            }
        }

        private void ExecuteSaveTemplate()
        {
            if (!IsTemplateCreated) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "模板配置 (*.json)|*.json",
                InitialDirectory = GetModelDirectory(),
                FileName = $"{_template.TemplateName}.json"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var cfg = _template.ExportConfig();
                Directory.CreateDirectory(GetModelDirectory());
                File.WriteAllText(dlg.FileName, cfg.ToJson());
                StatusText = $"模板已保存: {dlg.FileName}";

                // 保存后清除 ROI 绘图对象，恢复正常采集显示
                _isRoiDrawing = false;
                RequestClearRoi?.Invoke();
                RedisplayLastFrame();
            }
            catch (Exception ex)
            {
                StatusText = $"保存模板失败: {ex.Message}";
            }
        }

        private void ExecuteLoadReferenceImage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "图像文件 (*.png;*.bmp;*.jpg)|*.png;*.bmp;*.jpg"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var img = new HObject();
                HOperatorSet.ReadImage(out img, dlg.FileName);
                ShowImage(img);

                lock (_frameLock)
                {
                    _lastFrame?.Dispose();
                    _lastFrame = img.Clone();
                }
                StatusText = $"参考图已加载: {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                StatusText = $"加载参考图失败: {ex.Message}";
            }
        }

        private void ExecuteStartDetect()
        {
            try
            {
                _detection.Start(_template, _inspectionConfig);

                if (!_camera.IsGrabbing)
                    _camera.StartGrabbing();

                IsDetecting = true;
                StatusText = "检测中...";
            }
            catch (Exception ex)
            {
                StatusText = $"启动检测失败: {ex.Message}";
                _logger.AddLog("Error", $"启动检测失败: {ex.Message}");
            }
        }

        private void ExecuteStopDetect()
        {
            try
            {
                _detection.Stop(3000);
                IsDetecting = false;
                StatusText = "检测停止";
            }
            catch (Exception ex)
            {
                StatusText = $"停止检测异常: {ex.Message}";
            }
        }

        #endregion

        /// <summary>
        /// ImageGrabbedEvent 回调（已调度到 UI 线程）。
        /// </summary>
        private void OnImageGrabbed(ImageGrabbedPayload payload)
        {
            try
            {
                ShowImage(payload.Image);

                lock (_frameLock)
                {
                    _lastFrame?.Dispose();
                    _lastFrame = payload.Image.Clone();
                }

                if (IsDetecting)
                {
                    bool queued = _detection.EnqueueFrame(payload.Image);
                    if (!queued)
                    {
                        var result = _detection.Process(_template, _inspectionConfig, payload.Image);
                        OnDetectionResult(result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.AddLog("Error", $"OnImageGrabbed 异常: {ex.Message}");
            }
            finally
            {
                payload.Dispose();
            }
        }

        private void OnDetectionResult(DetectionResult result)
        {
            ResultText = result.ResultText;
            MatchScore = result.MatchScore;
            PinCount = result.PinCount;
            PinCount2 = result.PinCount2;

            if (HalconWindow == null) return;

            if (result.DisplayImage != null)
            {
                HalconWindow.SetPart(0, 0, -1, -1);
                HalconWindow.DispObj(result.DisplayImage);
                result.DisplayImage.Dispose();
            }

            if (result.ModelContours != null)
            {
                try
                {
                    HalconWindow.SetColor("green");
                    HalconWindow.DispObj(result.ModelContours);
                }
                finally
                {
                    result.ModelContours.Dispose();
                }
            }

            if (result.DetectionRegion1 != null)
            {
                try
                {
                    HalconWindow.SetColor(result.IsOK ? "cyan" : "red");
                    HalconWindow.DispObj(result.DetectionRegion1);
                }
                finally
                {
                    result.DetectionRegion1.Dispose();
                }
            }

            if (result.DetectionRegion2 != null)
            {
                try
                {
                    HalconWindow.SetColor(result.IsOK ? "cyan" : "red");
                    HalconWindow.DispObj(result.DetectionRegion2);
                }
                finally
                {
                    result.DetectionRegion2.Dispose();
                }
            }
        }

        private string GetModelDirectory()
        {
            string dir = _config.Vision?.ModelDirectory ?? "Models";
            if (!Path.IsPathRooted(dir))
                dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dir);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private void RefreshCommandStates()
        {
            OpenCameraCmd.RaiseCanExecuteChanged();
            CloseCameraCmd.RaiseCanExecuteChanged();
            StartGrabCmd.RaiseCanExecuteChanged();
            StopGrabCmd.RaiseCanExecuteChanged();
            DrawRoiCmd.RaiseCanExecuteChanged();
            CreateTemplateCmd.RaiseCanExecuteChanged();
            LoadTemplateCmd.RaiseCanExecuteChanged();
            SaveTemplateCmd.RaiseCanExecuteChanged();
            LoadReferenceImageCmd.RaiseCanExecuteChanged();
            StartDetectCmd.RaiseCanExecuteChanged();
            StopDetectCmd.RaiseCanExecuteChanged();
        }
<<<<<<< Updated upstream
=======


        #region 对话框辅助方法（直接在 ViewModel 中）

        private Task<bool> ShowConfirmationDialogAsync(string message, string title = "确认")
        {
            var tcs = new TaskCompletionSource<bool>();

            var parameters = new DialogParameters
            {
                { "title", title },
                { "message", message },
                { "confirmText", "确定" },
                { "cancelText", "取消" }
            };

            _dialogService.ShowDialog(
                "ConfirmationDialog",
                parameters,
                result =>
                {
                    if (result.Result == ButtonResult.OK)
                    {
                        var confirmed = result.Parameters.GetValue<bool>("Confirmed");
                        tcs.SetResult(confirmed);
                    }
                    else
                    {
                        tcs.SetResult(false);
                    }
                });

            return tcs.Task;
        }

        private Task ShowInfoDialogAsync(string message, string title = "提示")
        {
            var tcs = new TaskCompletionSource<bool>();

            // 使用 Prism 内置的 NotificationDialog
            var parameters = new DialogParameters
            {
                { "title", title },
                { "content", message }
            };

            _dialogService.ShowDialog(
                "NotificationDialog",
                parameters,
                result => tcs.SetResult(true));

            return tcs.Task;
        }

        private Task ShowErrorDialogAsync(string message, string title = "错误")
        {
            var tcs = new TaskCompletionSource<bool>();

            var parameters = new DialogParameters
            {
                { "title", title },
                { "content", message }
            };

            _dialogService.ShowDialog(
                "NotificationDialog",
                parameters,
                result => tcs.SetResult(true));

            return tcs.Task;
        }

        private Task ShowWarningDialogAsync(string message, string title = "警告")
        {
            var tcs = new TaskCompletionSource<bool>();

            var parameters = new DialogParameters
            {
                { "title", title },
                { "content", message }
            };

            _dialogService.ShowDialog(
                "NotificationDialog",
                parameters,
                result => tcs.SetResult(true));

            return tcs.Task;
        }

<<<<<<< HEAD
        private Task<bool> ShowTemplateNameDialogAsync(out string templateName)
        {
            var tcs = new TaskCompletionSource<bool>();
            string name = null;
            _dialogService.ShowDialog("TemplateNameDialogView",
                result=>{
                    if(result.Result == ButtonResult.OK)
                    {
                         name = result.Parameters.GetValue<string>("TemplateName");
                         tcs.SetResult(true);
                    }
                    else
                    {
                        name = string.Empty;
                        tcs.SetResult(false);
                    }

                 });
            templateName = name;
            return tcs.Task;
        }

        private Task<bool> ShowFileListDialogAsync( string filePath,out FileItem templateJson) 
        {
            var tcs = new TaskCompletionSource<bool>();
            var parameters = new DialogParameters
            {
                { "FolderPath", @filePath  }
            };
            FileItem fileItem = null;
            _dialogService.ShowDialog("FileListDiaLogView",
                result =>
                {
                    if (result.Result == ButtonResult.OK)
                    {
                        fileItem = result.Parameters.GetValue<FileItem>("SelectedFile");
                        tcs.SetResult(true);
                    }
                    else
                    {
                        fileItem = null;
                    }
                });
            templateJson = fileItem;
            return tcs.Task;
        }

=======
>>>>>>> 60f0d7d9a62666162f30d5ddd48345f47e8ff02d
        #endregion
>>>>>>> Stashed changes
    }
}
