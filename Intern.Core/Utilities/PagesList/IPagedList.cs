namespace Intern.Core.Utilities.PagesList
{
    public interface IPagedList<T>
    {
        int PageNumber { get; }
        int PageSize { get; }
        int TotalCount { get; }
        int TotalPages { get; }
        bool HasPreviousPage { get; }
        bool HasNextPage { get; }
        IReadOnlyList<T> Items { get; }
        public T this[int index] => Items.ElementAt(index);
    }
}
