using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace QuickMarkup.Infra.Collections;

internal class ObservableCollectionReference<T> : IReference<ObservableCollection<T>>
{
    readonly ObservableCollection<T> collection;
    public ObservableCollectionReference(ObservableCollection<T> collection)
    {
        if (collection is null)
            throw new ArgumentNullException(nameof(collection));
        Value = collection;
        this.collection = collection;
    }
    public ObservableCollection<T> Value {
        get
        {
            ReferenceTracker.NotifyRefernceRead(this);
            return field;
        }
    }

    Action? _ValueChanged;
    public event Action ValueChanged
    {
        add
        {
            if (_ValueChanged is null)
                collection.CollectionChanged += CollectionChangedHandler;
            _ValueChanged += value;
        }
        remove
        {
            _ValueChanged -= value;
            if (_ValueChanged is null)
                collection.CollectionChanged -= CollectionChangedHandler;
        }
    }
    void CollectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args) => _ValueChanged?.Invoke();
}