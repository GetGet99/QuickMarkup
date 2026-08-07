using System.Collections.ObjectModel;
using QuickMarkup.Infra;
using QuickMarkup.Infra.Collections;

namespace QuickMarkup.Infra.Test
{
    [TestClass]
    public sealed class ReactiveCountTests
    {
        [TestInitialize]
        public void Setup()
        {
            ReactiveScheduler.ResetForCurrentThread();
            ReactiveScheduler.Instance.Value!.AutoTick = false;
        }

        [TestMethod]
        public void ObservableCollection_ReactiveCount_TracksChanges()
        {
            var collection = new ObservableCollection<string> { "a", "b" };
            var computed = new Computed<int>(() => collection.ReactiveCount);

            Assert.AreEqual(2, computed.Value);

            collection.Add("c");
            ReactiveScheduler.Tick();

            Assert.AreEqual(3, computed.Value);

            collection.RemoveAt(0);
            ReactiveScheduler.Tick();

            Assert.AreEqual(2, computed.Value);
        }

        [TestMethod]
        public void ObservableCollection_ReactiveCountProp_TracksChanges()
        {
            var collection = new ObservableCollection<string> { "a" };
            var refs = new HashSet<IReference>();
            var computed = new Computed<int>(() => collection.ReactiveCountProp.Value);

            Assert.AreEqual(1, computed.Value);

            collection.Add("b");
            ReactiveScheduler.Tick();

            Assert.AreEqual(2, computed.Value);
        }
    }
}
