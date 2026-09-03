using Core.Events;
using Core.Interfaces;
using Core.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Services.Dialogs;
using System;
using System.Threading.Tasks;

namespace Motion.ViewModels
{
    public class MotionControlViewModel : BindableBase, IDestructible
    {
        private readonly IMotionControlService _motionControlService;
        private readonly IDialogService _dialogService;
        private readonly ILogService _logService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ISensorService _sensor;

        private readonly IPollTask _sensorPollTask;
        

        private string _speed = "3000";
        private MotionStatus _status = new MotionStatus();

        #region 命令
        public DelegateCommand ConnectCommand { get; }
        public DelegateCommand SevonCommand { get; }
        public DelegateCommand VmoveCommand { get; }
        public DelegateCommand VstopCommand { get; }
        public DelegateCommand ChangeSpeedCommand { get; }
        #endregion

        public string Speed
        {
            get => _speed;
            set => SetProperty(ref _speed, value);
        }

        public MotionControlViewModel(
            IMotionControlService motionControlService,
            IDialogService dialogService,
            ILogService logService,
            IEventAggregator eventAggregator,
            ISensorService sensor,
            IPollTaskFactory pollTaskFactory)
        {
            _motionControlService = motionControlService ?? throw new ArgumentNullException(nameof(motionControlService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _sensor = sensor ?? throw new ArgumentNullException(nameof(sensor));
            _ = pollTaskFactory ?? throw new ArgumentNullException(nameof(pollTaskFactory));

            ConnectCommand = new DelegateCommand(OnConnectCommand);
            SevonCommand = new DelegateCommand(OnSevonCommand);
            VmoveCommand = new DelegateCommand(OnVmoveCommand);
            VstopCommand = new DelegateCommand(OnVstopCommand);
            ChangeSpeedCommand = new DelegateCommand(OnChangeSpeedCommand);

            // 启动传感器轮询：每 50ms 检测一次产品到位信号
            _sensorPollTask = pollTaskFactory.CreatePollTask(OnSensorPollAsync, 50);
            _sensorPollTask.StartPoll();
        }

        /// <summary>
        /// 传感器轮询回调（后台线程）。
        /// 检测到上升沿（无产品 → 有产品）时发布 SensorTriggeredEvent。
        /// </summary>
        private async Task OnSensorPollAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    bool currentState = _sensor.ReadSensorState();
                    double position = _sensor.ReadSensorPosition();

                    
                        _eventAggregator.GetEvent<SensorTriggeredEvent>().Publish(
                            new SensorTriggeredPayload { TriggerTime = DateTime.Now,SensorStatue = currentState?0:1 , ConveyorPosition = position});
                    

                   
                }
                catch
                {
                    // 传感器读取失败时静默处理
                }
            });
        }

        private async void OnChangeSpeedCommand()
        {
            if (double.TryParse(Speed, out var speedValue))
            {
                AddLog("INFO", $"调整传送带速度,目标速度:{speedValue}");
                bool isSuccess = await Task.Run(() =>
                {
                    try
                    {
                        return _motionControlService.ChangeSpeed(speedValue);
                    }
                    catch
                    {
                        return false;
                    }
                });
                if (isSuccess)
                {
                    AddLog("INFO", $"传送带速度调整成功!当前速度：{speedValue},请重启传送带以使其生效");
                    _status.Speed = speedValue.ToString();
                }
                else
                {
                    AddLog("ERROR", $"传送带速度调整失败!错误码:{_motionControlService.LastErrorCode}");
                    await ShowErrorDialogAsync($"传送带速度调整失败!错误码:{_motionControlService.LastErrorCode}");
                }
                _eventAggregator.GetEvent<MotionStatusEvent>().Publish(_status);
            }
            else
            {
                AddLog("WARN", "传送带速度调整失败!,传送带数值不合法");
                await ShowWarningDialogAsync("请输入合法的传送带速度数值!");
            }
        }

        private async void OnVstopCommand()
        {
            AddLog("INFO", "停止传送带运动...");
            bool isSuccess = await Task.Run(() => _motionControlService.Vstop());
            _status.MoveStatus = !isSuccess ? 0 : 1;
            _eventAggregator.GetEvent<MotionStatusEvent>().Publish(_status);
            AddLog("INFO", $"传送带已停止，执行结果: {isSuccess}");
        }

        private async void OnVmoveCommand()
        {
            AddLog("INFO", "启动传送带运动...");
            bool isSuccess = await Task.Run(() => _motionControlService.Vmove());
            _status.MoveStatus = isSuccess ? 0 : 1;
            _eventAggregator.GetEvent<MotionStatusEvent>().Publish(_status);
            AddLog("INFO", $"传送带已启动运动，执行结果: {isSuccess}");
        }

        private async void OnSevonCommand()
        {
            AddLog("INFO", "尝试电机使能操作...");
            bool isSuccess = await Task.Run(() => _motionControlService.Sevon());
            if (isSuccess)
            {
                AddLog("INFO", "电机使能成功,传送带准备就绪");
                _status.SevonStatus = 0;
            }
            else
            {
                _status.SevonStatus = 1;
                AddLog("ERROR", "点击使能失败，请检查运动控制卡");
                await ShowErrorDialogAsync("点击使能失败，请检查运动控制卡");
            }
            _eventAggregator.GetEvent<MotionStatusEvent>().Publish(_status);
        }

        private async void OnConnectCommand()
        {
            AddLog("INFO", "正在连接运动控制卡...");
            try
            {
                bool isSuccess = await Task.Run(() =>
                {
                    try
                    {
                        return _motionControlService.Connect();
                    }
                    catch
                    {
                        return false;
                    }
                });

                if (isSuccess)
                {
                    _status.ConnectStatus = 0;
                    AddLog("INFO", "运动控制卡连接成功!");
                }
                else
                {
                    _status.ConnectStatus = 1;
                    AddLog("ERROR", "运动控制卡连接失败，请检查：1.控制卡电源 2.网线连接 3.IP地址(192.168.5.11)");
                    await ShowErrorDialogAsync("运动控制卡连接失败！\\n\\n请检查：\\n1. 控制卡是否上电\\n2. 网线是否连接\\n3. IP地址是否为192.168.5.11\\n4. 本机IP是否在同一网段\", \"连接失败");
                }
            }
            catch (Exception ex)
            {
                _status.ConnectStatus = 2;
                AddLog("ERROR", $"运动控制卡连接异常：{ex.Message}");
                await ShowErrorDialogAsync($"运动控制卡连接异常：{ex.Message}");
            }
            finally
            {
                _eventAggregator.GetEvent<MotionStatusEvent>().Publish(_status);
            }
        }

        public void Destroy()
        {
            _sensorPollTask?.StopPoll();
        }

        private Task ShowInfoDialogAsync(string message, string title = "提示")
        {
            var tcs = new TaskCompletionSource<bool>();
            var parameters = new DialogParameters
            {
                { "title", title },
                { "content", message },
            };
            _dialogService.ShowDialog(
                "NotificationDialogView",
                parameters,
                result => tcs.SetResult(true));
            return tcs.Task;
        }

        private Task ShowErrorDialogAsync(string message, string title = "错误")
        {
            var tcs = new TaskCompletionSource<bool>();
            var parameters = new DialogParameters
            {
                { "title", title },
                { "content", message }
            };
            _dialogService.ShowDialog(
                "NotificationDialogView",
                parameters,
                result => tcs.SetResult(true));
            return tcs.Task;
        }

        private Task ShowWarningDialogAsync(string message, string title = "警告")
        {
            var tcs = new TaskCompletionSource<bool>();
            var parameters = new DialogParameters
            {
                { "title", title },
                { "content", message }
            };
            _dialogService.ShowDialog(
                "NotificationDialogView",
                parameters,
                result => tcs.SetResult(true));
            return tcs.Task;
        }

        public void AddLog(string level, string message)
        {
            _logService.AddLog(level, message);
        }
    }
}