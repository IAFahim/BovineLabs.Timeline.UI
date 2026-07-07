using NUnit.Framework;

namespace BovineLabs.Timeline.UI.Tests
{
    public class UssClassRefCountTests
    {
        [Test]
        public void FirstAcquire_NotPreExisting_ShouldAdd()
        {
            var refs = new UssClassTrackSystem.ClassRefCounts();
            var key = new object();

            Assert.IsTrue(refs.TryAcquire(key, preExisting: false));
        }

        [Test]
        public void FirstAcquire_PreExisting_ShouldNotAdd()
        {
            var refs = new UssClassTrackSystem.ClassRefCounts();
            var key = new object();

            Assert.IsFalse(refs.TryAcquire(key, preExisting: true));
        }

        [Test]
        public void SecondAcquire_SameKey_ShouldNotAdd()
        {
            var refs = new UssClassTrackSystem.ClassRefCounts();
            var key = new object();

            refs.TryAcquire(key, preExisting: false);

            Assert.IsFalse(refs.TryAcquire(key, preExisting: false));
        }

        [Test]
        public void Overlap_ClassStaysUntilLastRelease()
        {
            var refs = new UssClassTrackSystem.ClassRefCounts();
            var key = new object();

            // Two overlapping holders on the same (element, class).
            Assert.IsTrue(refs.TryAcquire(key, preExisting: false)); // A applies
            Assert.IsFalse(refs.TryAcquire(key, preExisting: false)); // B rides along

            // A ends first -> must NOT remove while B is still active.
            Assert.IsFalse(refs.Release(key));

            // B ends last -> now remove.
            Assert.IsTrue(refs.Release(key));
        }

        [Test]
        public void PreExisting_NeverRemoved_EvenAfterAllReleases()
        {
            var refs = new UssClassTrackSystem.ClassRefCounts();
            var key = new object();

            refs.TryAcquire(key, preExisting: true);  // first holder observes UXML-authored class
            refs.TryAcquire(key, preExisting: false); // later holder's flag is ignored

            Assert.IsFalse(refs.Release(key)); // second holder out
            Assert.IsFalse(refs.Release(key)); // last holder out -> still not removed (was pre-existing)
        }

        [Test]
        public void Release_ClearsEntry_AllowingFreshPreExistingCapture()
        {
            var refs = new UssClassTrackSystem.ClassRefCounts();
            var key = new object();

            refs.TryAcquire(key, preExisting: false);
            Assert.IsTrue(refs.Release(key));
            Assert.AreEqual(0, refs.Count);

            // A brand-new group re-captures pre-existing state independently.
            Assert.IsFalse(refs.TryAcquire(key, preExisting: true));
        }

        [Test]
        public void Release_UnknownKey_ReturnsFalse()
        {
            var refs = new UssClassTrackSystem.ClassRefCounts();

            Assert.IsFalse(refs.Release(new object()));
        }

        [Test]
        public void DistinctKeys_TrackedIndependently()
        {
            var refs = new UssClassTrackSystem.ClassRefCounts();
            var a = new object();
            var b = new object();

            Assert.IsTrue(refs.TryAcquire(a, preExisting: false));
            Assert.IsTrue(refs.TryAcquire(b, preExisting: false));
            Assert.AreEqual(2, refs.Count);

            Assert.IsTrue(refs.Release(a));
            Assert.AreEqual(1, refs.Count);
            Assert.IsTrue(refs.Release(b));
            Assert.AreEqual(0, refs.Count);
        }

        [Test]
        public void Clear_EmptiesRegistry()
        {
            var refs = new UssClassTrackSystem.ClassRefCounts();
            refs.TryAcquire(new object(), preExisting: false);
            refs.TryAcquire(new object(), preExisting: false);

            refs.Clear();

            Assert.AreEqual(0, refs.Count);
        }
    }
}
