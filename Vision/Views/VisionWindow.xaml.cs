using Core.Models;
using HalconDotNet;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Vision.ViewModels;

namespace Vision.Views
{
    public partial class VisionWindow : System.Windows.Controls.UserControl
    {
        private HDrawingObject? _roiDrawingObject;
        private HDrawingObject.HDrawingObjectCallback? _roiCallback;
        private bool _isRoiAttached;

        // 当前正在交互绘制的 ROI 类型。
        // 用 HDrawingObject（非阻塞）取代 DrawRectangle1/DrawRectangle2（阻塞式交互算子），
        // 避免其内部消息循环与 WPF Dispatcher 争用导致项目卡死。
        private enum RoiMode { None, Template, CheckXld1, CheckXld2 }
        private RoiMode _roiMode = RoiMode.None;

        public VisionWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            // 用键盘确认/取消交互绘制（Enter=确认，Esc=取消）
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is VisionWindowViewModel vm)
            {
                vm.ImageGrabbed += OnImageGrabbed;
                vm.DetectionResultUpdated += OnDetectionResultUpdated;
                vm.RequestTemplateCreate += OnRequestTemplateCreate;
                vm.RequestCheckXld1Create += OnRequestCheckXld1Create;
                vm.RequestCheckXld2Create += OnRequestCheckXld2Create;
            }
        }

        #region 非阻塞 ROI 绘制（HDrawingObject）

        private void OnRequestTemplateCreate()
        {
            if (HalconWindow.HalconWindow == null || !HalconWindow.HalconWindow.IsInitialized())
            {
                MessageBox.Show("Halcon窗口未初始化");
                return;
            }
            StartRoiDrawing(RoiMode.Template);
        }

        private void OnRequestCheckXld1Create()
        {
            if (HalconWindow.HalconWindow == null || !HalconWindow.HalconWindow.IsInitialized())
            {
                MessageBox.Show("Halcon窗口未初始化");
                return;
            }
            StartRoiDrawing(RoiMode.CheckXld1);
        }

        private void OnRequestCheckXld2Create()
        {
            if (HalconWindow.HalconWindow == null || !HalconWindow.HalconWindow.IsInitialized())
            {
                MessageBox.Show("Halcon窗口未初始化");
                return;
            }
            StartRoiDrawing(RoiMode.CheckXld2);
        }

        /// <summary>
        /// 创建并附加一个 HDrawingObject 到 Halcon 窗口，等待用户拖动/缩放。
        /// 不阻塞 UI 线程；用户按 Enter 确认、Esc 取消。
        /// </summary>
        private void StartRoiDrawing(RoiMode mode)
        {
            DetachExistingRoi();
            _roiMode = mode;

            try
            {
                var window = HalconWindow.HalconWindow;

                // 默认矩形：优先按当前显示范围，其次用固定占位
                window.GetPart(out HTuple partRow1, out HTuple partCol1, out HTuple partRow2, out HTuple partCol2);
                double r1, c1, r2, c2;
                if (partRow1.D == 0 && partCol1.D == 0 && partRow2.D == -1 && partCol2.D == -1)
                {
                    r1 = 50; c1 = 50; r2 = 250; c2 = 350;
                }
                else
                {
                    r1 = partRow1.D; c1 = partCol1.D;
                    r2 = partRow2.D; c2 = partCol2.D;
                }

                if (mode == RoiMode.Template)
                {
                    double centerRow = (r1 + r2) / 2.0;
                    double centerCol = (c1 + c2) / 2.0;
                    double width = (r2 - r1) * 0.5;
                    double height = (c2 - c1) * 0.5;

                    double newR1 = centerRow - width / 2.0;
                    double newC1 = centerCol - height / 2.0;
                    double newR2 = centerRow + width / 2.0;
                    double newC2 = centerCol + height / 2.0;

                    _roiDrawingObject = new HDrawingObject(newR1, newC1, newR2, newC2);
                }
                else
                {
                    double cr = (r1 + r2) / 2.0;
                    double cc = (c1 + c2) / 2.0;
                    double len1 = Math.Max(1.0, Math.Abs(r2 - r1) / 2.0);
                    double len2 = Math.Max(1.0, Math.Abs(c2 - c1) / 2.0);
                    _roiDrawingObject = new HDrawingObject(cr, cc, 0.0, len1, len2);
                }

                _roiCallback = new HDrawingObject.HDrawingObjectCallback(OnRoiChanged);
                _roiDrawingObject.OnResize(_roiCallback);
                _roiDrawingObject.OnDrag(_roiCallback);

                HOperatorSet.AttachDrawingObjectToWindow(window, _roiDrawingObject);
                _isRoiAttached = true;

                HalconWindow.Focus();
                HalconWindow.UpdateLayout();
                Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

                UpdateStatusHint();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建绘图对象失败：{ex.Message}");
                _roiMode = RoiMode.None;
            }
        }

        private void UpdateStatusHint()
        {
            string hint = _roiMode switch
            {
                RoiMode.Template => "请在图像窗口调整模板矩形区域，按 Enter 确认，Esc 取消",
                RoiMode.CheckXld1 => "请在图像窗口调整检测区域一，按 Enter 确认，Esc 取消",
                RoiMode.CheckXld2 => "请在图像窗口调整检测区域二，按 Enter 确认，Esc 取消",
                _ => ""
            };
            if (DataContext is VisionWindowViewModel vm) vm.StatusText = hint;
        }

        private void OnRoiChanged(IntPtr drawid, IntPtr windowHandle, string type)
        {
            // 拖动/缩放过程中无需实时处理；按 Enter 时统一确认
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_roiMode == RoiMode.None) return;

            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                ConfirmRoi();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelRoi();
                e.Handled = true;
            }
        }

        private void ConfirmRoi()
        {
            if (_roiDrawingObject == null)
            {
                _roiMode = RoiMode.None;
                return;
            }

            var mode = _roiMode;
            _roiMode = RoiMode.None;

            try
            {
                if (DataContext is not VisionWindowViewModel vm) return;

                if (mode == RoiMode.Template)
                {
                    double r1 = ParamD("row1");
                    double c1 = ParamD("column1");
                    double r2 = ParamD("row2");
                    double c2 = ParamD("column2");
                    DetachExistingRoi();
                    vm.SetTemplateRegion(r1, c1, r2, c2);
                }
                else if (mode == RoiMode.CheckXld1)
                {
                    double row = ParamD("row");
                    double col = ParamD("column");
                    double phi = ParamD("phi");
                    double len1 = ParamD("length1");
                    double len2 = ParamD("length2");
                    DetachExistingRoi();
                    _ = vm.SetCheckXld1(row, col, len1, len2, phi);
                }
                else if (mode == RoiMode.CheckXld2)
                {
                    double row = ParamD("row");
                    double col = ParamD("column");
                    double phi = ParamD("phi");
                    double len1 = ParamD("length1");
                    double len2 = ParamD("length2");
                    DetachExistingRoi();
                    _ = vm.SetCheckXld2(row, col, len1, len2, phi);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"确认绘制失败：{ex.Message}");
                DetachExistingRoi();
            }
        }

        private void CancelRoi()
        {
            _roiMode = RoiMode.None;
            DetachExistingRoi();
        }

        private double ParamD(string param) => _roiDrawingObject!.GetDrawingObjectParams(param).D;

        private void DetachExistingRoi()
        {
            if (_roiDrawingObject != null && _isRoiAttached)
            {
                try
                {
                    HOperatorSet.DetachDrawingObjectFromWindow(HalconWindow.HalconWindow, _roiDrawingObject);
                }
                catch { }
                _isRoiAttached = false;
            }

            if (_roiDrawingObject != null)
            {
                _roiDrawingObject.Dispose();
                _roiDrawingObject = null;
            }
            _roiCallback = null;
        }

        #endregion

        #region 图像显示（Halcon 窗口绘制）

        /// <summary>
        /// ViewModel 采集图像就绪回调：显示实时采集图像。
        /// </summary>
        private void OnImageGrabbed(HObject image)
        {
            DisplayImage(image);
        }

        /// <summary>
        /// ViewModel 检测结果更新回调：在 Halcon 窗口绘制检测结果图像。
        /// </summary>
        private void OnDetectionResultUpdated(DetectionResult result)
        {
            // 显示检测结果叠加图
            if (result.DisplayImage != null)
            {
                try
                {
                    DisplayImage(result.DisplayImage);
                }
                finally
                {
                    result.DisplayImage.Dispose();
                }
            }

            // 显示模板轮廓（绿色）
            if (result.ModelContours != null)
            {
                try
                {
                    DisplayOverlay(result.ModelContours, "green");
                }
                finally
                {
                    result.ModelContours.Dispose();
                }
            }

            // 显示检测区域一（合格=青色，不合格=红色）
            if (result.DetectionRegion1 != null)
            {
                try
                {
                    DisplayOverlay(result.DetectionRegion1, result.IsOK ? "cyan" : "red");
                }
                finally
                {
                    result.DetectionRegion1.Dispose();
                }
            }

            // 显示检测区域二（合格=青色，不合格=红色）
            if (result.DetectionRegion2 != null)
            {
                try
                {
                    DisplayOverlay(result.DetectionRegion2, result.IsOK ? "cyan" : "red");
                }
                finally
                {
                    result.DetectionRegion2.Dispose();
                }
            }
        }

        /// <summary>
        /// 在 Halcon 窗口显示图像（满窗口适配）。
        /// </summary>
        private void DisplayImage(HObject image)
        {
            if (image == null || !image.IsInitialized()) return;
            if (HalconWindow?.HalconWindow == null || !HalconWindow.HalconWindow.IsInitialized()) return;

            try
            {
                HalconWindow.HalconWindow.SetPart(0, 0, -1, -1);
                HalconWindow.HalconWindow.DispObj(image);
                HalconWindow.UpdateLayout();
                Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示图像失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 在 Halcon 窗口叠加显示对象（region / xld 等），使用指定颜色。
        /// </summary>
        private void DisplayOverlay(HObject obj, string color)
        {
            if (obj == null || !obj.IsInitialized()) return;
            if (HalconWindow?.HalconWindow == null || !HalconWindow.HalconWindow.IsInitialized()) return;

            try
            {
                HalconWindow.HalconWindow.SetColor(color);
                HalconWindow.HalconWindow.DispObj(obj);
                HalconWindow.UpdateLayout();
                Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示叠加对象失败: {ex.Message}");
            }
        }

        #endregion

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CancelRoi();
            PreviewKeyDown -= OnPreviewKeyDown;

            if (DataContext is VisionWindowViewModel vm)
            {
                vm.ImageGrabbed -= OnImageGrabbed;
                vm.DetectionResultUpdated -= OnDetectionResultUpdated;
                vm.RequestTemplateCreate -= OnRequestTemplateCreate;
                vm.RequestCheckXld1Create -= OnRequestCheckXld1Create;
                vm.RequestCheckXld2Create -= OnRequestCheckXld2Create;
            }
        }
    }
}