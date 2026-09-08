using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace TypeIt4Me.Services
{
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        public void ReplaceAll(IEnumerable<T> items)
        {
            if (items == null)
                throw new System.ArgumentNullException(nameof(items));

            Items.Clear();
            foreach (var item in items)
                Items.Add(item);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new System.ArgumentNullException(nameof(items));

            foreach (var item in items)
                Items.Add(item);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
