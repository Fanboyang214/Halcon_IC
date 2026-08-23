using Core.Events;
using Core.Interfaces;
using Core.Models;
using NLog;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastruce.Services
{

    public class LogService : ILogService
    {
        private IEventAggregator? _eventAggregator;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public LogService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        public void AddLog(string level, string message)
        {
            try
            {
                var logEntry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Message = message
                };
                _eventAggregator?.GetEvent<LogPubSubEvent>().Publish(logEntry);
                // 使用NLog记录日志到文件
                switch (level)
                {
                    case "INFO":
                        _logger.Info(message);
                        break;
                    case "WARN":
                        _logger.Warn(message);
                        break;
                    case "ERROR":
                        _logger.Error(message);
                        break;
                    case "DEBUG":
                        _logger.Debug(message);
                        break;
                    default:
                        _logger.Info(message);
                        break;
                }
            }
            catch (Exception ex)
            {
                // 处理日志记录异常
                _logger.Error(ex, "Failed to add log entry.");
            }

        }

    }
}

