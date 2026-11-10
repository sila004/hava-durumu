using Intern.Entities.Abstracts;

namespace Intern.Entities.DTOs.Employee
{
    public class EmployeeDto : BaseDTO
    {
        public string Id { get; set; } = string.Empty;
        public string? EmployeeNumber { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Gender { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime? BirthDate { get; set; }
    }
}
