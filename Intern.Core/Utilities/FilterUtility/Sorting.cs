using System.ComponentModel;

namespace Intern.Core.Utilities.FilterUtility
{
    public enum SortOrders
    {
        Asc = 1,
        Desc = 2
    }

    public class SortingParams
    {
        public SortOrders SortOrder { get; set; } = SortOrders.Asc;
        public string ColumnName { get; set; }
    }

    public class Sorting<T>
    {
        public static IQueryable<T> SortData(IQueryable<T> data, IQueryable<SortingParams> sortingParams)
        {
            IOrderedQueryable<T> sortedData = null;
            foreach (var sortingParam in sortingParams.Where(x => !string.IsNullOrEmpty(x.ColumnName)))
            {
                PropertyDescriptor prop = TypeDescriptor.GetProperties(typeof(T)).Find(sortingParam.ColumnName, false);

               // query = query.OrderBy(x => prop.GetValue(x));

                

                if(prop != null)
                {
                    if(sortedData == null)
                    {
                        if(sortingParam.SortOrder == SortOrders.Asc)
                        {
                            sortedData = data.OrderBy(x => prop.Name);
                        } 
                        else
                        {
                            sortedData= data.OrderByDescending(x => prop.Name);
                        }
                    }
                    else
                    {
                        if (sortingParam.SortOrder == SortOrders.Asc)
                        {
                            sortedData = sortedData.OrderBy(x => prop.Name);
                        }
                        else
                        {
                            sortedData=sortedData.OrderByDescending(x => prop.Name);
                        }
                    }
                }

                //var col = typeof(T).GetProperty(sortingParam.ColumnName, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
                //if (col != null)
                //{
                //    sortedData = sortedData == null ? sortingParam.SortOrder == SortOrders.Asc ? data.OrderBy(x => col.GetValue(x, null))
                //                                                                               : data.OrderByDescending(x => col.GetValue(x, null))
                //                                    : sortingParam.SortOrder == SortOrders.Asc ? sortedData.ThenBy(x => col.GetValue(x, null))
                //                                                                        : sortedData.ThenByDescending(x => col.GetValue(x, null));
                //}
            }
            return sortedData ?? data;
        }

    }
}
