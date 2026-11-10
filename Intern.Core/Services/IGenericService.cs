using Intern.Shared.DTOs;
using System.Linq.Expressions;

namespace Intern.Core.Services
{
    public interface IGenericService<TEntity, TDto, CDto> 
        where TEntity: class 
        where TDto: class
        where CDto: class
    {
        Task<Response<TDto>> GetByIdAsync(string id);
        Task<Response<TDto>> GetByIdWithExtraIncludeAsync(string id);
        Task<Response<IEnumerable<TDto>>> GetAllActiveWithIncludesAsync();
        Task<Response<IEnumerable<TDto>>> GetAllActiveWithNestedIncludesAsync();
        Task<Response<IEnumerable<TDto>>> GetAllFilterWithNestedIncludesAsync(string filterField, string filterValue);
        Task<Response<IEnumerable<TDto>>> Where(Expression<Func<TEntity, bool>> predicate);
        Task<Response<TDto>> AddEntityAsync(CDto dto);
        Task<Response<NoDataDto>> HardDeleteAsync(string id);
        Task<Response<NoDataDto>> SoftDeleteAsync(string id);
        Task<Response<TDto>> UpdateEntityAsync(string id, CDto dto);
    }
}
