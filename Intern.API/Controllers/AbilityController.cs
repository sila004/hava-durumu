using Intern.Entities.DTOs.Ability;
using Intern.Service.Services.Abstracts;
using Microsoft.AspNetCore.Mvc;

namespace Intern.API.Controllers
{
    /// <summary>En basit örnek: GenericService'in hazır CRUD metodları.</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class AbilityController : BaseController
    {
        private readonly IAbilityService _abilityService;

        public AbilityController(IAbilityService abilityService)
        {
            _abilityService = abilityService;
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            return ActionResultInstance(await _abilityService.GetAllActiveWithIncludesAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            return ActionResultInstance(await _abilityService.GetByIdAsync(id));
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateAbilityDto ability)
        {
            return ActionResultInstance(await _abilityService.AddEntityAsync(ability));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, CreateAbilityDto ability)
        {
            return ActionResultInstance(await _abilityService.UpdateEntityAsync(id, ability));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            return ActionResultInstance(await _abilityService.SoftDeleteAsync(id));
        }
    }
}
