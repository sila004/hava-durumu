using Intern.Core.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Intern.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly DbContext _context;

        // Açık bir transaction varken çağrılan BeginTransactionAsync sayısı. Birden fazla servisi
        // TEK işlemde çalıştıran akış dış transaction'ı açar; içerideki servisler kendi
        // Begin/Commit/Rollback çağrılarında dış işleme KATILIR. Commit ve geri alma kararı dış akışındır.
        private int _joinedDepth;

        public UnitOfWork(AppDbContext appDbContext)
        {
            _context = appDbContext;
        }

        public async Task BeginTransactionAsync()
        {
            if (_context.Database.CurrentTransaction != null)
            {
                _joinedDepth++;
                return;
            }

            await _context.Database.BeginTransactionAsync();
        }

        public void Commit()
        {
            _context.SaveChanges();
        }

        public async Task CommitAsync()
        {
             await _context.SaveChangesAsync();
        }

        public async Task CommitTransactionAsync()
        {
            if (_joinedDepth > 0)
            {
                // Dış transaction'a katılmıştık: yazılanlar kaydedildi, commit dış akışın işi.
                _joinedDepth--;
                await _context.SaveChangesAsync();
                return;
            }

            await _context.Database.CommitTransactionAsync();
        }

        public async Task RollbackAsync()
        {
            if (_joinedDepth > 0)
            {
                // Dış transaction'ı iç servis geri alamaz; hatayı dönen yanıtla bildirir,
                // dış akış hepsini birlikte geri alır.
                _joinedDepth--;
                return;
            }

            await _context.Database.RollbackTransactionAsync();
        }

        public void ClearChangeTracker()
        {
            _context.ChangeTracker.Clear();
        }
    }
}
