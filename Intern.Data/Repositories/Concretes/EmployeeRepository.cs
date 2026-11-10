using Intern.Data.Repositories.Abstract;
using Intern.Entities.Concretes;

namespace Intern.Data.Repositories.Concretes
{
    public class EmployeeRepository : GenericRepository<Employee>, IEmployeeRepository
    {
        public EmployeeRepository(AppDbContext context) : base(context)
        {
        }
    }
}
