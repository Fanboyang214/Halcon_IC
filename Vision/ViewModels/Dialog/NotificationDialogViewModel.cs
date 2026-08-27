using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;

namespace Vision.ViewModels.Dialog
{
    public class NotificationDialogViewModel : BindableBase, IDialogAware
    {
        private string _title = "提示";
        private string _message = "";

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

        public event Action<IDialogResult> RequestClose;

        public DelegateCommand OkCommand { get; }

        public NotificationDialogViewModel()
        {
            OkCommand = new DelegateCommand(OnOk);
        }

        private void OnOk()
        {
            RaiseRequestClose(new DialogResult(ButtonResult.OK));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("title"))
                Title = parameters.GetValue<string>("title") ?? "提示";
            if (parameters.ContainsKey("content"))
                Message = parameters.GetValue<string>("content") ?? "";
        }

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        protected virtual void RaiseRequestClose(IDialogResult dialogResult)
        {
            RequestClose?.Invoke(dialogResult);
        }
    }
}