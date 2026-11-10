using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Intern.Core.Repositories
{
    public interface IGenericRepository<TEntity> where TEntity : class
    {
        Task<TEntity> GetByIdWithIncludes(string id);
        Task<TEntity> GetByIdWithNestedIncludes(string id);
        Task<TEntity> GetByIdDetached(string id);
        Task<IEnumerable<TEntity>> GetAllActiveWithIncludes(Expression<Func<TEntity, bool>> predicate = null);
        Task<IEnumerable<TEntity>> GetAllActiveWithNestedIncludes(Expression<Func<TEntity, bool>> predicate = null);
        Task<IEnumerable<TEntity>> GetAllFilterWithNestedIncludes(string filterField, string filterValue);
        Task<TEntity> GetFirst(
           Expression<Func<TEntity, bool>> predicate = null,
           Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
           Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> include = null,
           bool enableTracking = true,
           bool ignoreQueryFilters = false);
        IQueryable<TEntity> Query();
        IQueryable<TEntity> FilterBy(Expression<Func<TEntity, bool>> predicate);

        Task AddEntity(TEntity entity);
        void HardDelete(TEntity entity);
        void SoftDelete(TEntity entity);
        TEntity UpdateEntity(TEntity entity);
    }
}