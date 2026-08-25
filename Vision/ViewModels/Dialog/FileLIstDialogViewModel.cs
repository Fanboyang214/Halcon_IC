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

namespace Vision.ViewModels.Dialog
{
    public class FileListDialogViewModel : BindableBase, IDialogAware
    {
        private ObservableCollection<FileItem> _fileItems;
        private FileItem _selectedFile;
        private string _searchText;
        private bool _isLoading;

        public string Title => "选择文件";

        public ObservableCollection<FileItem> FileItems
        {
            get => _fileItems;
            set => SetProperty(ref _fileItems, value);
        }

        public FileItem SelectedFile
        {
            get => _selectedFile;
            set
            {
                if (SetProperty(ref _selectedFile, value))
                {
                    ConfirmCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterFiles();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public event Action<IDialogResult> RequestClose;

        public DelegateCommand ConfirmCommand { get; }
        public DelegateCommand CancelCommand { get; }
        public DelegateCommand LoadFilesCommand { get; }
        public DelegateCommand<FileItem> SelectFileCommand { get; }
        public DelegateCommand<FileItem> DoubleClickCommand { get; }

        private ObservableCollection<FileItem> _allFiles;
        private string _folderPath;

        public FileListDialogViewModel()
        {
            FileItems = new ObservableCollection<FileItem>();
            _allFiles = new ObservableCollection<FileItem>();

            ConfirmCommand = new DelegateCommand(OnConfirm, CanConfirm);
            CancelCommand = new DelegateCommand(OnCancel);
            LoadFilesCommand = new DelegateCommand(LoadFiles);
            SelectFileCommand = new DelegateCommand<FileItem>(OnSelectFile);
            DoubleClickCommand = new DelegateCommand<FileItem>(OnDoubleClick);
        }

        private bool CanConfirm()
        {
            return SelectedFile != null;
        }

        private void OnConfirm()
        {
            var parameters = new DialogParameters
            {
                { "SelectedFile", SelectedFile }
            };
            RaiseRequestClose(new DialogResult(ButtonResult.OK, parameters));
        }

        private void OnCancel()
        {
            RaiseRequestClose(new DialogResult(ButtonResult.Cancel));
        }

        private void OnSelectFile(FileItem fileItem)
        {
            SelectedFile = fileItem;
        }

        private void OnDoubleClick(FileItem fileItem)
        {
            SelectedFile = fileItem;
            OnConfirm();
        }

        private void FilterFiles()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FileItems = new ObservableCollection<FileItem>(_allFiles);
            }
            else
            {
                var filtered = _allFiles.Where(f =>
                    f.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                );
                FileItems = new ObservableCollection<FileItem>(filtered);
            }
            RaisePropertyChanged(nameof(FileItems));
        }

        private async void LoadFiles()
        {
            if (string.IsNullOrEmpty(_folderPath) || !Directory.Exists(_folderPath))
            {
                System.Windows.MessageBox.Show("指定的路径不存在或无效！", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            IsLoading = true;
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    var files = new ObservableCollection<FileItem>();

                    // 获取所有文件（不递归子文件夹）
                    var directoryFiles = Directory.GetFiles(_folderPath)
                        .Select(f => new FileItem
                        {
                            FileName = Path.GetFileName(f),
                            FullPath = f,
                            Directory = Path.GetDirectoryName(f),
                            Extension = Path.GetExtension(f),
                            Size = new FileInfo(f).Length,
                            ModifiedTime = File.GetLastWriteTime(f)
                        });

                    foreach (var file in directoryFiles)
                    {
                        files.Add(file);
                    }

                    _allFiles = files;
                    FileItems = new ObservableCollection<FileItem>(_allFiles);
                    RaisePropertyChanged(nameof(FileItems));
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"加载文件失败: {ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public virtual void OnDialogOpened(IDialogParameters parameters)
        {
            // 从参数中获取文件夹路径
            if (parameters.TryGetValue<string>("FolderPath", out var path))
            {
                _folderPath = path;
                LoadFiles();
            }
            else
            {
                System.Windows.MessageBox.Show("未指定文件夹路径！", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        protected virtual void RaiseRequestClose(IDialogResult dialogResult)
        {
            RequestClose?.Invoke(dialogResult);
        }

        public virtual bool CanCloseDialog()
        {
            return true;
        }

        public virtual void OnDialogClosed()
        {
            // 清理资源
        }
    }

    public class FileItem : BindableBase
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public string Directory { get; set; }
        public string Extension { get; set; }
        public long Size { get; set; }
        public DateTime ModifiedTime { get; set; }

        public string SizeDisplay
        {
            get
            {
                if (Size < 1024)
                    return $"{Size} B";
                else if (Size < 1024 * 1024)
                    return $"{Size / 1024.0:F1} KB";
                else if (Size < 1024 * 1024 * 1024)
                    return $"{Size / (1024.0 * 1024.0):F1} MB";
                else
                    return $"{Size / (1024.0 * 1024.0 * 1024.0):F1} GB";
            }
        }

        public string ModifiedTimeDisplay => ModifiedTime.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
