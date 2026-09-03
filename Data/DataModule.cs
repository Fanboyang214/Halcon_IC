using Core.Events;
using Core.Interfaces;
using Core.Models;
using Data.Entity;
using Data.Repository;
using Data.Services;
using Microsoft.EntityFrameworkCore;
using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;

namespace Data
{
    public class DataModule : IModule
    {
        private SubscriptionToken? _detectionResultToken;

        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 1. 确保数据库已创建
            using var scope = containerProvider.CreateScope();
            var dbContext = scope.Resolve<AppDbContext>();
            dbContext.Database.EnsureCreated();

            // 2. 订阅检测结果事件，自动入库
            var eventAggregator = containerProvider.Resolve<IEventAggregator>();
            var statsService = containerProvider.Resolve<IStatisticsService>();
            _detectionResultToken = eventAggregator.GetEvent<DetectionResultEvent>()
                .Subscribe(async result =>
                {
                    try
                    {
                        await statsService.RecordAsync(result, result.TemplateName);
                    }
                    catch
                    {
                        // RecordAsync 内部已处理 M1 异常，此处兜底
                    }
                }, ThreadOption.BackgroundThread);
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterScoped<AppDbContext>(sp=>
            {
                var cfg = sp.Resolve<Core.Interfaces.IConfigService>();
               var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlServer(cfg.GetConnectionString())
                    .Options;
                return new AppDbContext(options);
            });
            containerRegistry.RegisterSingleton<IRepository<ProductInspectionRecord>, ProductInspectionRecordRepository>();
            containerRegistry.RegisterSingleton<IRepository<Member>, MemberRepository>();
            containerRegistry.RegisterSingleton<IStatisticsService, StatisticsService>();
        }
    }
}
