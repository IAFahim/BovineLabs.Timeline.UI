using BovineLabs.Timeline.UI.Data;
using NUnit.Framework;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Tests
{
    public class ControllableSelectionTests
    {
        [Test]
        public void EmptyCurrent_TakesCandidate()
        {
            var candidate = new Entity { Index = 7, Version = 1 };

            Assert.AreEqual(candidate, ControllableSelection.Select(Entity.Null, candidate));
        }

        [Test]
        public void LowerCandidateIndex_TakesCandidate()
        {
            var current = new Entity { Index = 5, Version = 1 };
            var candidate = new Entity { Index = 3, Version = 1 };

            Assert.AreEqual(candidate, ControllableSelection.Select(current, candidate));
        }

        [Test]
        public void HigherCandidateIndex_KeepsCurrent()
        {
            var current = new Entity { Index = 3, Version = 1 };
            var candidate = new Entity { Index = 5, Version = 1 };

            Assert.AreEqual(current, ControllableSelection.Select(current, candidate));
        }

        [Test]
        public void EqualIndex_KeepsIncumbent()
        {
            var current = new Entity { Index = 4, Version = 1 };
            var candidate = new Entity { Index = 4, Version = 2 };

            Assert.AreEqual(current, ControllableSelection.Select(current, candidate));
        }

        [Test]
        public void NullCandidate_WithPositiveCurrentIndex_TakesNullCandidate()
        {
            var current = new Entity { Index = 5, Version = 1 };

            Assert.AreEqual(Entity.Null, ControllableSelection.Select(current, Entity.Null));
        }

        [Test]
        public void NullCandidate_WithZeroCurrentIndex_KeepsCurrent()
        {
            var current = new Entity { Index = 0, Version = 1 };

            Assert.AreEqual(current, ControllableSelection.Select(current, Entity.Null));
        }
    }
}
