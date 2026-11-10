using Intern.Core.Entities.Abstracts;
using Intern.Core.Entities.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Intern.Entities.Abstracts
{
    public abstract class BaseEntity : IEntity
    {
        public DateTimeOffset CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
        public DateTimeOffset ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public Status? Status { get; set; }
        [NotMapped]
        public bool ForceDelete { get; set; } = false;
    }
}
