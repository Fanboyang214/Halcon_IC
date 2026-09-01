using Core.Events;
using Core.Models;
using Prism.Events;
using Prism.Mvvm;

namespace ICApp.ViewModels
{
    public class DeviceStatusViewModel : BindableBase
    {
        private IEventAggregator _eventAggregator;
        private SubscriptionToken _motionStatusToken;
        private SubscriptionToken _visionStatusToken;

        private MotionStatus _motion;

        public MotionStatus Motion
        {
            get => _motion;
            set => SetProperty(ref _motion, value);
        }
        public DeviceStatusViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator??throw new ArgumentNullException();

            _motionStatusToken = _eventAggregator.GetEvent<MotionStatusEvent>().Subscribe(status => Motion = status,ThreadOption.UIThread);
        }

       
    }
}
