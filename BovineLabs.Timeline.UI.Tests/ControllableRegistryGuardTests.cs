using BovineLabs.Timeline.UI.Data;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Tests
{
    public class ControllableRegistryGuardTests
    {
        [Test]
        public void IsDuplicateClaim_EmptySlot_False()
        {
            var candidate = new Entity { Index = 3, Version = 1 };

            Assert.IsFalse(ControllableSelection.IsDuplicateClaim(Entity.Null, candidate));
        }

        [Test]
        public void IsDuplicateClaim_SameEntity_False()
        {
            var entity = new Entity { Index = 3, Version = 1 };

            Assert.IsFalse(ControllableSelection.IsDuplicateClaim(entity, entity));
        }

        [Test]
        public void IsDuplicateClaim_DifferentEntities_True()
        {
            var existing = new Entity { Index = 3, Version = 1 };
            var candidate = new Entity { Index = 5, Version = 1 };

            Assert.IsTrue(ControllableSelection.IsDuplicateClaim(existing, candidate));
        }

        [Test]
        public void IsDuplicateClaim_SameIndexDifferentVersion_True()
        {
            // A recycled entity id (same index, bumped version) is a genuinely different entity.
            var existing = new Entity { Index = 4, Version = 1 };
            var candidate = new Entity { Index = 4, Version = 2 };

            Assert.IsTrue(ControllableSelection.IsDuplicateClaim(existing, candidate));
        }

        [Test]
        public void Changed_IdenticalContent_False()
        {
            using var a = Populate(Entity.Null, new Entity { Index = 2, Version = 1 }, Entity.Null);
            using var b = Populate(Entity.Null, new Entity { Index = 2, Version = 1 }, Entity.Null);

            Assert.IsFalse(ControllableSelection.Changed(a, b));
        }

        [Test]
        public void Changed_DifferentEntity_True()
        {
            using var a = Populate(Entity.Null, new Entity { Index = 2, Version = 1 }, Entity.Null);
            using var b = Populate(Entity.Null, new Entity { Index = 7, Version = 1 }, Entity.Null);

            Assert.IsTrue(ControllableSelection.Changed(a, b));
        }

        [Test]
        public void Changed_ClearedSlot_True()
        {
            using var a = Populate(Entity.Null, new Entity { Index = 2, Version = 1 }, Entity.Null);
            using var b = Populate(Entity.Null, Entity.Null, Entity.Null);

            Assert.IsTrue(ControllableSelection.Changed(a, b));
        }

        [Test]
        public void Changed_DifferentLength_True()
        {
            using var a = new NativeArray<Entity>(2, Allocator.Temp);
            using var b = new NativeArray<Entity>(3, Allocator.Temp);

            Assert.IsTrue(ControllableSelection.Changed(a, b));
        }

        private static NativeArray<Entity> Populate(params Entity[] values)
        {
            var array = new NativeArray<Entity>(values.Length, Allocator.Temp);
            for (var i = 0; i < values.Length; i++)
                array[i] = values[i];
            return array;
        }
    }
}
