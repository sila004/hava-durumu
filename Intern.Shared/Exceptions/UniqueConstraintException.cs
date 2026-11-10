namespace Intern.Shared.Exceptions
{
    public class UniqueConstraintException : Exception
    {
        public UniqueConstraintException()
        {
        }

        public UniqueConstraintException(string message) : base(message)
        {
        }
    }
}
