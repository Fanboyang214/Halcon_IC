using Core.Events;
using Core.Interfaces;
using Core.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Services.Dialogs;
using System;
using System.Collections.Concurrent;
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
        private readonly ISolenoidValueService _solenoid;
        private readonly IConfigService _config;

        private readonly IPollTask _sensorPollTask;

        // 检测结果队列（Vision 模块发布 → Motion 消费）
        private readonly ConcurrentQueue<DetectionResult> _resultQueue = new ConcurrentQueue<DetectionResult>();

        // 传感器上次状态，用于上升沿检测
        private bool _lastSensorState;

        // 订阅令牌
        private Prism.Events.SubscriptionToken _detectionResultToken;

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
            ISolenoidValueService solenoid,
            IConfigService config,
            IPollTaskFactory pollTaskFactory)
        {
            _motionControlService = motionControlService ?? throw new ArgumentNullException(nameof(motionControlService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _sensor = sensor ?? throw new ArgumentNullException(nameof(sensor));
            _solenoid = solenoid ?? throw new ArgumentNullException(nameof(solenoid));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _ = pollTaskFactory ?? throw new ArgumentNullException(nameof(pollTaskFactory));

            ConnectCommand = new DelegateCommand(OnConnectCommand);
            SevonCommand = new DelegateCommand(OnSevonCommand);
            VmoveCommand = new DelegateCommand(OnVmoveCommand);
            VstopCommand = new DelegateCommand(OnVstopCommand);
            ChangeSpeedCommand = new DelegateCommand(OnChangeSpeedCommand);

            // 订阅 Vision 模块发布的检测结果事件
            _detectionResultToken = _eventAggregator.GetEvent<DetectionResultEvent>()
                .Subscribe(OnDetectionResultReceived, ThreadOption.BackgroundThread);

            // 启动传感器轮询：每 50ms 检测一次产品到位信号
            _sensorPollTask = pollTaskFactory.CreatePollTask(OnSensorPollAsync, 50);
            _sensorPollTask.StartPoll();
        }

        /// <summary>
        /// 传感器轮询回调（后台线程）。
        /// 检测到上升沿（传感器从无产品→有产品）时，出队检测结果并判断是否执行剔除。
        /// </summary>
        private async Task OnSensorPollAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    bool currentState = _sensor.ReadSensorState();

                    // 上升沿检测：产品到达传感器
                    if (currentState && !_lastSensorState)
                    {
                        _lastSensorState = true;
                        ProcessSensorTrigger();
                        _eventAggregator.GetEvent<SensorTriggeredEvent>().Publish(new SensorTriggeredPayload { SensorStatue = 0 });
                    }
                    else if (!currentState)
                    {
                        _lastSensorState = false;
                        _eventAggregator.GetEvent<SensorTriggeredEvent>().Publish(new SensorTriggeredPayload { SensorStatue = 1 });

                    }
                }
                catch
                {
                    // 传感器读取失败时静默处理
                }
            });
        }

        /// <summary>
        /// 传感器触发处理：出队检测结果，若 ShouldTrigger && !IsOK 则执行剔除。
        /// </summary>
        private void ProcessSensorTrigger()
        {
            if (_resultQueue.TryDequeue(out var result))
            {
                AddLog("INFO", $"传感器触发，结果队列出队: {result.ResultText}, ShouldTrigger={result.ShouldTrigger}, IsOK={result.IsOK}");

                if (result.ShouldTrigger && !result.IsOK)
                {
                    AddLog("WARN", "检测结果 NG，执行电磁阀剔除");
                    TriggerRejectAsync();
                }
            }
            else
            {
                // 队列空：默认判 NG 并剔除（安全策略）
                AddLog("WARN", "传感器触发但结果队列为空，按安全策略执行剔除");
                TriggerRejectAsync();
            }
        }

        /// <summary>
        /// 接收 Vision 模块发布的检测结果并存入队列。
        /// </summary>
        private void OnDetectionResultReceived(DetectionResult result)
        {
            _resultQueue.Enqueue(result);
            AddLog("INFO", $"检测结果入队: {result.ResultText}, 队列长度={_resultQueue.Count}");

            // 确保队列不堆积，只保留最新的结果
            while (_resultQueue.Count > 1)
            {
                _resultQueue.TryDequeue(out _);
            }
        }

        /// <summary>
        /// 执行电磁阀剔除：根据传送带速度和 Camera→Solenoid 距离计算延时，打开电磁阀后自动关闭。
        /// </summary>
        private async void TriggerRejectAsync()
        {
            try
            {
                // 从配置读取传送带速度和 Camera→Solenoid 距离
                double beltSpeed = _config.Motion?.BeltSpeedMmPerSec ?? 200.0;
                double distance = _config.Motion?.CameraToRejectMm ?? 350.0;
                double rejectDuration = _config.Motion?.RejectDurationMs ?? 40.0;

                // 计算产品从传感器到电磁阀所需时间（ms）
                int delayMs = distance > 0 && beltSpeed > 0
                    ? (int)(distance / beltSpeed * 1000)
                    : 1750; // 默认 1.75s

                AddLog("INFO", $"剔除延时计算: 距离={distance}mm, 速度={beltSpeed}mm/s, 延时={delayMs}ms");

                await Task.Delay(delayMs);

                _solenoid.OpenValue();
                AddLog("INFO", $"电磁阀打开，持续 {rejectDuration}ms");
                _eventAggregator.GetEvent<SolenoidStatusEvent>().Publish(new SolenoidStatus { solenoidStatus = 0 });

                await Task.Delay((int)rejectDuration);

                _solenoid.CloseValue();
                AddLog("INFO", "电磁阀关闭，剔除完成");
                _eventAggregator.GetEvent<SolenoidStatusEvent>().Publish(new SolenoidStatus { solenoidStatus = 1 });

            }
            catch (Exception ex)
            {
                AddLog("ERROR", $"剔除执行异常: {ex.Message}");
            }
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
            _detectionResultToken?.Dispose();
            _solenoid?.Dispose();
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