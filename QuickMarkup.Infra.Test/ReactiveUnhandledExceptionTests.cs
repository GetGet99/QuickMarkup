using QuickMarkup.Infra;

namespace QuickMarkup.Infra.Test;

[TestClass]
public sealed class ReactiveUnhandledExceptionTests
{
    [TestInitialize]
    public void Setup()
    {
        ReactiveScheduler.ResetForCurrentThread();
        ReactiveScheduler.Instance.Value!.AutoTick = false;
        ReactiveScheduler.Instance.Value!.ContinueOnException = false;
    }

    [TestCleanup]
    public void Cleanup()
    {
        ReactiveScheduler.ResetForCurrentThread();
    }

    [TestMethod]
    public void TickRethrowsWhenNoHandler()
    {
        Reference<int> value = new(0);
        ReferenceTracker.RunAndRerunOnReferenceChange(
            () => value.Value,
            x =>
            {
                if (x == 1)
                    throw new InvalidOperationException("fail");
            });

        value.Value = 1;

        Assert.ThrowsExactly<InvalidOperationException>(() => ReactiveScheduler.Tick());
    }

    [TestMethod]
    public void HandledExceptionLetsRemainingEffectsRun()
    {
        List<Exception> reported = [];
        EventHandler<ReactiveUnhandledExceptionEventArgs> handler = (_, args) =>
        {
            reported.Add(args.Exception);
            args.Handled = true;
        };
        ReactiveScheduler.UnhandledExceptionForCurrentThread += handler;
        try
        {
            Reference<int> value = new(0);
            int safeRuns = 0;

            ReferenceTracker.RunAndRerunOnReferenceChange(
                () => value.Value,
                x =>
                {
                    if (x == 1)
                        throw new InvalidOperationException("fail");
                });

            ReferenceTracker.RunAndRerunOnReferenceChange(
                () => value.Value,
                _ => safeRuns++);

            Assert.AreEqual(1, safeRuns);

            value.Value = 1;
            ReactiveScheduler.Tick();

            Assert.AreEqual(2, safeRuns);
            Assert.AreEqual(1, reported.Count);
            Assert.IsInstanceOfType<InvalidOperationException>(reported[0]);
        }
        finally
        {
            ReactiveScheduler.UnhandledExceptionForCurrentThread -= handler;
        }
    }

    [TestMethod]
    public void NotHandledExceptionRethrowsAfterReporting()
    {
        List<Exception> reported = [];
        EventHandler<ReactiveUnhandledExceptionEventArgs> handler = (_, args) =>
            reported.Add(args.Exception);
        ReactiveScheduler.UnhandledExceptionForCurrentThread += handler;
        try
        {
            Reference<int> value = new(0);
            ReferenceTracker.RunAndRerunOnReferenceChange(
                () => value.Value,
                x =>
                {
                    if (x == 1)
                        throw new InvalidOperationException("fail");
                });

            value.Value = 1;

            Assert.ThrowsExactly<InvalidOperationException>(() => ReactiveScheduler.Tick());
            Assert.AreEqual(1, reported.Count);
        }
        finally
        {
            ReactiveScheduler.UnhandledExceptionForCurrentThread -= handler;
        }
    }

    [TestMethod]
    public void CallbackExceptionIsReported()
    {
        List<Exception> reported = [];
        EventHandler<ReactiveUnhandledExceptionEventArgs> handler = (_, args) =>
        {
            reported.Add(args.Exception);
            args.Handled = true;
        };
        ReactiveScheduler.UnhandledExceptionForCurrentThread += handler;
        try
        {
            ReactiveScheduler.ScheduleCallback(() => throw new InvalidOperationException("fail"));

            ReactiveScheduler.Tick();

            Assert.AreEqual(1, reported.Count);
        }
        finally
        {
            ReactiveScheduler.UnhandledExceptionForCurrentThread -= handler;
        }
    }

    [TestMethod]
    public void DoNowIfScheduledReportsAndSchedulerStaysUsable()
    {
        List<Exception> reported = [];
        EventHandler<ReactiveUnhandledExceptionEventArgs> handler = (_, args) =>
        {
            reported.Add(args.Exception);
            args.Handled = true;
        };
        ReactiveScheduler.UnhandledExceptionForCurrentThread += handler;
        try
        {
            Reference<int> value = new(0);
            Computed<int> computed = new(() => value.Value == 1
                ? throw new InvalidOperationException("fail")
                : value.Value);

            value.Value = 1;

            Assert.AreEqual(0, computed.Value);
            Assert.AreEqual(1, reported.Count);

            value.Value = 2;
            ReactiveScheduler.Tick();

            Assert.AreEqual(2, computed.Value);
        }
        finally
        {
            ReactiveScheduler.UnhandledExceptionForCurrentThread -= handler;
        }
    }

    [TestMethod]
    public void ConstructionFailureIsReported()
    {
        List<Exception> reported = [];
        EventHandler<ReactiveUnhandledExceptionEventArgs> handler = (_, args) =>
        {
            reported.Add(args.Exception);
            args.Handled = true;
        };
        ReactiveScheduler.UnhandledExceptionForCurrentThread += handler;
        try
        {
            ReferenceTracker.RunAndRerunOnReferenceChange<int>(
                () => throw new InvalidOperationException("fail"),
                _ => { });

            Assert.AreEqual(1, reported.Count);
        }
        finally
        {
            ReactiveScheduler.UnhandledExceptionForCurrentThread -= handler;
        }
    }
}
