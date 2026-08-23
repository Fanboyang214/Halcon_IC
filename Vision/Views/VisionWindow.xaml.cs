using HalconDotNet;
using System;
using System.Windows;
using Vision.ViewModels;

namespace Vision.Views
{
    /// <summary>
    /// 使用 HALCON HDrawingObject 进行交互式 ROI 绘制。
    /// 取代手动鼠标事件处理，由 ViewModel.DrawRoiCmd 触发。
    /// </summary>
    public partial class VisionWindow : System.Windows.Controls.UserControl
    {
        private HDrawingObject? _roiDrawingObject;
        private HDrawingObject.HDrawingObjectCallback? _roiCallback;
        private bool _isRoiAttached;

        public VisionWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is VisionWindowViewModel vm)
            {
                vm.HalconWindow = HalconWindow.HalconWindow;
                vm.RequestDrawRoi += OnRequestDrawRoi;
                vm.RequestClearRoi += OnRequestClearRoi;
            }
        }

        /// <summary>
        /// ViewModel 触发：创建并附加一个 Rectangle1 HDrawingObject 到 Halcon 窗口。
        /// </summary>
        private void OnRequestDrawRoi()
        {
            if (DataContext is not VisionWindowViewModel vm) return;

            DetachExistingRoi();

            try
            {
                // 默认矩形（中心位置），用户可拖动调整
                var window = HalconWindow.HalconWindow;
                window.GetPart(out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);

                double r1, c1, r2, c2;
                if (row1.D == 0 && col1.D == 0 && row2.D == -1 && col2.D == -1)
                {
                    // 没有设置过 SetPart，使用图像尺寸估算
                    if (vm.TryGetLastFrame(out var frame) && frame != null)
                    {
                        HOperatorSet.GetImageSize(frame, out HTuple w, out HTuple h);
                        r1 = h / 4.0; c1 = w / 4.0;
                        r2 = h * 3.0 / 4.0; c2 = w * 3.0 / 4.0;
                    }
                    else
                    {
                        r1 = 50; c1 = 50; r2 = 250; c2 = 350;
                    }
                }
                else
                {
                    r1 = row1 + (row2 - row1) / 4.0;
                    c1 = col1 + (col2 - col1) / 4.0;
                    r2 = row1 + (row2 - row1) * 3.0 / 4.0;
                    c2 = col1 + (col2 - col1) * 3.0 / 4.0;
                }

                _roiDrawingObject = new HDrawingObject(r1, c1, r2, c2);
                _roiCallback = new HDrawingObject.HDrawingObjectCallback(OnRoiChanged);
                _roiDrawingObject.OnResize(_roiCallback);
                _roiDrawingObject.OnDrag(_roiCallback);

                HOperatorSet.AttachDrawingObjectToWindow(HalconWindow.HalconWindow, _roiDrawingObject);
                _isRoiAttached = true;

                vm.SetRoiDrawing(true);

                // 立即通知一次当前 ROI
                NotifyRoiChanged();
            }
            catch (Exception ex)
            {
                vm.StatusText = $"创建 ROI 绘图对象失败: {ex.Message}";
            }
        }

        private void OnRoiChanged(IntPtr drawid, IntPtr windowHandle, string type)
        {
            NotifyRoiChanged();
        }

        /// <summary>
        /// ViewModel 触发：清除 ROI 绘图对象（保存/创建模板成功后调用）。
        /// </summary>
        private void OnRequestClearRoi()
        {
            DetachExistingRoi();
        }

        private void NotifyRoiChanged()
        {
            if (_roiDrawingObject == null) return;
            if (DataContext is not VisionWindowViewModel vm) return;

            try
            {
                double row1 = _roiDrawingObject.GetDrawingObjectParams("row1");
                double col1 = _roiDrawingObject.GetDrawingObjectParams("column1");
                double row2 = _roiDrawingObject.GetDrawingObjectParams("row2");
                double col2 = _roiDrawingObject.GetDrawingObjectParams("column2");

                vm.SetRoi(row1, col1, row2, col2);
            }
            catch { }
        }

        private void DetachExistingRoi()
        {
            if (_roiDrawingObject != null && _isRoiAttached)
            {
                try
                {
                    HOperatorSet.DetachDrawingObjectFromWindow(
                        HalconWindow.HalconWindow, _roiDrawingObject);
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

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachExistingRoi();

            if (DataContext is VisionWindowViewModel vm)
            {
                vm.RequestDrawRoi -= OnRequestDrawRoi;
                vm.RequestClearRoi -= OnRequestClearRoi;
            }
        }
    }
}
