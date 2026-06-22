using BovineLabs.Core.Extensions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Testing;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.UI.Data;
using NUnit.Framework;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Tests
{
    public class UISourceResolverTests : ECSTestsFixture
    {
        [Test]
        public void NoPlayer_RouteSelf_ResolvesToSelf()
        {
            var self = Manager.CreateEntity();
            var (ok, resolved) = Resolve(Source(Target.Self), self);

            Assert.IsTrue(ok);
            Assert.AreEqual(self, resolved);
        }

        [Test]
        public void NoPlayer_RouteNone_ResolvesToSelf()
        {
            var self = Manager.CreateEntity();
            var (ok, resolved) = Resolve(Source(Target.None), self);

            Assert.IsTrue(ok);
            Assert.AreEqual(self, resolved);
        }

        [Test]
        public void NoPlayer_RouteTarget_ResolvesThroughTargets()
        {
            var other = Manager.CreateEntity();
            var self = Manager.CreateEntity();
            Manager.AddComponentData(self, new Targets { Target = other });

            var (ok, resolved) = Resolve(Source(Target.Target), self);

            Assert.IsTrue(ok);
            Assert.AreEqual(other, resolved);
        }

        [Test]
        public void NoPlayer_RouteTarget_WithoutTargetsComponent_Fails()
        {
            var self = Manager.CreateEntity();
            var (ok, _) = Resolve(Source(Target.Target), self);

            Assert.IsFalse(ok);
        }

        [Test]
        public void NoPlayer_RouteTarget_NullTarget_Fails()
        {
            var self = Manager.CreateEntity();
            Manager.AddComponentData(self, new Targets { Target = Entity.Null });

            var (ok, _) = Resolve(Source(Target.Target), self);

            Assert.IsFalse(ok);
        }

        private static UISource Source(Target route)
        {
            return new UISource { Player = UISource.NoPlayer, Route = route, LinkKey = 0 };
        }

        private (bool ok, Entity resolved) Resolve(UISource source, Entity self)
        {
            UISourceResolverProbe.Source = source;
            UISourceResolverProbe.Self = self;
            World.GetOrCreateSystem<UISourceResolverProbe>().Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            return (UISourceResolverProbe.Success, UISourceResolverProbe.Resolved);
        }
    }

    public partial struct UISourceResolverProbe : ISystem
    {
        public static UISource Source;
        public static Entity Self;
        public static bool Success;
        public static Entity Resolved;

        public void OnUpdate(ref SystemState state)
        {
            var targets = state.GetUnsafeComponentLookup<Targets>(true);
            var sources = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            var links = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);

            Success = UISourceResolver.TryResolve(in Source, Self, default, in targets, in sources, in links,
                out Resolved);
        }
    }
}