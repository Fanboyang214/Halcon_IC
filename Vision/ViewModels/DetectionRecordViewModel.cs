using Core.Events;
using Core.Models;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation;
using System.Collections.ObjectModel;

namespace Vision.ViewModels
{
    public class DetectionRecordViewModel : BindableBase,IDestructible
    {
        private ObservableCollection<DetectionResult> _detectionResults = new ObservableCollection<DetectionResult>();
        private IEventAggregator _eventAggregator;
        private SubscriptionToken _subscriptionToken;
        public ObservableCollection<DetectionResult> DetectionResults
        {
            get=> _detectionResults;
            set
            {
                SetProperty(ref _detectionResults, value);
            }
        }

        public DetectionRecordViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator??throw new ArgumentNullException();
            _subscriptionToken = _eventAggregator.GetEvent<DetectionResultEvent>().Subscribe(detectionResult =>
            {
                DetectionResults.Add(detectionResult);
                if (DetectionResults.Count > 200)
                {
                    DetectionResults.RemoveAt(0);
                }
            }, ThreadOption.UIThread);
        }


        public  void Destroy()
        {
            _eventAggregator.GetEvent<DetectionResultEvent>().Unsubscribe(_subscriptionToken);
            _eventAggregator = null;
            _subscriptionToken?.Dispose();
        }
    }
}
