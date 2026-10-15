namespace nvidiaProfileInspector.UI.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;

    /// <summary>
    /// ObservableCollection that can swap its entire content with a single Reset notification,
    /// avoiding per-item CollectionChanged handling when the settings view is rebuilt.
    /// </summary>
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        private int _deferredUpdates;

        public void ReplaceAll(IEnumerable<T> items)
        {
            Items.Clear();
            if (items != null)
            {
                foreach (var item in items)
                    Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Batches many mutations into a single Reset notification. Nestable;
        /// the Reset fires when the outermost scope is disposed. New items added
        /// during the scope are preserved - only the notifications collapse.
        /// </summary>
        public IDisposable BeginUpdate()
        {
            _deferredUpdates++;
            return new UpdateScope(this);
        }

        private void EndUpdate()
        {
            if (_deferredUpdates <= 0)
                return;

            _deferredUpdates--;
            if (_deferredUpdates == 0)
            {
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (_deferredUpdates > 0)
                return;

            base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (_deferredUpdates > 0)
                return;

            base.OnPropertyChanged(e);
        }

        private sealed class UpdateScope : IDisposable
        {
            private readonly BulkObservableCollection<T> _owner;
            private bool _disposed;

            public UpdateScope(BulkObservableCollection<T> owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _owner.EndUpdate();
            }
        }
    }
}
