namespace Intern.Core.UnitOfWork
{
    public interface IUnitOfWork
    {
        Task CommitAsync();

        void Commit();

        Task BeginTransactionAsync();

        Task CommitTransactionAsync();

        Task RollbackAsync();

        // Hata sonrası takipte kalan kaydedilmemiş varlıkları bırakır; sonraki kayda sızmaz.
        void ClearChangeTracker();
    }
}
