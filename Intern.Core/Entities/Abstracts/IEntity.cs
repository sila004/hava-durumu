using Intern.Core.Entities.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Intern.Core.Entities.Abstracts
{
    public interface IEntity
    {
        public string CreatedBy { get; set; }
        public string ModifiedBy { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset ModifiedDate { get; set; }
        public Status? Status { get; set; }
        [NotMapped]
        public bool ForceDelete { get; set; }
    }
}
