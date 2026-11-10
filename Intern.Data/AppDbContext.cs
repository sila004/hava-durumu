using Intern.Core.Entities.Enums;
using Intern.Entities.Abstracts;
using Intern.Entities.Concretes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Intern.Data
{
    public class AppDbContext : DbContext, IDataProtectionKeyContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<Ability> Abilities { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<WeatherMailAccount> WeatherMailAccounts { get; set; }
        public DbSet<WeatherLoginChallenge> WeatherLoginChallenges { get; set; }
        public DbSet<WeatherMailDelivery> WeatherMailDeliveries { get; set; }
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
        // Yeni tabloları buraya ekleyin

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<WeatherMailAccount>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Email).HasMaxLength(254).IsRequired();
                entity.HasIndex(x => x.Email).IsUnique();
                entity.Property(x => x.City).HasMaxLength(80).IsRequired();
                entity.Property(x => x.UnsubscribeToken).HasMaxLength(64).IsRequired();
                entity.HasIndex(x => x.UnsubscribeToken).IsUnique();
            });
            modelBuilder.Entity<WeatherLoginChallenge>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Email).HasMaxLength(254).IsRequired();
                entity.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
                entity.HasIndex(x => new { x.Email, x.CreatedUtc });
            });
            modelBuilder.Entity<WeatherMailDelivery>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.City).HasMaxLength(80).IsRequired();
                entity.Property(x => x.Kind).HasMaxLength(16).IsRequired();
                entity.Property(x => x.ErrorCode).HasMaxLength(40);
                entity.HasIndex(x => x.AttemptedUtc);
                entity.HasIndex(x => new { x.AccountId, x.AttemptedUtc });
                entity.HasOne<WeatherMailAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            });

            // BaseEntity'den türeyen tüm varlıklar için ortak sütun ayarları
            foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                .Where(t => t.ClrType.IsSubclassOf(typeof(BaseEntity))))
            {
                modelBuilder.Entity(entityType.Name)
                    .Property("CreatedDate").HasDefaultValueSql("CURRENT_TIMESTAMP");
                modelBuilder.Entity(entityType.Name)
                    .Property("ModifiedDate").HasDefaultValueSql("CURRENT_TIMESTAMP");
                modelBuilder.Entity(entityType.Name)
                    .Property("CreatedBy").HasDefaultValue("System");
                var converter = new EnumToStringConverter<Status>();
                modelBuilder.Entity(entityType.Name)
                    .Property(nameof(BaseEntity.Status))
                    .HasConversion(converter)
                    .HasDefaultValue(Status.Active);
            }
        }

        public override int SaveChanges()
        {
            SetAuditFields();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            SetAuditFields();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SetAuditFields();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            SetAuditFields();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        // Eklenen ve değişen kayıtların denetim alanlarını (tarih + kullanıcı) kaydetmeden önce doldurur.
        private void SetAuditFields()
        {
            var currentTime = DateTimeOffset.UtcNow;
            var currentUserId = GetCurrentUserId();

            var entries = ChangeTracker.Entries<BaseEntity>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedDate = currentTime;

                    if (string.IsNullOrEmpty(entry.Entity.CreatedBy))
                    {
                        entry.Entity.CreatedBy = currentUserId;
                    }
                }

                entry.Entity.ModifiedDate = currentTime;
                entry.Entity.ModifiedBy = currentUserId;
            }
        }

        // Giriş yapmış kullanıcının kimliği: NameIdentifier, yoksa Email, yoksa Name; hiçbiri yoksa "System".
        private string GetCurrentUserId()
        {
            try
            {
                var user = _httpContextAccessor?.HttpContext?.User;
                if (user?.Identity?.IsAuthenticated == true)
                {
                    var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(userId))
                    {
                        return userId;
                    }

                    var email = user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                    if (!string.IsNullOrEmpty(email))
                    {
                        return email;
                    }

                    return user.Identity.Name ?? "System";
                }
            }
            catch
            {
                // Kimlik okunamazsa denetim alanı "System" ile dolar; kayıt işlemi bozulmaz.
            }
            return "System";
        }
    }
}
