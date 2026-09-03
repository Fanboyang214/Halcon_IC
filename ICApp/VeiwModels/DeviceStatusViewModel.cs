using Core.Events;
using Core.Models;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation;

namespace ICApp.ViewModels
{
    public class DeviceStatusViewModel : BindableBase,IDestructible
    {
        private IEventAggregator _eventAggregator;
        private SubscriptionToken _motionStatusToken;
        private SubscriptionToken _visionStatusToken;
        private SubscriptionToken _sensorStatusToken;

        private MotionStatus _motion;
        private VisionStatus _vision;
        private SensorTriggeredPayload _sensorStatus;

        public MotionStatus Motion
        {
            get => _motion;
            set
            {
                SetProperty(ref _motion, value);
                RaisePropertyChanged(nameof(Motion));
            }
        }

        public VisionStatus Vision
        {
            get => _vision;
            set
            {
                SetProperty(ref _vision, value);
                RaisePropertyChanged(nameof(Vision));
            }
        }

        public SensorTriggeredPayload SensorStatus
        {
            get => _sensorStatus;
            set
            {
                SetProperty(ref _sensorStatus, value);
                RaisePropertyChanged(nameof(SensorStatus));
            }
        }
        public DeviceStatusViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator??throw new ArgumentNullException();

            Motion = new MotionStatus();
            Vision = new VisionStatus();

            _motionStatusToken = _eventAggregator.GetEvent<MotionStatusEvent>().Subscribe(status =>
            {
                Motion.ConnectStatus = status.ConnectStatus;
                Motion.SevonStatus = status.SevonStatus;
                Motion.MoveStatus = status.MoveStatus;
               
            }, ThreadOption.UIThread);
            _visionStatusToken = _eventAggregator.GetEvent<VisionStatusEvent>().Subscribe(status =>
            {
                Vision.CameraStatus = status.CameraStatus;
                Vision.TemplateStatus = status.TemplateStatus;
                Vision.DetectionStatus = status.DetectionStatus;
                
            }, ThreadOption.UIThread);

            _sensorStatusToken = _eventAggregator.GetEvent<SensorTriggeredEvent>().Subscribe(status =>
            {
                SensorStatus.TriggerTime = status.TriggerTime;
                SensorStatus.SensorStatue = status.SensorStatue;
                SensorStatus.ConveyorPosition = status.ConveyorPosition;

            }, ThreadOption.UIThread);

        }

        public void Destroy()
        {
            _eventAggregator.GetEvent<MotionStatusEvent>().Unsubscribe(_motionStatusToken);
            _eventAggregator.GetEvent<VisionStatusEvent>().Unsubscribe(_visionStatusToken);
            _motionStatusToken?.Dispose();
            _visionStatusToken?.Dispose();
            _motionStatusToken = null;
            _visionStatusToken = null;
        }
    }
}
