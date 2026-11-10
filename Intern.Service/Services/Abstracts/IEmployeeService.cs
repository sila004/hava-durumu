using Intern.Core.Services;
using Intern.Core.Utilities.PagesList;
using Intern.Entities.Concretes;
using Intern.Entities.DTOs.Employee;
using Intern.Shared.DTOs;

namespace Intern.Service.Services.Abstracts
{
    public interface IEmployeeService : IGenericService<Employee, EmployeeDto, CreateEmployeeDto>
    {
        Task<Response<IPagedList<EmployeeDto>>> GetAllPagedAsync(int page, int pageSize);
        Task<Response<EmployeeDto>> GetByEmployeeNumberAsync(string employeeNumber);
    }
}
