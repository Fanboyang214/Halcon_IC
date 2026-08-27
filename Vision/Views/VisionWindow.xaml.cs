using HalconDotNet;
using System;
using System.Windows;
using System.Windows.Threading;
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
            if (DataContext is  VisionWindowViewModel vm) 
            {
                vm.HalconWindow = HalconWindow.HalconWindow;
            
                vm.ImageReady += OnImageReady;
                vm.ImageReadyColor += OnImageReadyColor;
                vm.RequestTemplateCreate += OnRequestTemplateCreate;
                vm.RequestCheckXld1Create += OnRequestCheckXld1Create;
                vm.RequestCheckXld2Create += OnRequestCheckXld2Create;
            }
        }

        private async void OnRequestCheckXld2Create()
        {
            if (HalconWindow.HalconWindow == null || HalconWindow.HalconWindow.IsInitialized() == false)
            {
                MessageBox.Show("Halcon窗口未初始化");
                return;
            }

            HalconWindow.Focus();
            HalconWindow.UpdateLayout();
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            HOperatorSet.DispCross(HalconWindow.HalconWindow, 100, 100, 20, 0);

            try
            {
                HOperatorSet.DrawRectangle2(HalconWindow.HalconWindow, out var hv_r3Row, out var hv_r3Column, out var hv_r3Phi, out var hv_r3Length1, out var hv_r3Length2);
                if (DataContext is VisionWindowViewModel vm)
                {
                    await vm.SetCheckXld2(hv_r3Row, hv_r3Column, hv_r3Length1, hv_r3Length2, hv_r3Phi);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"绘制检测区域1，当前窗口句柄：{HalconWindow.HalconWindow?.Handle}", ex);
            }
        }

        private async void OnRequestCheckXld1Create()
        {
            if (HalconWindow.HalconWindow == null || HalconWindow.HalconWindow.IsInitialized() == false)
            {
                MessageBox.Show("Halcon窗口未初始化");
                return;
            }

            HalconWindow.Focus();
            HalconWindow.UpdateLayout();
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            HOperatorSet.DispCross(HalconWindow.HalconWindow, 100, 100, 20, 0);

            try
            {
                HOperatorSet.DrawRectangle2(HalconWindow.HalconWindow, out var hv_r2Row, out var hv_r2Column, out var hv_r2Phi, out var hv_r2Length1, out var hv_r2Length2);
                if(DataContext is VisionWindowViewModel vm)
                {
                    await vm.SetCheckXld1(hv_r2Row, hv_r2Column, hv_r2Length1, hv_r2Length2, hv_r2Phi);
                }
            }catch (Exception ex)
            {
                throw new Exception($"绘制检测区域1，当前窗口句柄：{HalconWindow.HalconWindow?.Handle}", ex);
            }

        }

        private  void OnRequestTemplateCreate()
        {
            if (HalconWindow.HalconWindow == null || HalconWindow.HalconWindow.IsInitialized() == false)
            {
                MessageBox.Show("Halcon窗口未初始化");
                return;
            }
            HalconWindow.Focus();
            HalconWindow.UpdateLayout();
            Dispatcher.Invoke(()=> { },System.Windows.Threading.DispatcherPriority.Render);

            try
            {
                HOperatorSet.DispCross(HalconWindow.HalconWindow, 100, 100, 20, 0);
                MessageBox.Show("请在图像窗口绘制矩形区域作为模板");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"窗口无效：{ex.Message}");
                return;
            }

            try
            {
                HOperatorSet.DrawRectangle1(HalconWindow.HalconWindow, out var hv_mRow1, out var hv_mColumn1, out var hv_mRow2, out var hv_mColumn2);

                if (DataContext is VisionWindowViewModel vm)
                {
                     vm.SetTemplateRegion(hv_mRow1, hv_mColumn1, hv_mRow2, hv_mColumn2);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"绘制模板失败：{ex.Message}");
            }
        }

        private void OnImageReadyColor(HObject obj,string color)
        {
            if (DataContext is not VisionWindowViewModel) return;
            // obj 可能是 image / region / xld，统一为 HObject，不能用 'is HImage' 检查
            if (obj == null || !obj.IsInitialized()) return;

            if (HalconWindow == null || HalconWindow.HalconWindow == null || !HalconWindow.HalconWindow.IsInitialized()) return;

            try
            {
                HalconWindow.HalconWindow.SetColor(color);
                HalconWindow.HalconWindow.DispObj(obj);
                HalconWindow.UpdateLayout();
                Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示图像失败: {ex.Message}");
            }
        }

        private void OnImageReady(HObject @object)
        {
            if (DataContext is not VisionWindowViewModel) return;
            // GrabImage 返回的是 HObject（不是 HImage 派生类型），不能用 'is HImage' 检查，否则会被直接拦截
            if (@object == null || !@object.IsInitialized()) return;

            if (HalconWindow == null || HalconWindow.HalconWindow == null || !HalconWindow.HalconWindow.IsInitialized()) return;
            
            try
            {
                HalconWindow.HalconWindow.SetPart(0, 0, -1, -1);
                HalconWindow.HalconWindow.DispObj(@object);
                HalconWindow.UpdateLayout();
                Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示图像失败: {ex.Message}");
            }
        }

        

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            

            if (DataContext is VisionWindowViewModel vm)
            {
            
                vm.ImageReady -= OnImageReady;
                vm.ImageReadyColor -= OnImageReadyColor;
                vm.RequestTemplateCreate -= OnRequestTemplateCreate;
                vm.RequestCheckXld1Create -= OnRequestCheckXld1Create;
                vm.RequestCheckXld2Create -= OnRequestCheckXld2Create;

            }
        }
    }
}
