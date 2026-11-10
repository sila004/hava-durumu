using Intern.Data.Repositories.Abstract;
using Intern.Entities.Concretes;

namespace Intern.Data.Repositories.Concretes
{
    public class AbilityRepository : GenericRepository<Ability>, IAbilityRepository
    {
        public AbilityRepository(AppDbContext context) : base(context)
        {
        }
    }
}