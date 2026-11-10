using Intern.Entities.DTOs.Employee;
using Intern.Service.Services.Abstracts;
using Microsoft.AspNetCore.Mvc;

namespace Intern.API.Controllers
{
    /// <summary>Özel servis metodu, sayfalama ve kodlu hata örneği.</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeeController : BaseController
    {
        private readonly IEmployeeService _employeeService;

        public EmployeeController(IEmployeeService employeeService)
        {
            _employeeService = employeeService;
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetAllPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            return ActionResultInstance(await _employeeService.GetAllPagedAsync(page, pageSize));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            return ActionResultInstance(await _employeeService.GetByIdAsync(id));
        }

        [HttpGet("number/{employeeNumber}")]
        public async Task<IActionResult> GetByEmployeeNumber(string employeeNumber)
        {
            return ActionResultInstance(await _employeeService.GetByEmployeeNumberAsync(employeeNumber));
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] CreateEmployeeDto employeeDto)
        {
            return ActionResultInstance(await _employeeService.AddEntityAsync(employeeDto));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, CreateEmployeeDto employeeDto)
        {
            return ActionResultInstance(await _employeeService.UpdateEntityAsync(id, employeeDto));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            return ActionResultInstance(await _employeeService.SoftDeleteAsync(id));
        }
    }
}
