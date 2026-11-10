using Intern.Core.Entities.Abstracts;
using Intern.Core.Entities.Enums;
using Intern.Core.Repositories;
using Intern.Core.Services;
using Intern.Core.UnitOfWork;
using Intern.Service.AutoMapper;
using Intern.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Intern.Service.Services
{
    public class GenericService<TEntity, TDto, CDto> : IGenericService<TEntity, TDto, CDto>
        where TEntity : class, IEntity, new()
        where TDto : class, new()
        where CDto : class, new()
    {
        private readonly IUnitOfWork _unitOfWork;
        protected readonly IGenericRepository<TEntity> _genericRepository;

        public GenericService(IUnitOfWork unitOfWork, IGenericRepository<TEntity> genericRepository)
        {
            _unitOfWork = unitOfWork;
            _genericRepository = genericRepository;
        }

        public virtual async Task<Response<TDto>> AddEntityAsync(CDto dto)
        {
            var newEntity = ObjectMapper.Mapper.Map<TEntity>(dto);
            await _genericRepository.AddEntity(newEntity);
            await _unitOfWork.CommitAsync();
            var newDto = ObjectMapper.Mapper.Map<TDto>(newEntity);
            return Response<TDto>.Success(newDto, 200);
        }

        public virtual async Task<Response<TDto>> UpdateEntityAsync(string id, CDto dto)
        {
            var isExistEntity = await _genericRepository.GetByIdDetached(id);

            if (isExistEntity == null)
            {
                return Response<TDto>.Fail("Id not found", 404, true);
            }

            // Mevcut entity üzerine gelen dto'yu mapliyoruz
            ObjectMapper.Mapper.Map(dto, isExistEntity);

            _genericRepository.UpdateEntity(isExistEntity);
            await _unitOfWork.CommitAsync();

            // Güncellenmiş entity'yi tekrar dto'ya çevirip dönüyoruz
            var updatedDto = ObjectMapper.Mapper.Map<TDto>(isExistEntity);
            return Response<TDto>.Success(updatedDto, 200);
        }

        public virtual async Task<Response<IEnumerable<TDto>>> GetAllActiveWithIncludesAsync()
        {
            var products = ObjectMapper.Mapper.Map<List<TDto>>(await _genericRepository.GetAllActiveWithIncludes());

            return Response<IEnumerable<TDto>>.Success(products, 200);
        }
        public async Task<Response<IEnumerable<TDto>>> GetAllActiveWithNestedIncludesAsync()
        {
            var products = ObjectMapper.Mapper.Map<List<TDto>>(await _genericRepository.GetAllActiveWithNestedIncludes());

            return Response<IEnumerable<TDto>>.Success(products, 200);
        }
        public async Task<Response<IEnumerable<TDto>>> GetAllFilterWithNestedIncludesAsync(string filterField, string filterValue)
        {
            var products = ObjectMapper.Mapper.Map<List<TDto>>(await _genericRepository.GetAllFilterWithNestedIncludes(filterField, filterValue));

            return Response<IEnumerable<TDto>>.Success(products, 200);
        }

        public async Task<Response<TDto>> GetByIdAsync(string id)
        {
            var newDto = ObjectMapper.Mapper.Map<TDto>(await _genericRepository.GetByIdWithIncludes(id));

            if (newDto == null)
            {
                return Response<TDto>.Fail("Id not found", 404, true);
            }

            return Response<TDto>.Success(newDto, 200);
        }
        public async Task<Response<TDto>> GetByIdWithExtraIncludeAsync(string id)
        {
            var newDto = ObjectMapper.Mapper.Map<TDto>(await _genericRepository.GetByIdWithNestedIncludes(id));

            if (newDto == null)
            {
                return Response<TDto>.Fail("Id not found", 404, true);
            }

            return Response<TDto>.Success(newDto, 200);
        }
        public async Task<Response<NoDataDto>> HardDeleteAsync(string id)
        {
            var isExistEntity = await _genericRepository.GetByIdDetached(id);
            if (isExistEntity == null)
            {
                return Response<NoDataDto>.Fail("Id not found", 404, true);
            }

            _genericRepository.HardDelete(isExistEntity);
            await _unitOfWork.CommitAsync();

            return Response<NoDataDto>.Success(204);
        }

        public virtual async Task<Response<NoDataDto>> SoftDeleteAsync(string id)
        {
            var isExistEntity = await _genericRepository.GetByIdDetached(id);
            if (isExistEntity == null)
            {
                return Response<NoDataDto>.Fail("Id not found", 404, true);
            }

            _genericRepository.SoftDelete(isExistEntity);
            await _unitOfWork.CommitAsync();

            return Response<NoDataDto>.Success(204);
        }

        public async Task<Response<IEnumerable<TDto>>> Where(Expression<Func<TEntity, bool>> predicate)
        {
            var list = _genericRepository.FilterBy(predicate);

            var listDto = ObjectMapper.Mapper.Map<IEnumerable<TDto>>(await list.ToListAsync());

            return Response<IEnumerable<TDto>>.Success(listDto, 200);
        }
    }
}