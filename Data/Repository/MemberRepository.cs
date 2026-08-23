using Core.Interfaces;
using Data.Entity;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Data.Repository
{
    public class MemberRepository : RepositoryBase, IRepository<Member>
    {
        public MemberRepository(AppDbContext appDbContext) : base(appDbContext) { }

        public void Add(Member entity)
        {
            appDbContext.Members.Add(entity);
        }

        public void AddRange(IEnumerable<Member> entities)
        {
            appDbContext.Members.AddRange(entities);
        }

        public IQueryable<Member> Find(Expression<Func<Member, bool>> predicate)
        {
            return appDbContext.Members.Where(predicate);
        }

        public IQueryable<Member> GetAll()
        {
            return appDbContext.Members;
        }

        public async Task<Member?> GetByIdAsync(long id, CancellationToken ct = default)
        {
            return await appDbContext.Members.FindAsync([id], ct);
        }

        public void Remove(Member entity)
        {
            appDbContext.Members.Remove(entity);
        }

        public void RemoveRange(IEnumerable<Member> entities)
        {
            appDbContext.Members.RemoveRange(entities);
        }

        public int SaveChanges()
        {
            return appDbContext.SaveChanges();
        }
        

        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            return await appDbContext.SaveChangesAsync(ct);
        }

        public void Update(Member entity)
        {
            appDbContext.Members.Update(entity);
        }
    }
}
