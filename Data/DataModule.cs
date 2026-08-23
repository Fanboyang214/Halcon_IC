using Core.Interfaces;
using Core.Models;
using Data.Entity;
using Data.Repository;
using Data.Services;
using Microsoft.EntityFrameworkCore;
using Prism.Ioc;
using Prism.Modularity;

namespace Data
{
    public class DataModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            using var scope = containerProvider.CreateScope();
            var dbContext = scope.Resolve<AppDbContext>();
            dbContext.Database.EnsureCreated();
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
