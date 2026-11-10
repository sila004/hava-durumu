using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Intern.Core.Utilities.PagesList
{
    public static class PagedListExtensions
    {
        public static IPagedList<T> ToPagedList<T>(this IQueryable<T> source, int pageSize, int pageNumber = 1)
        {
            int count = source.Count();
            int totalPages = (int)Math.Ceiling((double)count / pageSize);
            if (totalPages == 0) totalPages = 1;
            if ((pageNumber - 1) >= totalPages) throw new Exception("404");
            var pagedList = source.Skip((pageNumber-1) * pageSize).Take(pageSize);
            return new PagedList<T>(pageNumber, pageSize, count, totalPages, pagedList.ToList());
        }

        public static async Task<IPagedList<T>> ToPagedListAsync<T>(this IQueryable<T> source, int pageSize, int pageNumber = 1)
        {
            int count = source.Count();
            int totalPages = (int)Math.Ceiling((double)count / pageSize);
            if (totalPages == 0) totalPages = 1;
            if ((pageNumber - 1) >= totalPages) throw new Exception("404 ^__^");
            var pagedList = source.Skip((pageNumber-1) * pageSize).Take(pageSize);
            var result = await pagedList.ToListAsync();
            return new PagedList<T>(pageNumber, pageSize, count, totalPages, result);
        }

        public static IPagedList<TResult> ToPagedList<TEntity, TResult>(this IQueryable<TEntity> source, int pageSize, int pageNumber = 1, Expression<Func<TEntity, TResult>> selector = null)
        {
            int count = source.Count();
            int totalPages = (int)Math.Ceiling((double)count / pageSize);
            if (totalPages == 0) totalPages = 1;
            if ((pageNumber - 1) >= totalPages) throw new Exception("404");
            source = source.Skip((pageNumber - 1) * pageSize).Take(pageSize);
            var pagedList = new List<TResult>();
            if (selector != null) pagedList = new List<TResult>(source.Select(selector));
            return new PagedList<TResult>(pageNumber, pageSize, count, totalPages, pagedList);
        }

        public static async Task<IPagedList<TResult>> ToPagedListAsync<TEntity, TResult>(this IQueryable<TEntity> source,
            int pageSize,
            int pageNumber = 1,
            Expression<Func<TEntity, TResult>> selector = null)
        {
            int count = source.Count();
            int totalPages = (int)Math.Ceiling((double)count / pageSize);
            if (totalPages == 0) totalPages = 1;
            if ((pageNumber - 1) >= totalPages) throw new Exception("404 ^__^");
            source = source.Skip((pageNumber - 1) * pageSize).Take(pageSize);
            var pagedList = new List<TResult>();
            if (selector != null) pagedList = new List<TResult>(await source.Select(selector).ToListAsync());
            return new PagedList<TResult>(pageNumber, pageSize, count, totalPages, pagedList);
        }

        public static IPagedList<T> ToPagedList<T>(this IEnumerable<T> source, int pageSize, int pageNumber, int totalCount, int totalPages)
        {
            if (totalPages == 0) totalPages = 1;
            if ((pageNumber - 1) >= totalPages) throw new Exception("404 ^__^");
            return new PagedList<T>(pageNumber, pageSize, totalCount, totalPages, source.ToList());
        }
    }
}

