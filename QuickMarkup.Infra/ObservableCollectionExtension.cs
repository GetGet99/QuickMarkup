

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;

namespace QuickMarkup.Infra;

public static class ObservableCollectionExtension
{
    extension<T>(ObservableCollection<T> collection)
    {
        public IReference<int> ReactiveCountProp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ObservableCollectionCountCache<T>.Table.GetValue(
                    collection,
                    static c => new ObservableCollectionCountReference<T>(c));
            }
        }
        public int ReactiveCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => collection.ReactiveCountProp.Value;
        }
    }
}
static class ObservableCollectionCountCache<T>
{
    internal static readonly ConditionalWeakTable<
        ObservableCollection<T>,
        ObservableCollectionCountReference<T>> Table = new();
}
public class ObservableCollectionCountReference<T> : IReference<int>
{
    readonly ObservableCollection<T> collection;
    public ObservableCollectionCountReference(ObservableCollection<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        Value = collection.Count;
        this.collection = collection;
    }
    public int Value {
        get
        {
            ReferenceTracker.NotifyRefernceRead(this);
            return field;
        }
        private set
        {
            if (field == value)
                return;
            field = value;
            _ValueChanged?.Invoke();
        }
    }

    Action? _ValueChanged;
    public event Action ValueChanged
    {
        add
        {
            if (_ValueChanged is null)
                collection.CollectionChanged += ReactiveCountCollectionChangedHandler;
            _ValueChanged += value;
        }
        remove
        {
            _ValueChanged -= value;
            if (_ValueChanged is null)
                collection.CollectionChanged -= ReactiveCountCollectionChangedHandler;
        }
    }
    void ReactiveCountCollectionChangedHandler(object? sender, NotifyCollectionChangedEventArgs args) => Value = collection.Count;
}