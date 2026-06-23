using BovineLabs.Timeline.UI.Data;
using NUnit.Framework;

namespace BovineLabs.Timeline.UI.Tests
{
    public class IdValueLookupTests
    {
        [Test]
        public void Present_ReturnsValue()
        {
            var entries = new[]
            {
                new IdValue { Id = 3, Value = 1.5f },
                new IdValue { Id = 7, Value = 2.5f },
            };

            Assert.AreEqual(2.5f, IdValueLookup.Resolve(entries, 7));
        }

        [Test]
        public void Absent_ReturnsZero()
        {
            var entries = new[]
            {
                new IdValue { Id = 3, Value = 1.5f },
            };

            Assert.AreEqual(0f, IdValueLookup.Resolve(entries, 99));
        }

        [Test]
        public void Duplicate_ReturnsFirstMatch()
        {
            var entries = new[]
            {
                new IdValue { Id = 5, Value = 10f },
                new IdValue { Id = 5, Value = 20f },
            };

            Assert.AreEqual(10f, IdValueLookup.Resolve(entries, 5));
        }

        [Test]
        public void Empty_ReturnsZero()
        {
            var entries = new IdValue[0];

            Assert.AreEqual(0f, IdValueLookup.Resolve(entries, 1));
        }
    }
}
