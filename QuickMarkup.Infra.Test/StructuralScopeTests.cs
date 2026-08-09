using QuickMarkup.Infra;
using QuickMarkup.Infra.Collections;

namespace QuickMarkup.Infra.Test.Blocks
{
    [TestClass]
    public sealed class StructuralScopeTests
    {
        [TestInitialize]
        public void Setup()
        {
            ReactiveScheduler.ResetForCurrentThread();
            ReactiveScheduler.Instance.Value!.AutoTick = false;
            ReactiveScheduler.Instance.Value!.ContinueOnException = false;
        }

        [TestMethod]
        public void ScopeHierarchy_EstablishesParentAndDepth()
        {
            var root = new ReactiveScope();
            ReactiveScope child;

            using (ReferenceTracker.EnterStructuralScope(root))
            {
                child = new ReactiveScope();
            }

            Assert.IsNull(root.Parent);
            Assert.AreEqual(0, root.Depth);
            Assert.AreSame(root, child.Parent);
            Assert.AreEqual(1, child.Depth);
        }

        [TestMethod]
        public void EffectAddedToScope_IsAssociatedWithThatScope()
        {
            var scope = new ReactiveScope();
            var effect = new RefEffect(_ => { });

            scope.Add(effect);

            Assert.AreSame(scope, effect.Scope);
            Assert.AreEqual(scope.Depth, effect.Scope!.Depth);
        }

        [TestMethod]
        public void IfBranchRemoval_SkipsQueuedDescendantEffect()
        {
            var show = new Reference<bool>(true);
            var value = new Reference<string>("a");
            var bindRuns = 0;
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new ConditionalBlock<TextBlock>(
                new ReactiveScope(),
                () => show.Value,
                () => new FragmentBlock<TextBlock>(
                    new ReactiveScope(),
                    (h, scope) =>
                    {
                        h.AddBlock(new StaticBlock<TextBlock>(
                            new ReactiveScope(),
                            (elements, scope) =>
                            {
                                var box = new TextBlock();
                                elements.Add(box);
                                scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                                    () => { bindRuns++; return value.Value; },
                                    v => box.Text = v));
                            }));
                    })));

            Assert.AreEqual("a", target[0].Text);
            var runsBefore = bindRuns;

            show.Value = false;
            value.Value = "b";
            ReactiveScheduler.Tick();

            Assert.IsEmpty(target);
            Assert.AreEqual(runsBefore, bindRuns);
        }

        [TestMethod]
        public void AncestorEffect_RunsBeforeDescendantEffect_WhenBothScheduled()
        {
            var show = new Reference<bool>(true);
            var value = new Reference<string>("a");
            var log = new List<string>();
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new ConditionalBlock<TextBlock>(
                new ReactiveScope(),
                () => { log.Add("if"); return show.Value; },
                () => new FragmentBlock<TextBlock>(
                    new ReactiveScope(),
                    (h, scope) =>
                    {
                        h.AddBlock(new StaticBlock<TextBlock>(
                            new ReactiveScope(),
                            (elements, scope) =>
                            {
                                var box = new TextBlock();
                                elements.Add(box);
                                scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                                    () => { log.Add("child"); return value.Value; },
                                    v => box.Text = v));
                            }));
                    })));

            log.Clear();

            show.Value = false;
            show.Value = true;
            value.Value = "b";
            ReactiveScheduler.Tick();

            Assert.AreEqual("b", target[0].Text);
            CollectionAssert.AreEqual(new[] { "if", "child" }, log);
        }

        [TestMethod]
        public void BranchCreatedDuringReconcile_ReadsFinalValues()
        {
            var show = new Reference<bool>(false);
            var value = new Reference<string>("a");
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new ConditionalBlock<TextBlock>(
                new ReactiveScope(),
                () => show.Value,
                () => new FragmentBlock<TextBlock>(
                    new ReactiveScope(),
                    (h, scope) =>
                    {
                        h.AddBlock(new StaticBlock<TextBlock>(
                            new ReactiveScope(),
                            (elements, scope) =>
                            {
                                var box = new TextBlock();
                                elements.Add(box);
                                scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                                    () => value.Value,
                                    v => box.Text = v));
                            }));
                    })));

            Assert.IsEmpty(target);

            show.Value = true;
            value.Value = "b";
            ReactiveScheduler.Tick();

            Assert.AreEqual("b", target[0].Text);
        }

        [TestMethod]
        public void EffectsScheduledDuringTick_DeferToNextTick()
        {
            var value = new Reference<string>("a");
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new StaticBlock<TextBlock>(
                new ReactiveScope(),
                (elements, scope) =>
                {
                    var box = new TextBlock();
                    elements.Add(box);
                    scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                        () => value.Value,
                        v =>
                        {
                            box.Text = v;
                            if (v == "b")
                                value.Value = "c";
                        }));
                }));

            value.Value = "b";
            ReactiveScheduler.Tick();

            Assert.AreEqual("b", target[0].Text);

            ReactiveScheduler.Tick();

            Assert.AreEqual("c", target[0].Text);
        }

        [TestMethod]
        public void Computed_Value_StabilizesOnRead_AndIsNotStructurallyScoped()
        {
            var baseRef = new Reference<int>(1);
            var scope = new ReactiveScope();
            Computed<int> computed;

            using (ReferenceTracker.EnterStructuralScope(scope))
            {
                computed = new Computed<int>(() => baseRef.Value + 1);
            }

            scope.Dispose();

            baseRef.Value = 10;

            Assert.AreEqual(11, computed.Value);
        }

        [TestMethod]
        public void ForEachItemIf_ItemRemoved_SkipsQueuedDescendantEffect()
        {
            var items = new ReactiveList<ScopeItem>
            {
                new(new(true), new("one")),
                new(new(true), new("two")),
            };
            var bindRuns = 0;
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(QuickMarkup.Infra.Blocks.ForBlock.Create<ScopeItem, TextBlock>(
                new ReactiveScope(),
                items,
                itemRef => new FragmentBlock<TextBlock>(
                    new ReactiveScope(),
                    (h, scope) =>
                    {
                        h.AddBlock(new ConditionalBlock<TextBlock>(
                            new ReactiveScope(),
                            () => itemRef.Value.Show.Value,
                            () => new StaticBlock<TextBlock>(
                                new ReactiveScope(),
                                (elements, scope) =>
                                {
                                    var box = new TextBlock();
                                    elements.Add(box);
                                    scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                                        () => { bindRuns++; return itemRef.Value.Value.Value; },
                                        v => box.Text = v));
                                })));
                    })));

            ForBlockTestSupport.AssertText(target, "one", "two");
            var runsBefore = bindRuns;
            var removed = items[0];

            items.RemoveAt(0);
            removed.Value.Value = "ONE";
            ReactiveScheduler.Tick();
            ReactiveScheduler.Tick();

            ForBlockTestSupport.AssertText(target, "two");
            Assert.AreEqual(runsBefore, bindRuns);
        }

        [TestMethod]
        public void IfContainsForEach_AncestorIf_RemovesBranchBeforeItemEffects()
        {
            var show = new Reference<bool>(true);
            var items = new ReactiveList<ScopeItem>
            {
                new(new(true), new("one")),
                new(new(true), new("two")),
            };
            var bindRuns = 0;
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new ConditionalBlock<TextBlock>(
                new ReactiveScope(),
                () => show.Value,
                () => new FragmentBlock<TextBlock>(
                    new ReactiveScope(),
                    (h, scope) =>
                    {
                        h.AddBlock(QuickMarkup.Infra.Blocks.ForBlock.Create<ScopeItem, TextBlock>(
                            new ReactiveScope(),
                            items,
                            itemRef => new FragmentBlock<TextBlock>(
                                new ReactiveScope(),
                                (h, scope) =>
                                {
                                    h.AddBlock(new StaticBlock<TextBlock>(
                                        new ReactiveScope(),
                                        (elements, scope) =>
                                        {
                                            var box = new TextBlock();
                                            elements.Add(box);
                                            scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                                                () => { bindRuns++; return itemRef.Value.Value.Value; },
                                                v => box.Text = v));
                                        }));
                                })));
                    })));

            ForBlockTestSupport.AssertText(target, "one", "two");
            var runsBefore = bindRuns;

            show.Value = false;
            items[0].Value.Value = "ONE";
            ReactiveScheduler.Tick();
            ReactiveScheduler.Tick();

            Assert.IsEmpty(target);
            Assert.AreEqual(runsBefore, bindRuns);
        }

        [TestMethod]
        public void DisposedScope_QueuedEffect_IsSkippedByScheduler()
        {
            var scope = new ReactiveScope();
            var ran = false;
            var effect = new RefEffect(_ => ran = true);
            scope.Add(effect);
            ReactiveScheduler.ScheduleEffect(effect);
            scope.Dispose();

            ReactiveScheduler.Tick();

            Assert.IsFalse(ran);
        }

        [TestMethod]
        public void DisposedAncestorScope_QueuedDescendantEffect_IsSkippedByScheduler()
        {
            var grandparent = new ReactiveScope();
            ReactiveScope child = null!;
            ReactiveScope grandchild = null!;
            var ran = false;

            using (ReferenceTracker.EnterStructuralScope(grandparent))
            {
                child = new ReactiveScope();
                using (ReferenceTracker.EnterStructuralScope(child))
                {
                    grandchild = new ReactiveScope();
                }
            }

            var effect = new RefEffect(_ => ran = true);
            grandchild.Add(effect);
            ReactiveScheduler.ScheduleEffect(effect);

            grandparent.Dispose();
            Assert.IsFalse(grandchild.IsDisposed);

            ReactiveScheduler.Tick();

            Assert.IsFalse(ran);
        }

        sealed record ScopeItem(Reference<bool> Show, Reference<string> Value);
    }
}
