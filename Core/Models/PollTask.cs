using Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class PollTask : IPollTask, IDisposable
    {
        private readonly Func<Task> _pollAction;
        private readonly int _intervalMs;
        private PeriodicTimer? _timer;
        private CancellationTokenSource? _cts;
        private Task? _runTask;
        private 

        public PollTask(Func<Task> pollAction, int intervalMs)
        {
            _pollAction = pollAction ?? throw new ArgumentNullException(nameof(pollAction));
            _intervalMs = intervalMs;
        }

        public bool IsRunning => _cts is { Token: {IsCancellationRequested: false } };

        public void StartPoll()
        {
            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_intervalMs));
            _runTask = RunPollLoop(_cts.Token);
        }

        private async Task? RunPollLoop(CancellationToken token)
        {
            try
            {
                while (await _timer!.WaitForNextTickAsync(token))
                {
                    try
                    {
                        await _pollAction.Invoke();
                    }
                    catch (Exception ex)
                    {
                        // Log the exception or handle it as needed
                        throw new Exception($"轮询任务执行异常: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {

            }
        }

        public void StopPoll()
        {
           _cts?.Cancel();
           _timer?.Dispose();
            try
            {
               _runTask?.Wait(1000);
            }
            catch(Exception e)
            {
                throw new Exception($"轮询任务停止超时:{e.Message}");
            }
            _cts?.Dispose();
            _runTask?.Dispose();
            _runTask = null;
            _cts = null;
            _timer = null;
        }

        public void Dispose()
        {
            StopPoll();
        }
    }
}
