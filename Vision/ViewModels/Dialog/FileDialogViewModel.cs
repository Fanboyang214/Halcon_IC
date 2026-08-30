using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Vision.ViewModels.Dialog
{
    /// <summary>
    /// 文件浏览对话框视图模型，实现 Prism 的 IDialogAware。
    /// 入参：DialogParameters["FolderPath"] = 字符串文件夹路径
    /// 出参：DialogResult(ButtonResult.OK) 携带 Parameters["SelectedFilePath"] = 选中文件完整路径
    /// </summary>
    public class FileDialogViewModel : BindableBase, IDialogAware
    {
        private string _currentPath;
        private FileItem _selectedItem;
        private string _title = "选择文件";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>当前所在文件夹路径（来自入参 FolderPath）</summary>
        public string CurrentPath
        {
            get => _currentPath;
            set => SetProperty(ref _currentPath, value);
        }

        public FileItem SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public ObservableCollection<FileItem> Items { get; } = new ObservableCollection<FileItem>();

        public DelegateCommand<FileItem> OpenCommand { get; }
        public DelegateCommand<FileItem> RenameCommand { get; }
        public DelegateCommand<FileItem> CommitRenameCommand { get; }
        public DelegateCommand<FileItem> CancelRenameCommand { get; }
        public DelegateCommand<FileItem> DeleteCommand { get; }
        public DelegateCommand SelectCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public event Action<IDialogResult> RequestClose;

        public FileDialogViewModel()
        {
            OpenCommand = new DelegateCommand<FileItem>(Open);
            RenameCommand = new DelegateCommand<FileItem>(StartRename);
            CommitRenameCommand = new DelegateCommand<FileItem>(CommitRename);
            CancelRenameCommand = new DelegateCommand<FileItem>(CancelRename);
            DeleteCommand = new DelegateCommand<FileItem>(Delete);
            SelectCommand = new DelegateCommand(Select);
            CancelCommand = new DelegateCommand(Cancel);
        }

        #region IDialogAware

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        /// <summary>对话框打开时由 Prism 调用，读取入参中的文件夹路径</summary>
        public void OnDialogOpened(IDialogParameters parameters)
        {
            var path = parameters.GetValue<string>("FolderPath");
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                path = Directory.GetCurrentDirectory();
            }
            CurrentPath = path;
            LoadItems();
        }

        #endregion

        #region 文件列表加载

        private void LoadItems()
        {
            Items.Clear();
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(CurrentPath).OrderBy(d => d))
                {
                    var info = new DirectoryInfo(dir);
                    Items.Add(new FileItem
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        Modified = info.LastWriteTime,
                        IsDirectory = true
                    });
                }

                foreach (var file in Directory.EnumerateFiles(CurrentPath).OrderBy(f => f))
                {
                    var info = new FileInfo(file);
                    Items.Add(new FileItem
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        Modified = info.LastWriteTime,
                        IsDirectory = false,
                        Size = info.Length
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法读取文件夹：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region 命令处理

        /// <summary>双击：文件夹进入，文件选中并关闭</summary>
        public void Open(FileItem item)
        {
            if (item == null) return;

            if (item.IsDirectory)
            {
                CurrentPath = item.FullPath;
                LoadItems();
            }
            else
            {
                SelectFile(item);
            }
        }

        private void SelectFile(FileItem item)
        {
            var result = new DialogParameters { { "SelectedFile", item } };
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK, result));
        }

        /// <summary>右键“重命名”：进入行内编辑状态</summary>
        public void StartRename(FileItem item)
        {
            if (item == null) return;
            foreach (var i in Items) i.IsRenaming = false;
            item.RenameText = item.Name;
            item.IsRenaming = true;
        }

        /// <summary>提交重命名</summary>
        public void CommitRename(FileItem item)
        {
            if (item == null) return;

            var newName = (item.RenameText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(newName) || newName == item.Name)
            {
                item.IsRenaming = false;
                return;
            }

            var parent = Path.GetDirectoryName(item.FullPath);
            var newPath = Path.Combine(parent, newName);

            try
            {
                if (item.IsDirectory)
                    Directory.Move(item.FullPath, newPath);
                else
                    File.Move(item.FullPath, newPath);

                item.IsRenaming = false;
                LoadItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重命名失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>取消重命名</summary>
        public void CancelRename(FileItem item)
        {
            if (item != null) item.IsRenaming = false;
        }

        /// <summary>右键“删除”：确认后删除并刷新</summary>
        public void Delete(FileItem item)
        {
            if (item == null) return;

            var kind = item.IsDirectory ? "文件夹" : "文件";
            var answer = MessageBox.Show($"确定要删除{kind} “{item.Name}” 吗？",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            try
            {
                if (item.IsDirectory)
                    Directory.Delete(item.FullPath, true);
                else
                    File.Delete(item.FullPath);

                LoadItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Select()
        {
            if (SelectedItem != null && !SelectedItem.IsDirectory)
                SelectFile(SelectedItem);
        }

        private void Cancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        #endregion
    }

    /// <summary>
    /// 列表中的单个条目（文件或文件夹）。同时承载行内重命名所需的 UI 状态。
    /// </summary>
    public class FileItem : BindableBase
    {
        private bool _isRenaming;
        private string _renameText;

        public string Name { get; set; }
        public string FullPath { get; set; }
        public long Size { get; set; }
        public System.DateTime Modified { get; set; }
        public bool IsDirectory { get; set; }

        /// <summary>图标（文件夹 / 文件）</summary>
        public string Icon => IsDirectory ? "📁" : "📄";

        /// <summary>人类可读的大小；文件夹不显示</summary>
        public string SizeText => IsDirectory ? string.Empty : FormatSize(Size);

        /// <summary>是否处于行内重命名状态（由视图通过样式切换显示）</summary>
        public bool IsRenaming
        {
            get => _isRenaming;
            set => SetProperty(ref _isRenaming, value);
        }

        /// <summary>重命名时用户输入的新名称</summary>
        public string RenameText
        {
            get => _renameText;
            set => SetProperty(ref _renameText, value);
        }

        private static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int i = 0;
            while (size >= 1024 && i < units.Length - 1)
            {
                size /= 1024;
                i++;
            }
            return $"{size:0.##} {units[i]}";
        }
    }
}
