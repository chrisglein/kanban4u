using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban4U.Models
{
    static class CollectionExtensions
    {
        public static void SortAndFilter<T, TKey>(this ObservableCollection<T> collection, Func<T, TKey> selector, 
            Func<T, bool> filter)
        {
            List<T> sorted = collection.OrderBy(selector).Where(filter).ToList();
            collection.Clear();
            sorted.ForEach(x => collection.Add(x));
        }

        public static void Sort<T, TKey>(this ObservableCollection<T> collection, Func<T, TKey> selector)
        {
            SortAndFilter(collection, selector, x => true);
        }
    }
}
