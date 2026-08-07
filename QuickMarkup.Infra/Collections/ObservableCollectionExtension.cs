using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace QuickMarkup.Infra.Collections;

public static class ObservableCollectionExtension
{
    extension<T>(ObservableCollection<T> collection)
    {
        /// <summary>
        /// Wraps <see cref="ObservableCollection{T}"/> into an <see cref="IReference{T}"/>.
        /// It notifies whenever <see cref="ObservableCollection{T}"/> collection changed.
        /// </summary>
        /// <remarks>
        /// Reference always return the same <see cref="ObservableCollection{T}"/> instance
        /// <c>ReferenceEquals(myCollection, myCollection.ReactiveProp.Value)</c>.
        /// However, <c>myCollection.ReactiveProp.Value</c> will participate in QuickMarkup reactive chain.
        /// <c>.Value</c> getter must be invoked in reactive tracking context for the reference to be tracked.<br/>
        /// <code>
        /// var prop = myCollection.ReactiveProp;
        /// var sample1 = Computed(() => ReactiveProp.Value.Count); // this is reactive
        /// var collection2 = myCollection.ReactiveProp.Value;
        /// var sample2 = Computed(() => collection2.Count); // this is NOT reactive and may not work as expected
        /// </code>
        /// </remarks>
        public IReference<ObservableCollection<T>> ReactiveProp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return SingletonWeakTable<ObservableCollection<T>, ObservableCollectionReference<T>>.Table.GetValue(
                collection,
                static c => new(c));
            }
        }

        /// <summary>
        /// Enable <see cref="ObservableCollection{T}"/> to participate in reactive chain.
        /// </summary>
        /// <remarks>
        /// Property returns the same <see cref="ObservableCollection{T}"/> instance
        /// <c>ReferenceEquals(myCollection, myCollection.Reactive)</c>.
        /// However, <c>myCollection.Reactive</c> will participate in QuickMarkup reactive chain.
        /// <c>.Reactive</c> getter must be invoked in reactive tracking context for the reference to be tracked.<br/>
        /// <code>
        /// var sample1 = Computed(() => myCollection.Reactive.Count); // this is reactive
        /// var collection2 = myCollection.Reactive;
        /// var sample2 = Computed(() => collection2.Count); // this is NOT reactive and may not work as expected
        /// </code>
        /// </remarks>
        public ObservableCollection<T> Reactive
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return collection.ReactiveProp.Value;
            }
        }
    }
}
