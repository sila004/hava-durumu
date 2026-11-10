namespace Intern.Shared.Exceptions
{
    public class DeletedRecordException : Exception
    {
        public DeletedRecordException() : base("Silinen kayıt üzerinde işlem yapılamaz!")
        {
        }

        public DeletedRecordException(string message) : base(message)
        {
        }
    }
}
