using System.ComponentModel;

namespace Intern.Shared.Constants
{
    public enum Roles
    {
        [Description("SuperAdmin")]
        SUPER_ADMIN,
        [Description("Admin")]
        ADMIN,
        [Description("Editor")]
        EDITOR,
        [Description("ApiUser")]
        APIUSER,

    }
}
