using Core.Events;
using Core.Interfaces;
using Core.Models;
using HalconDotNet;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.IO;
using System.Windows;
using Vision.Services;
using Vision.ViewModels.Dialog;

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
        private readonly IDialogService _dialogService;

        // Template 框选结果
        private double _templateRow1,_templateColumn1,_templateRow2,_templateColumn2;
        private double _checkRect1Row, _checkRect1Column ,_checkRect1Phi, _checkRect1Length1,_checkRect1Length2;
        private double _checkRect2Row, _checkRect2Column ,_checkRect2Phi, _checkRect2Length1,_checkRect2Length2;
        
        private bool _isTemplateDrawn;
        private bool _isCheckXld1Drawn;
        private bool _isCheckXld2Drawn;

        // 绑定属性
        private string _statusText = "就绪";
        private string _resultText = "";
        private int _matchScore;
        private int _pinCount;
        private int _pinCount2;
        private bool _isDetecting;
        private bool _isCameraOpen;
        
        private bool _isTemplateCreated;
        private bool _isCheckXld1Created;
        private bool _isCheckXld2Created;

        // 当前帧缓存
        private HObject? _lastFrame;
        private readonly object _frameLock = new();

        private HObject? _checkXld1;
        private HObject? _checkXld2;
        

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

        public bool IsCheckXld1Created
        {
            get => _isCheckXld1Created;
            set
            {
                SetProperty(ref _isCheckXld1Created, value);
                RefreshCommandStates();
            }
        }

        public bool IsCheckXld2Created
        {
            get => _isCheckXld2Created;
            set
            {
                SetProperty(ref _isCheckXld2Created, value);
                RefreshCommandStates();
            }
        }

        #endregion

        #region 命令

        public DelegateCommand OpenCameraCmd { get; }
        public DelegateCommand CloseCameraCmd { get; }
        public DelegateCommand StartGrabCmd { get; }
        public DelegateCommand StopGrabCmd { get; }
        
        public DelegateCommand CreateCheckXld1Cmd { get; }
        public DelegateCommand CreateCheckXld2Cmd { get; }
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
        
        /// <summary>
        /// 显示采集图像（由View订阅）
        /// </summary>
        public event Action<HObject>? ImageReady;
        public event Action<HObject,string>? ImageReadyColor;
        public event Action? RequestTemplateCreate;
        public event Action? RequestCheckXld1Create;
        public event Action? RequestCheckXld2Create;
        #endregion

        public VisionWindowViewModel(
            ICameraService camera,
            ITemplateService template,
            IDetectionService detection,
            IEventAggregator eventAggregator,
            ILogService logger,
            IConfigService config,
            IDialogService dialog)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _template = template ?? throw new ArgumentNullException(nameof(template));
            _detection = detection ?? throw new ArgumentNullException(nameof(detection));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _dialogService = dialog ?? throw new ArgumentNullException(nameof(dialog));

            

            OpenCameraCmd = new DelegateCommand(async()=>await ExecuteOpenCamera(), () => !IsCameraOpen);
            CloseCameraCmd = new DelegateCommand( async() =>await  ExecuteCloseCamera(), () => IsCameraOpen);
            StartGrabCmd = new DelegateCommand(async() => await ExecuteStartGrab(), () => IsCameraOpen && !IsDetecting);
            StopGrabCmd = new DelegateCommand(async() =>await ExecuteStopGrab(), () => IsCameraOpen && !IsDetecting);
            CreateCheckXld1Cmd = new DelegateCommand(async()=> await ExecuteCreateCheckXld1(), () => IsTemplateCreated );
            CreateCheckXld2Cmd = new DelegateCommand(async()=>await ExecuteCreateCheckXld2(), () => IsTemplateCreated );
            CreateTemplateCmd = new DelegateCommand(async() => await ExecuteCreateTemplate(), () => IsCameraOpen && !IsDetecting);
            LoadTemplateCmd = new DelegateCommand(async()=> await ExecuteLoadTemplate(), () => !IsDetecting);
            SaveTemplateCmd = new DelegateCommand(async()=>await ExecuteSaveTemplate(), () => IsTemplateCreated);
            LoadReferenceImageCmd = new DelegateCommand(ExecuteLoadReferenceImage, () => !IsDetecting);
            StartDetectCmd = new DelegateCommand(async()=>await ExecuteStartDetect(), () => IsTemplateCreated && !IsDetecting);
            StopDetectCmd = new DelegateCommand(async()=> await ExecuteStopDetect(), () => IsDetecting);

            
        }

        

        public async Task SetCheckXld1(double hv_r2Row,double hv_r2Column,double hv_r2Length1,double hv_r2Length2,double hv_r2Phi)
        {

            try
            {
                _checkRect1Row = hv_r2Row;
                _checkRect1Column = hv_r2Column;
                _checkRect1Phi = hv_r2Phi;
                _checkRect1Length1 = hv_r2Length1;
                _checkRect1Length2 = hv_r2Length2;

                HOperatorSet.GenRectangle2ContourXld(out HObject ho_r2Rectangle, hv_r2Row, hv_r2Column, hv_r2Phi, hv_r2Length1, hv_r2Length2);
                if (ho_r2Rectangle == null || !ho_r2Rectangle.IsInitialized())
                {
                    AddLog("ERROR", "生成检测区域1失败: 坐标或尺寸无效");
                    await ShowErrorDialogAsync("生成检测区域1失败，请检查绘制的坐标和尺寸");
                    return;
                }

                if (_checkXld1 != null && _checkXld1.IsInitialized())
                {
                    _checkXld1?.Dispose();
                }

                _checkXld1 = ho_r2Rectangle;
                _isCheckXld1Drawn = true;
                AddLog("INFO", "检测区域1设置成功");
                await ShowInfoDialogAsync("检测区域1设置成功");
            }
            catch (Exception ex)
            {
                AddLog("ERROR", $"设置检测区域1失败: {ex.Message}");
                MessageBox.Show($"设置检测区域1失败：{ex.Message}");
                if (_checkXld1 != null && _checkXld1.IsInitialized())
                {
                    _checkXld1?.Dispose();
                }
                _checkXld1 = null;
                _isCheckXld1Drawn = false;
            }
        }

        public async Task SetCheckXld2(double hv_r3Row, double hv_r3Column, double hv_r3Length1, double hv_r3Length2, double hv_r3Phi)
        {

            try
            {
                _checkRect2Row = hv_r3Row;
                _checkRect2Column = hv_r3Column;
                _checkRect2Phi = hv_r3Phi;
                _checkRect2Length1 = hv_r3Length1;
                _checkRect2Length2 = hv_r3Length2;

                HOperatorSet.GenRectangle2ContourXld(out HObject ho_r2Rectangle, hv_r3Row, hv_r3Column, hv_r3Phi, hv_r3Length1, hv_r3Length2);
                if (ho_r2Rectangle == null || !ho_r2Rectangle.IsInitialized())
                {
                    AddLog("ERROR", "生成检测区域1失败: 坐标或尺寸无效");
                    await ShowErrorDialogAsync("生成检测区域1失败，请检查绘制的坐标和尺寸");
                    return;
                }

                if (_checkXld2 != null && _checkXld2.IsInitialized())
                {
                    _checkXld2?.Dispose();
                }

                _checkXld2 = ho_r2Rectangle;
                _isCheckXld2Drawn = true;
                AddLog("INFO", "检测区域1设置成功");
                await ShowInfoDialogAsync("检测区域1设置成功");
            }
            catch (Exception ex)
            {
                AddLog("ERROR", $"设置检测区域1失败: {ex.Message}");
                MessageBox.Show($"设置检测区域1失败：{ex.Message}");
                if (_checkXld2 != null && _checkXld2.IsInitialized())
                {
                    _checkXld2?.Dispose();
                }
                _checkXld2 = null;
                _isCheckXld2Drawn = false;
            }
        }







        #region 命令实现

        private async Task ExecuteOpenCamera()
        {
            if (IsCameraOpen)
            {
                AddLog("WARN", "相机已打开");
                return;
            }
            AddLog("INFO", "正在打开相机...");
            try
            {
                var camSettings = _config.Camera;

                await Task.Run(() =>
                {
                    try { _camera.Open(camSettings); }
                    catch (Exception ex)
                    {
                        throw new Exception($"相机打开失败：{ex.Message}");
                    }
                });

                if (!_camera.IsOpen)
                {
                    AddLog("ERROR", "相机打开失败：设备未响应");
                    await  ShowErrorDialogAsync("相机打开失败！设备未响应\n\n请检查：\n1. 相机是否上电\n2. 网线是否连接\n3. 相机IP是否可达\n4. Halcon是否正确安装", "错误");
                    return;
                }
               
                IsCameraOpen = true;
                

                _grabSubToken?.Dispose();
                _grabSubToken = _eventAggregator.GetEvent<ImageGrabbedEvent>()
                    .Subscribe(OnImageGrabbed, ThreadOption.UIThread);
                AddLog("INFO", "相机已打开，等待采集...");
            }
            catch (Exception ex)
            {
                
                AddLog("ERROR", $"相机打开失败: {ex.Message}");
                await ShowErrorDialogAsync($"相机打开失败：{ex.Message}", "错误");

            }
        }

        private async Task ExecuteCloseCamera()
        {
            AddLog("INFO", "相机正在关闭...");
            try
            {
                if (IsDetecting)  ExecuteStopDetect();

                await Task.Run(() =>
                {
                    _camera.Close();
                });
                AddLog("DEBUG", "相机设备已关闭");
                IsCameraOpen = false;

                await Task.Run(() =>
                {
                    _template.ClearTemplate();
                });
                AddLog("DEBUG", "模板数据已清除");
                
                if(_lastFrame != null && _lastFrame.IsInitialized())
                {
                    _lastFrame.Dispose();
                }

               _lastFrame = null;

                if(_checkXld1 != null && _checkXld1.IsInitialized())
                {
                    _checkXld1.Dispose();
                }
                _checkXld1 = null;
                _isCheckXld1Drawn = false;
                if(_checkXld2  != null && _checkXld2.IsInitialized() )
                {
                    _checkXld2.Dispose();
                }
                _checkXld2 = null;
                _isCheckXld2Drawn = false;

                AddLog("INFO", "相机关闭完成，所有资源已释放");
                await ShowInfoDialogAsync("相机关闭完成，所有资源已释放", "通知");
                
            }
            catch (Exception ex)
            {

                AddLog("ERROR", $"相机关闭失败: {ex.Message}");
                await ShowErrorDialogAsync("相机关闭失败", "错误");
            }
        }

        private async Task ExecuteStartGrab()
        {
            if (_camera == null)
            {
                AddLog("ERROR", "开始采集失败: 相机服务未初始化");
                await ShowErrorDialogAsync("相机服务未初始化", "错误");
                return;
            }

            if (!IsCameraOpen || !_camera.IsOpen)
            {
                AddLog("ERROR", "开始采集失败: 相机未打开");
                await ShowErrorDialogAsync("相机未打开，请先点击\"打开相机\"", "错误");
                return;
            }

            if (_camera.IsGrabbing)
            {
                AddLog("WARN", "相机已在采集中");
                return;
            }

            try
            {
                await Task.Run(() => { _camera.StartGrabbing(); });
                AddLog("INFO", "相机图像采集已启动");
            }
            catch (Exception ex)
            {
                AddLog("ERROR", $"图像采集异常: {ex.Message}");
                await ShowErrorDialogAsync($"图像采集异常：{ex.Message}", "错误");
            }
        }

        private async Task ExecuteStopGrab()
        {
            try
            {
                await Task.Run(() => { _camera.StopGrabbing(); });
                AddLog("INFO", "相机图像采集已停止");              
            }
            catch (Exception ex)
            {
                AddLog("ERROR", $"停止采集异常: {ex.Message}");
                await ShowErrorDialogAsync($"停止采集异常：{ex.Message}", "错误");
            }
        }

        private async Task ExecuteCreateCheckXld1()
        {
            AddLog("INFO", "开始绘制检测区域1...");
            if (!IsCameraOpen || _lastFrame == null)
            {
                AddLog("ERROR", "绘制模板失败: 相机未打开或无图像或未绘制模板");
                await ShowErrorDialogAsync("绘制模板失败: 相机未打开或无图像或未绘制模板\n请先打开相机,确保画面有图像，并绘制模板");
                return;
            }
            if (_isCheckXld1Drawn)
            {

                AddLog("WARN", "检测区域1已存在");
                await ShowWarningDialogAsync("检测区域1已绘制，请勿重复绘制");
                return;
            }
            try
            {
                AddLog("INFO", "请在图像窗口绘制检测区域1");
                RequestCheckXld1Create?.Invoke();
            } catch (Exception ex)
            {
                AddLog("ERROR",$"{ex.Message}");
                await ShowErrorDialogAsync($"{ex.Message}");
            }
        }

        private async Task ExecuteCreateCheckXld2()
        {
            if (!IsCameraOpen || !_isTemplateDrawn || !_isCheckXld1Drawn)
            {
                AddLog("ERROR", "绘制检测区域2失败: 请先创建模板和检测区域1");
                await ShowErrorDialogAsync("绘制检测区域2失败: 请先创建模板和检测区域1");
                return;
            }
            if (_isCheckXld2Drawn)
            {
                AddLog("WARN", "检测区域2已存在");
                await ShowWarningDialogAsync("检测区域2已存在");
                return;
            }
            AddLog("INFO", "请在图像窗口绘制检测区域2");
            RequestCheckXld2Create?.Invoke();
        }

        private async Task ExecuteCreateTemplate()
        {
            AddLog("INFO", "开始创建模板...");
            if(!IsCameraOpen || _lastFrame == null)
            {
                AddLog("ERROR", "绘制模板失败: 相机未打开或无图像");
                await ShowErrorDialogAsync("绘制模板失败: 相机未打开或无图像\n请先打开相机,确保画面有图像");
                return;
            }
            if (_template.IsTemplateCreated)
            {
                AddLog("WARN", "模板已创建，请先清除再重新创建");
                if(await ShowConfirmationDialogAsync("是否清除已创建模板"))
                    await Task.Run(async()=> _template.ClearTemplate() );
            }
            AddLog("INFO", "请在图像窗口绘画模板区域");
            try
            {
                 RequestTemplateCreate?.Invoke();
            }catch(Exception ex)
            {
                AddLog("ERROR", $"{ex.Message}");
                await ShowErrorDialogAsync($"{ex.Message}");
            }
           



        }

        private async Task ExecuteLoadTemplate()
        {
           
            if (!Directory.Exists(TemplatesDir))
            {
                AddLog("WARN", "模板目录加载失败，未找到模板目录");
                await ShowWarningDialogAsync("模板目录加载失败，未找到模板目录\n请先保存模板，创建模板目录", "警告");
            }

            FileItem templateJson = null;
            await ShowFileListDialogAsync(TemplatesDir, out templateJson);
            if (templateJson == null || Directory.GetFiles(templateJson.FullPath).LongLength == 0)
            {
                AddLog("WARN", "模板加载失败，模板为空");
                await ShowWarningDialogAsync("模板加载失败，模板为空");
                return;
            }

            if (!IsCameraOpen)
            {
                AddLog("WARN", "加载模板失败: 相机未打开");
                await ShowWarningDialogAsync("加载模板失败: 相机未打开");
                return;
            }

            if (_lastFrame == null || !_lastFrame.IsInitialized())
            {
                AddLog("WARN", "加载模板失败: 当前无有效图像");
                await ShowWarningDialogAsync("加载模板失败: 当前无有效图像");
                return;
            }
            
            try
            {
                string json = File.ReadAllText(templateJson.FullPath, System.Text.Encoding.UTF8);
                var config = TemplateConfig.FromJson(json);
                if(config == null)
                {
                    AddLog("ERROR", "加载模板失败: 配置文件内容无效");
                    await ShowErrorDialogAsync("加载模板失败: 配置文件内容无效");
                    return;
                }

                if (IsDetecting)
                {
                    ExecuteStopDetect();
                }

                IsTemplateCreated = false;
                IsCheckXld1Created = false;
                IsCheckXld2Created = false;

                SetTemplateRegion(config.TemplateRow1, config.TemplateColumn1, config.TemplateRow2, config.TemplateColumn2);

                if (!_template.IsTemplateCreated)
                {
                    AddLog("ERROR", "加载模板失败: 模板重建失败");
                    await ShowErrorDialogAsync("加载模板失败: 模板重建失败");
                    return;
                }
                // 重建两个检测区域的旋转矩形
                await SetCheckXld1(config.CheckRect1Row, config.CheckRect1Column, config.CheckRect1Phi, config.CheckRect1Length1, config.CheckRect1Length2);
                await SetCheckXld2(config.CheckRect2Row, config.CheckRect2Column, config.CheckRect2Phi, config.CheckRect2Length1, config.CheckRect2Length2);

                AddLog("INFO", $"模板 \"{templateJson.Name}\" 已成功加载");
                await ShowInfoDialogAsync($"模板 \"{templateJson.Name}\" 已成功加载");
            }
            catch(Exception ex)
            {
                AddLog("ERROR", $"加载模板失败：{ex.Message}");
                await ShowErrorDialogAsync($"加载模板失败：{ex.Message}");
            }

        }

        // 模板文件存储目录，位于程序根目录下的Templates文件夹

        private static readonly string TemplatesDir = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Templates");
        private async Task ExecuteSaveTemplate()
        {
            if (!IsTemplateCreated)
            {
                AddLog("WARN", "模板保存失败，模板未创建");
                await ShowWarningDialogAsync("模板保存失败，模板未创建", "警告");
                return;
            }

            if(!IsCheckXld1Created || !IsCheckXld2Created)
            {
                AddLog("WARN", "模板保存失败，检测区域未绘制");
                await ShowWarningDialogAsync("模板保存失败，检测区域未绘制", "警告");
                return;
            }

            string templateName = string.Empty;
            await ShowTemplateNameDialogAsync(out templateName);
            templateName=templateName.Trim();
            if (string.IsNullOrWhiteSpace(templateName))
            {
                AddLog("WARN", "模板保存失败，模板名称不能为空");
                await ShowWarningDialogAsync("模板保存失败，模板名称不能为空", "警告");
                return;
            }

            foreach(var c in System.IO.Path.GetInvalidFileNameChars())
            {
                if (templateName.Contains(c))
                {
                    AddLog("WARN", "模板保存失败，模板名称不能含有非法字符（如 \\ / : * ? \" < > |）");
                    await ShowWarningDialogAsync("模板保存失败，模板名称不能含有非法字符（如 \\ / : * ? \" < > |）", "警告");
                    return;
                }

            }

            try
            {
                Directory.CreateDirectory(TemplatesDir);
                var config = new TemplateConfig
                {
                    TemplateName = templateName,
                    TemplateRow1 = _templateRow1,
                    TemplateColumn1 = _templateColumn1,
                    TemplateRow2 = _templateRow2,
                    TemplateColumn2 = _templateColumn2,
                    CheckRect1Row = _checkRect1Row,
                    CheckRect1Column = _checkRect1Column,
                    CheckRect1Phi = _checkRect1Phi,
                    CheckRect1Length1 = _checkRect1Length1,
                    CheckRect1Length2 = _checkRect1Length2,
                    CheckRect2Row = _checkRect2Row,
                    CheckRect2Column = _checkRect2Column,
                    CheckRect2Phi = _checkRect2Phi,
                    CheckRect2Length1 = _checkRect2Length1,
                    CheckRect2Length2 = _checkRect2Length2
                };
                string filePath = Path.Combine(TemplatesDir, $"{templateName}.json");
                if (Path.Exists(filePath))
                {
                   var result = await ShowConfirmationDialogAsync($"模板 \"{templateName}\" 已存在，是否覆盖？", "确认覆盖");
                    if (!result)
                    {
                        AddLog("INFO", "保存模板已取消（不覆盖）");
                        return;
                    }
                }
                string templateJson = config.ToJson();
                File.WriteAllText(filePath, templateJson, System.Text.Encoding.UTF8);

                AddLog("INFO", "模板保存成功");
                await ShowInfoDialogAsync("模板保存成功");
            }catch(Exception ex)
            {
                AddLog("ERROR", $"模板保存失败：{ex.Message}");
                await ShowErrorDialogAsync($"模板保存失败：{ex.Message}");
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
                //ShowImage(img);
                ImageReady?.Invoke(img);
                lock (_frameLock)
                {
                    _lastFrame?.Dispose();
                    _lastFrame = img.Clone();
                }
                //StatusText = $"参考图已加载: {Path.GetFileName(dlg.FileName)}";
                AddLog("INFO", $"参考图已加载: {Path.GetFileName(dlg.FileName)}");
            }
            catch (Exception ex)
            {
                //StatusText = $"加载参考图失败: {ex.Message}";
                AddLog("ERROR", $"加载参考图失败: {ex.Message}");
            }
        }

        private async Task ExecuteStartDetect()
        {
            AddLog("INFO", "启动实时检测...");
            ExecuteStopDetect();

            if (_camera == null || _template == null || _detection == null)
            {
                AddLog("ERROR", "启动检测失败: 服务未初始化");
                await ShowErrorDialogAsync("服务未初始化，无法开始预览");
                return;
            }

            if (!IsCameraOpen || !_camera.IsGrabbing)
            {
                AddLog("WARN", "启动实时检测失败：相机未打开或相机未采集");
                return;
            }

            if(!_template.IsTemplateCreated || _template.ModelID == null || _template.ModelID.Length == 0)
            {
                AddLog("ERROR", "启动检测失败: 模板未创建");
                await ShowErrorDialogAsync("模板未创建，请先绘制模板");
                return;
            }
            if (!_isCheckXld1Drawn || !_isCheckXld2Drawn || _checkXld1 == null || _checkXld2 == null)
            {
                AddLog("ERROR", "启动检测失败: 检测区域未完整绘制");
                await ShowErrorDialogAsync("请先绘制检测区域");
                return;
            }
            try
            {
                _detection.Start(_template, _inspectionConfig);
                _detection.ResultReady += OnDetectionResultReady;

                if (!_camera.IsGrabbing)
                {
                    await ExecuteStartGrab();
                }

                IsDetecting = true;
                AddLog("INFO", "实时检测已启动");
            }catch(Exception ex)
            {
                AddLog("ERROR", $"启动实时检测失败: {ex.Message}");
                await ShowErrorDialogAsync($"开始预览失败：{ex.Message}");
                IsDetecting = false;
            }
        }

        private async Task ExecuteStopDetect()
        {
            try
            {
                if (_detection != null)
                {
                    _detection.ResultReady -= OnDetectionResultReady;
                    _detection.Stop(3000);
                }
                IsDetecting = false;
                AddLog("INFO", "实时检测已停止");
            }
            catch (Exception ex)
            {
                AddLog("ERROR", $"停止检测异常: {ex.Message}");
                await ShowErrorDialogAsync($"停止检测异常: {ex.Message}");
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
                ImageReady?.Invoke(payload.Image);

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
                AddLog("ERROR", $"OnImageGrabbed 异常: {ex.Message}");
            }
            finally
            {
                payload.Dispose();
            }
        }

        /// <summary>
        /// DetectionService.ResultReady 事件回调。
        /// 消费者线程发布，需要 marshal 到 UI 线程更新界面。
        /// </summary>
        private void OnDetectionResultReady(DetectionResult result)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                OnDetectionResult(result);
            });
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
                //HalconWindow.SetPart(0, 0, -1, -1);
                //HalconWindow.DispObj(result.DisplayImage);
                ImageReady?.Invoke(result.DisplayImage);
                result.DisplayImage.Dispose();
            }

            if (result.ModelContours != null)
            {
                try
                {
                    //HalconWindow.SetColor("green");
                    //HalconWindow.DispObj(result.ModelContours);
                    ImageReadyColor?.Invoke(result.ModelContours, "green");
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
                    //HalconWindow.SetColor(result.IsOK ? "cyan" : "red");
                    //HalconWindow.DispObj(result.DetectionRegion1);
                    ImageReadyColor?.Invoke(result.DetectionRegion1, result.IsOK ? "cyan" : "red");

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
                    //HalconWindow.SetColor(result.IsOK ? "cyan" : "red");
                    //HalconWindow.DispObj(result.DetectionRegion2);
                    ImageReadyColor?.Invoke(result.DetectionRegion2, result.IsOK ? "cyan" : "red");

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

        private void AddLog(string level,string message)
        {
            _logger.AddLog(level, message);
        }
        private void RefreshCommandStates()
        {
            OpenCameraCmd.RaiseCanExecuteChanged();
            CloseCameraCmd.RaiseCanExecuteChanged();
            StartGrabCmd.RaiseCanExecuteChanged();
            StopGrabCmd.RaiseCanExecuteChanged();
   
            CreateTemplateCmd.RaiseCanExecuteChanged();
            LoadTemplateCmd.RaiseCanExecuteChanged();
            SaveTemplateCmd.RaiseCanExecuteChanged();
            LoadReferenceImageCmd.RaiseCanExecuteChanged();
            StartDetectCmd.RaiseCanExecuteChanged();
            StopDetectCmd.RaiseCanExecuteChanged();
        }

        public async void SetTemplateRegion(double hv_mRow1, double hv_mColumn1, double hv_mRow2, double hv_mColumn2)
        {
            if (_lastFrame == null || !_lastFrame.IsInitialized())
            {
                AddLog("ERROR", "设置模板失败: 当前图像无效");
                await ShowErrorDialogAsync("当前图像无效，无法创建模板，请确保相机正常采集图像");
                return;
            }

            try
            {
                _templateRow1 = hv_mRow1;
                _templateColumn1 = hv_mColumn1;
                _templateRow2 = hv_mRow2;
                _templateColumn2 = hv_mColumn2;

                _template.SetTemplateRegion(hv_mRow1, hv_mColumn1, hv_mRow2, hv_mColumn2);
                _template.CreateTemplate(_lastFrame, hv_mRow1, hv_mColumn1, hv_mRow2, hv_mColumn2);

                if (!_template.IsTemplateCreated || _template.ModelID == null)
                {
                    AddLog("ERROR", "模板创建失败: ModelID无效");
                    await ShowErrorDialogAsync("模板创建失败，ModelID无效");
                    _template.ClearTemplate();
                    return;
                }

                _isTemplateDrawn = true;
                AddLog("INFO", "芯片模板创建成功");
                await ShowInfoDialogAsync("模板创建成功");
            }
            catch (Exception ex)
            {
                AddLog("ERROR", $"设置模板区域失败: {ex.Message}");
                await ShowErrorDialogAsync($"设置模板区域失败：{ex.Message}");
                _template.ClearTemplate();
                _isTemplateDrawn = false;
            }
        }


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
                "ConfirmationDialogView",
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
                "NotificationDialogView",
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
                "NotificationDialogView",
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
                "NotificationDialogView",
                parameters,
                result => tcs.SetResult(true));

            return tcs.Task;
        }

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
            _dialogService.ShowDialog("FileListDialogView",
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

        #endregion
    }
}
    

