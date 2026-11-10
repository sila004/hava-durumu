using Intern.Core.Entities.Enums;

namespace Intern.Core.Entities.Abstracts
{
    public interface IDTO
    {
        public string CreatedBy { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public Status? Status { get; set; }
        public DateTimeOffset ModifiedDate { get; set; }
    }
}
