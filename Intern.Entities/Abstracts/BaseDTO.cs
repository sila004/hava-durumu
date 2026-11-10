using Intern.Core.Entities.Abstracts;
using Intern.Core.Entities.Enums;

namespace Intern.Entities.Abstracts
{
    public abstract class BaseDTO : IDTO
    {
        public string CreatedBy { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public Status? Status { get; set; }
        public DateTimeOffset ModifiedDate { get; set; }
    }
}
