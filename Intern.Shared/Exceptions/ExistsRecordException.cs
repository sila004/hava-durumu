namespace Intern.Shared.Exceptions
{
    public class ExistsRecordException : Exception
    {
        public ExistsRecordException(string message = "Aynı kayıt zaten var!") : base(message)
        {
        }
    }
}
