using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;

namespace Vision.ViewModels.Dialog
{
    public class ConfirmationDialogViewModel : BindableBase, IDialogAware
    {
        private string _title = "确认";
        private string _message = "";
        private string _confirmText = "确定";
        private string _cancelText = "取消";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public string ConfirmText
        {
            get => _confirmText;
            set => SetProperty(ref _confirmText, value);
        }

        public string CancelText
        {
            get => _cancelText;
            set => SetProperty(ref _cancelText, value);
        }

        public event Action<IDialogResult> RequestClose;

        public DelegateCommand ConfirmCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public ConfirmationDialogViewModel()
        {
            ConfirmCommand = new DelegateCommand(OnConfirm);
            CancelCommand = new DelegateCommand(OnCancel);
        }

        private void OnConfirm()
        {
            var parameters = new DialogParameters
            {
                { "Confirmed", true }
            };
            RaiseRequestClose(new DialogResult(ButtonResult.OK, parameters));
        }

        private void OnCancel()
        {
            RaiseRequestClose(new DialogResult(ButtonResult.Cancel));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("title"))
                Title = parameters.GetValue<string>("title") ?? "确认";
            if (parameters.ContainsKey("message"))
                Message = parameters.GetValue<string>("message") ?? "";
            if (parameters.ContainsKey("confirmText"))
                ConfirmText = parameters.GetValue<string>("confirmText") ?? "确定";
            if (parameters.ContainsKey("cancelText"))
                CancelText = parameters.GetValue<string>("cancelText") ?? "取消";
        }

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        protected virtual void RaiseRequestClose(IDialogResult dialogResult)
        {
            RequestClose?.Invoke(dialogResult);
        }
    }
}