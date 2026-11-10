namespace Intern.Core.Utilities.FilterUtility
{
    public class InputModel
    {
        public IEnumerable<SortingParams> SortingParams { set; get; }
        public IEnumerable<FilterParams> FilterParam { get; set; }
        public int PageNumber { get; set; } = 0;
        public int PageSize { get; set; } = 20;
    }
}
