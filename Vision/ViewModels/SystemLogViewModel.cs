using Core.Events;
using Core.Interfaces;
using Core.Models;
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

        public void Destroy()
        {
            _eventAggregator.GetEvent<LogPubSubEvent>().Unsubscribe(_subscriptionToken);
        }
    }
}
