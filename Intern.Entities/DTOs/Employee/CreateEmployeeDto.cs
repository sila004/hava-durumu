namespace Intern.Entities.DTOs.Employee
{
    public class CreateEmployeeDto
    {
        public string? EmployeeNumber { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Gender { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime? BirthDate { get; set; }
    }
}
