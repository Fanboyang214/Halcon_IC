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
        private SubscriptionToken _solenoidStatusToken;

        private MotionStatus _motion;
        private VisionStatus _vision;
        private SensorTriggeredPayload _sensorStatus;
        private SolenoidStatus _solenoidStatus;

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

        public SolenoidStatus Solenoid
        {
            get { return _solenoidStatus; }
            set
            {
                SetProperty(ref _solenoidStatus, value);
                RaisePropertyChanged(nameof(Solenoid));
            }
        }


        public DeviceStatusViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator??throw new ArgumentNullException();

            Motion = new MotionStatus();
            Vision = new VisionStatus();
            SensorStatus = new SensorTriggeredPayload();
            Solenoid = new SolenoidStatus();

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
                SensorStatus.SensorStatue = status.SensorStatue;
            },ThreadOption.UIThread);

            _solenoidStatusToken = _eventAggregator.GetEvent<SolenoidStatusEvent>().Subscribe(status =>
            {
                Solenoid.solenoidStatus = status.solenoidStatus;
            }, ThreadOption.UIThread);
        }

        public void Destroy()
        {
            _eventAggregator.GetEvent<MotionStatusEvent>().Unsubscribe(_motionStatusToken);
            _eventAggregator.GetEvent<VisionStatusEvent>().Unsubscribe(_visionStatusToken);
            _eventAggregator.GetEvent<SensorTriggeredEvent>().Unsubscribe(_sensorStatusToken);
            _eventAggregator.GetEvent<SolenoidStatusEvent>().Unsubscribe(_solenoidStatusToken);
            _motionStatusToken?.Dispose();
            _visionStatusToken?.Dispose();
            _sensorStatusToken?.Dispose();
            _solenoidStatusToken?.Dispose();
            _motionStatusToken = null;
            _visionStatusToken = null;
            _sensorStatusToken = null;
            _solenoidStatusToken = null;
        }
    }
}
