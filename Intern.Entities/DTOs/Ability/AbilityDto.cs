using Intern.Entities.Abstracts;

namespace Intern.Entities.DTOs.Ability
{
    public class AbilityDto : BaseDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
