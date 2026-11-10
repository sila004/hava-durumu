using Intern.Core.Services;
using Intern.Entities.Concretes;
using Intern.Entities.DTOs.Ability;

namespace Intern.Service.Services.Abstracts
{
    public interface IAbilityService : IGenericService<Ability, AbilityDto, CreateAbilityDto>
    {
    }
}