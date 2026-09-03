using Core.Events;
using Core.Interfaces;
using Core.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Vision.ViewModels
{
    public class SystemLogViewModel : BindableBase,IDestructible
    {
        private IEventAggregator _eventAggregator;
        private SubscriptionToken _subscriptionToken;
        private ObservableCollection<LogEntry> _logRecords = new ObservableCollection<LogEntry>();
        private bool _isAutoScroll = true;

        public DelegateCommand LogClearCommand { get; }

        public bool IsAutoScroll
        {
            get => _isAutoScroll;
            set
            {
                SetProperty(ref _isAutoScroll, value);
            }
        }

        public ObservableCollection<LogEntry> LogRecords
        {
            get => _logRecords;
            set
            {

                SetProperty(ref _logRecords, value);
            }
        }
        public SystemLogViewModel(IEventAggregator eventAggregator)
        {

            LogClearCommand = new DelegateCommand(OnLogClearCommond, ()=>true);

            _eventAggregator = eventAggregator;

            _subscriptionToken =  _eventAggregator.GetEvent<LogPubSubEvent>().Subscribe(logItem =>
            {
                LogRecords.Add(logItem);

                if(LogRecords.Count > 200)
                {
                    LogRecords.RemoveAt(0);
                }
            },ThreadOption.UIThread);

            
        }

        private void OnLogClearCommond()
        {
            LogRecords.Clear();
            LogRecords = new ObservableCollection<LogEntry>();
        }

        public void Destroy()
        {
            _eventAggregator.GetEvent<LogPubSubEvent>().Unsubscribe(_subscriptionToken);
            _subscriptionToken?.Dispose();
            _subscriptionToken = null;
        }
    }
}
