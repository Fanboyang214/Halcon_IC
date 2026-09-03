using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Vision.ViewModels.Dialog;

namespace Vision.Views.Dialog
{
    /// <summary>
    /// FileDialogView.xaml 的交互逻辑
    /// </summary>
    public partial class FileDialogView : UserControl
    {
        public FileDialogView()
        {
            InitializeComponent();
        }

        /// <summary>双击列表项：文件=选中关闭，文件夹=进入。重命名中忽略。</summary>
        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as FileDialogViewModel;
            if (vm == null) return;
            if (vm.Items.Any(i => i.IsRenaming)) return;

            // 仅当双击发生在某个 ListBoxItem 上才响应
            DependencyObject dep = (DependencyObject)e.OriginalSource;
            while (dep != null && !(dep is ListBoxItem))
                dep = VisualTreeHelper.GetParent(dep);

            if (dep is ListBoxItem container && container.DataContext is FileItem item)
                vm.Open(item);
        }

        /// <summary>行内重命名文本框获得焦点并全选</summary>
        private void RenameTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.Focus();
                tb.SelectAll();
            }
        }

        /// <summary>Enter 提交重命名，Esc 取消</summary>
        private void RenameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is FileItem item)
            {
                var vm = DataContext as FileDialogViewModel;
                if (vm == null) return;

                if (e.Key == Key.Enter)
                {
                    vm.CommitRename(item);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    vm.CancelRename(item);
                    e.Handled = true;
                }
            }
        }

        /// <summary>右键菜单：根据右键点击的条目动态构建“重命名 / 删除”菜单。</summary>
        /// <remarks>
        /// 不在 XAML 的 Style ContextMenu 里写 Click 事件处理器——那样会触发 WPF XAML 编译器
        /// 对 Style 内 ContextMenu 的错误强制转换（XamlParseException），启动即崩。
        /// 改为在 ContextMenuOpening 中动态构建菜单并绑定到右键条目。
        /// </remarks>
        private void ListBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (DataContext is not FileDialogViewModel vm) return;

            // 定位被右键点击的 ListBoxItem
            if (e.OriginalSource is not DependencyObject dep) { e.Handled = true; return; }
            while (dep != null && dep is not ListBoxItem)
                dep = VisualTreeHelper.GetParent(dep);

            if (dep is not ListBoxItem container || container.DataContext is not FileItem item)
            {
                // 右键在空白区域：不弹菜单
                e.Handled = true;
                return;
            }

            var rename = new MenuItem { Header = "重命名" };
            rename.Click += (_, __) => vm.StartRename(item);
            var del = new MenuItem { Header = "删除" };
            del.Click += (_, __) => vm.Delete(item);

            var menu = new ContextMenu();
            menu.Items.Add(rename);
            menu.Items.Add(del);
            container.ContextMenu = menu;
        }
    }
}
