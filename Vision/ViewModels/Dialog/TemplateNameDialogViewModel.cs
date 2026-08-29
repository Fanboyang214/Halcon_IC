using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.ViewModels.Dialog
{
   
        public class TemplateNameDialogViewModel : BindableBase, IDialogAware
        {
            private string _templateName;
            public string TemplateName
            {
                get { return _templateName; }
                set
                {
                    SetProperty(ref _templateName, value);
                    ConfirmCommand.RaiseCanExecuteChanged();
                }
            }

            public string Title => "创建模板";

            public event Action<IDialogResult> RequestClose;

            public TemplateNameDialogViewModel()
            {
                ConfirmCommand = new DelegateCommand(OnConfirm, CanConfirm);
                CancelCommand = new DelegateCommand(OnCancel);
            }

            public DelegateCommand ConfirmCommand { get; }
            public DelegateCommand CancelCommand { get; }

            private bool CanConfirm()
            {
                return !string.IsNullOrWhiteSpace(TemplateName);
            }

            private void OnConfirm()
            {
                var parameters = new DialogParameters
            {
                { "TemplateName", TemplateName }
            };
                RaiseRequestClose(new DialogResult(ButtonResult.OK, parameters));
            }

            private void OnCancel()
            {
                RaiseRequestClose(new DialogResult(ButtonResult.Cancel));
            }

            protected virtual void RaiseRequestClose(IDialogResult dialogResult)
            {
                RequestClose?.Invoke(dialogResult);
            }

            public virtual void OnDialogOpened(IDialogParameters parameters)
            {
                // 可以在这里初始化对话框参数
            }

            public virtual bool CanCloseDialog()
            {
                return true;
            }

            public virtual void OnDialogClosed()
            {
                // 对话框关闭时的清理操作
            }
        }
    
}
