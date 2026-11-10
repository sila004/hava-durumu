using Intern.Core.Entities.Enums;
using Intern.Core.UnitOfWork;
using Intern.Core.Utilities.PagesList;
using Intern.Data.Repositories.Abstract;
using Intern.Entities.Concretes;
using Intern.Entities.DTOs.Employee;
using Intern.Service.AutoMapper;
using Intern.Service.Services.Abstracts;
using Intern.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Intern.Service.Services.Concretes
{
    /// <summary>
    /// Özel servis metodu, sayfalama ve kodlu hata örneği.
    /// Beklenen iş hataları Response.Fail ile döner; beklenmeyen hata try/catch'e takılmaz,
    /// GlobalExceptionHandlingMiddleware'e düşer.
    /// </summary>
    public class EmployeeService : GenericService<Employee, EmployeeDto, CreateEmployeeDto>, IEmployeeService
    {
        public EmployeeService(IUnitOfWork unitOfWork, IEmployeeRepository repository)
            : base(unitOfWork, repository)
        {
        }

        public async Task<Response<IPagedList<EmployeeDto>>> GetAllPagedAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _genericRepository.Query()
                .Where(x => x.Status == Status.Active)
                .AsNoTracking();

            var totalCount = await query.CountAsync();

            var employees = await query
                .OrderBy(x => x.EmployeeNumber)
                .ThenBy(x => x.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = ObjectMapper.Mapper.Map<List<EmployeeDto>>(employees);

            var pagedList = new PagedList<EmployeeDto>(
                pageNumber: page,
                pageSize: pageSize,
                totalCount: totalCount,
                totalPages: (int)Math.Ceiling(totalCount / (double)pageSize),
                items: items);

            return Response<IPagedList<EmployeeDto>>.Success(pagedList, 200);
        }

        public async Task<Response<EmployeeDto>> GetByEmployeeNumberAsync(string employeeNumber)
        {
            var employee = await _genericRepository.GetFirst(
                predicate: x => x.EmployeeNumber == employeeNumber && x.Status == Status.Active);

            if (employee == null)
                return Response<EmployeeDto>.Fail("EmployeeNotFound", "Çalışan bulunamadı.", 404, true);

            return Response<EmployeeDto>.Success(ObjectMapper.Mapper.Map<EmployeeDto>(employee), 200);
        }

        // Kodlu hata örneği: istemci error.code değerine göre davranabilir.
        public override async Task<Response<EmployeeDto>> AddEntityAsync(CreateEmployeeDto dto)
        {
            if (!string.IsNullOrEmpty(dto.EmployeeNumber))
            {
                var exists = await _genericRepository.Query()
                    .AnyAsync(x => x.EmployeeNumber == dto.EmployeeNumber && x.Status == Status.Active);

                if (exists)
                    return Response<EmployeeDto>.Fail("EmployeeNumberExists", "Bu sicil numarası zaten kullanılıyor.", 409, true);
            }

            return await base.AddEntityAsync(dto);
        }
    }
}
