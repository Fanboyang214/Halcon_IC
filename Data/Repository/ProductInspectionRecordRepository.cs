using Core.Interfaces;
using Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Data.Repository
{
    public class ProductInspectionRecordRepository : RepositoryBase, IRepository<ProductInspectionRecord>
    {
        public ProductInspectionRecordRepository(AppDbContext appDbContext) : base(appDbContext) { }


        public void Add(ProductInspectionRecord entity)
        {
            appDbContext.ProductInspectionRecords.Add(entity);
        }

        public void AddRange(IEnumerable<ProductInspectionRecord> entities)
        {
            appDbContext.ProductInspectionRecords.AddRange(entities);
        }
        

        public IQueryable<ProductInspectionRecord> Find(Expression<Func<ProductInspectionRecord, bool>> predicate)
        {
            return appDbContext.ProductInspectionRecords.Where(predicate);
        }

        public IQueryable<ProductInspectionRecord> GetAll()
        {
            return appDbContext.ProductInspectionRecords;
        }

        public async Task<ProductInspectionRecord?> GetByIdAsync(long id, CancellationToken ct = default)
        {
            return await appDbContext.ProductInspectionRecords.FindAsync([id],ct);
        }

        public void Remove(ProductInspectionRecord entity)
        {
             appDbContext.ProductInspectionRecords.Remove(entity);
        }

        public void RemoveRange(IEnumerable<ProductInspectionRecord> entities)
        {
             appDbContext.ProductInspectionRecords.RemoveRange(entities);
        }

        public int SaveChanges()
        {
            return appDbContext.SaveChanges();
        }

        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            return await appDbContext.SaveChangesAsync(ct);
        }

        public void Update(ProductInspectionRecord entity)
        {
             appDbContext.ProductInspectionRecords.Update(entity);
        }
    }
}
