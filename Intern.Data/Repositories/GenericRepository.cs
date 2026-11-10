using Intern.Core.Entities.Abstracts;
using Intern.Core.Entities.Enums;
using Intern.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Intern.Data.Repositories
{
    /// <summary>
    /// Generic repository, tüm varlıklar için standart veritabanı işlemleri sağlar.
    /// </summary>
    public class GenericRepository<TEntity> : IGenericRepository<TEntity> where TEntity : class, IEntity, new()
    {
        private readonly DbContext _context;
        private readonly DbSet<TEntity> _dbSet;

        public GenericRepository(DbContext context)
        {
            _context = context;
            _dbSet = context.Set<TEntity>();
        }

        /// <summary>Şartlara göre ilk veriyi getirir (opsiyonel sıralama, include, tracking ayarları ile).</summary>
        public async Task<TEntity> GetFirst(
            Expression<Func<TEntity, bool>> predicate = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
            Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> include = null,
            bool enableTracking = true,
            bool ignoreQueryFilters = false)
        {
            IQueryable<TEntity> query = _dbSet;
            if (!enableTracking) query = query.AsNoTracking();
            if (include != null) query = include(query);
            if (predicate != null) query = query.Where(predicate);
            if (ignoreQueryFilters) query = query.IgnoreQueryFilters();

            return orderBy != null
                ? await orderBy(query).FirstOrDefaultAsync()
                : await query.FirstOrDefaultAsync();
        }

        /// <summary>Varlıklar üzerinde sorgulama yapmak için IQueryable döner.</summary>
        public IQueryable<TEntity> Query()
        {
            return _dbSet;
        }

        /// <summary>Yeni bir varlık ekler.</summary>
        public async Task AddEntity(TEntity entity)
        {
            await _dbSet.AddAsync(entity);
        }

        /// <summary>Aktif durumda olan tüm verileri navigation property'leriyle birlikte getirir.</summary>
        public async Task<IEnumerable<TEntity>> GetAllActiveWithIncludes(Expression<Func<TEntity, bool>> predicate = null)
        {
            IQueryable<TEntity> query = _dbSet;
            query = query.Where(x => x.Status == Status.Active);

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var entityType = _context.Model.FindEntityType(typeof(TEntity));
            if (entityType != null)
            {
                foreach (var property in entityType.GetNavigations())
                {
                    query = query.Include(property.Name);
                }
            }

            return await query.ToListAsync();
        }

        /// <summary>Aktif verileri navigation ve iç navigation (nested) property'lerle birlikte getirir.</summary>
        public async Task<IEnumerable<TEntity>> GetAllActiveWithNestedIncludes(Expression<Func<TEntity, bool>> predicate = null)
        {
            IQueryable<TEntity> query = _dbSet;
            query = query.Where(x => x.Status == Status.Active);

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var entityType = _context.Model.FindEntityType(typeof(TEntity));
            if (entityType != null)
            {
                foreach (var navigation in entityType.GetNavigations())
                {
                    query = query.Include(navigation.Name);

                    var targetEntityType = navigation.TargetEntityType;
                    if (targetEntityType != null)
                    {
                        foreach (var nestedNavigation in targetEntityType.GetNavigations())
                        {
                            if (nestedNavigation.TargetEntityType != entityType)
                            {
                                query = query.Include($"{navigation.Name}.{nestedNavigation.Name}");
                            }
                        }
                    }
                }
            }

            return await query.ToListAsync();
        }

        public virtual async Task<IEnumerable<TEntity>> GetAllFilterWithNestedIncludes(string filterField, string filterValue)
        {
            IQueryable<TEntity> query = _dbSet;
            query = query.Where(x => x.Status == Status.Active);

            // Dinamik olarak filtre uygulama
            var parameter = Expression.Parameter(typeof(TEntity), "x");
            var property = Expression.Property(parameter, filterField);
            var constant = Expression.Constant(filterValue);

            // Convert ID türüne (Guid veya string olabilir)
            Expression equal;
            if (property.Type == typeof(Guid) || property.Type == typeof(Guid?))
            {
                var guidValue = Guid.Parse(filterValue);
                equal = Expression.Equal(property, Expression.Constant(guidValue, property.Type));
            }
            else
            {
                equal = Expression.Equal(property, Expression.Convert(constant, property.Type));
            }

            var lambda = Expression.Lambda<Func<TEntity, bool>>(equal, parameter);
            query = query.Where(lambda);

            // Navigation ve iç navigation'ları dahil et
            var entityType = _context.Model.FindEntityType(typeof(TEntity));
            if (entityType != null)
            {
                foreach (var navigation in entityType.GetNavigations())
                {
                    query = query.Include(navigation.Name);

                    var targetEntityType = navigation.TargetEntityType;
                    if (targetEntityType != null)
                    {
                        foreach (var nestedNavigation in targetEntityType.GetNavigations())
                        {
                            if (nestedNavigation.TargetEntityType != entityType)
                            {
                                query = query.Include($"{navigation.Name}.{nestedNavigation.Name}");
                            }
                        }
                    }
                }
            }

            return await query.ToListAsync();
        }

        /// <summary>Belirli bir ID'ye sahip aktif bir veriyi tüm navigation property'leriyle getirir.</summary>
        public async Task<TEntity> GetByIdWithIncludes(string id)
        {
            var entity = await _dbSet.FindAsync(id);

            if (entity != null && entity.Status == Status.Active)
            {
                var entityType = _context.Model.FindEntityType(typeof(TEntity));
                if (entityType != null)
                {
                    foreach (var property in entityType.GetNavigations())
                    {
                        if (property.IsCollection)
                        {
                            await _context.Entry(entity).Collection(property.Name).LoadAsync();
                        }
                        else
                        {
                            await _context.Entry(entity).Reference(property.Name).LoadAsync();
                        }
                    }
                }
                _context.Entry(entity).State = EntityState.Detached;
            }

            return entity;
        }

        /// <summary>Belirli bir ID'ye sahip aktif veriyi navigation ve iç navigation property'leri ile getirir.</summary>
        public async Task<TEntity> GetByIdWithNestedIncludes(string id)
        {
            var entity = await _dbSet.FindAsync(id);

            if (entity == null || entity.Status != Status.Active)
            {
                return entity;
            }

            IQueryable<TEntity> query = _dbSet;

            var primaryKeyName = _context.Model.FindEntityType(typeof(TEntity))
                .FindPrimaryKey().Properties.First().Name;

            var parameter = Expression.Parameter(typeof(TEntity), "x");
            var property = Expression.Property(parameter, primaryKeyName);
            var constant = Expression.Constant(id);
            var equality = Expression.Equal(property, constant);
            var lambda = Expression.Lambda<Func<TEntity, bool>>(equality, parameter);

            query = query.Where(lambda);

            var entityType = _context.Model.FindEntityType(typeof(TEntity));
            if (entityType != null)
            {
                foreach (var navigation in entityType.GetNavigations())
                {
                    query = query.Include(navigation.Name);
                    var targetEntityType = navigation.TargetEntityType;
                    if (targetEntityType != null)
                    {
                        foreach (var nestedNavigation in targetEntityType.GetNavigations())
                        {
                            if (nestedNavigation.TargetEntityType != entityType)
                            {
                                query = query.Include($"{navigation.Name}.{nestedNavigation.Name}");
                            }
                        }
                    }
                }
            }

            // Yansımayla eklenen Include'lar aynı seviyede BİRDEN ÇOK koleksiyon içerebilir.
            // Tek sorguda birleşince satır sayısı bu koleksiyonların ÇARPIMI olur ve sorgu zaman aşımına düşebilir.
            // AsSplitQuery çarpımı toplama indirir; dönen veri aynıdır.
            // Takip de gereksizdir; kimlik çözümlemeli sürüm aynı kaydı tek nesne olarak tutar.
            var loadedEntity = await query
                .AsSplitQuery()
                .AsNoTrackingWithIdentityResolution()
                .FirstOrDefaultAsync();

            return loadedEntity;
        }

        /// <summary>Verilen ID'ye sahip veriyi bulur ve tracking kapatır.</summary>
        public async Task<TEntity> GetByIdDetached(string id)
        {
            var entity = await _dbSet.FindAsync(id);

            if (entity != null)
            {
                _context.Entry(entity).State = EntityState.Detached;
            }

            return entity;
        }

        /// <summary>Veriyi fiziksel olarak veritabanından siler.</summary>
        public void HardDelete(TEntity entity)
        {
            _dbSet.Remove(entity);
        }

        /// <summary>Verinin durumunu "Deleted" yaparak soft delete işlemi yapar.</summary>
        public void SoftDelete(TEntity entity)
        {
            entity.Status = Status.Deleted;
            _context.Entry(entity).State = EntityState.Modified;
        }

        /// <summary>Var olan bir veriyi günceller.</summary>
        public TEntity UpdateEntity(TEntity entity)
        {
            _context.Entry(entity).State = EntityState.Modified;
            return entity;
        }

        /// <summary>Belirli bir filtre koşuluna göre verileri döndürür.</summary>
        public IQueryable<TEntity> FilterBy(Expression<Func<TEntity, bool>> predicate)
        {
            return _dbSet.Where(predicate);
        }
    }
}
