using System;

namespace Data.Repository
{
    /// <summary>
    /// 仓储基类，封装 <see cref="AppDbContext"/> 注入与 <see cref="IDisposable"/> 模式。
    /// </summary>
    /// <remarks>
    /// 修复说明：
    ///  原实现无构造函数注入 appDbContext，子类直接访问 null 引用导致 NRE；
    ///  原 Dispose 还有 `if (!_disposed) return;` 反向逻辑 bug，已修。
    /// </remarks>
    public abstract class RepositoryBase : IDisposable
    {
        protected readonly AppDbContext appDbContext;
        private bool _disposed;

        protected RepositoryBase(AppDbContext appDbContext)
        {
            this.appDbContext = appDbContext ?? throw new ArgumentNullException(nameof(appDbContext));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                appDbContext.Dispose();
            }
            _disposed = true;
        }
    }
}
