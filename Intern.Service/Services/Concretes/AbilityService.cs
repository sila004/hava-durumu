using Intern.Core.UnitOfWork;
using Intern.Data.Repositories.Abstract;
using Intern.Entities.Concretes;
using Intern.Entities.DTOs.Ability;
using Intern.Service.Services.Abstracts;

namespace Intern.Service.Services.Concretes
{
    public class AbilityService : GenericService<Ability, AbilityDto, CreateAbilityDto>, IAbilityService
    {
        public AbilityService(IUnitOfWork unitOfWork, IAbilityRepository repository)
            : base(unitOfWork, repository)
        {
        }
    }
}
