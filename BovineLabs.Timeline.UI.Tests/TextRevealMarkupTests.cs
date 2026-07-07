using BovineLabs.Timeline.UI.Data;
using NUnit.Framework;

namespace BovineLabs.Timeline.UI.Tests
{
    public class TextRevealMarkupTests
    {
        [Test]
        public void BuildRevealOffsets_EmptyString_NoSteps()
        {
            var offsets = TextReveal.BuildRevealOffsets(string.Empty);

            Assert.AreEqual(0, offsets.Length);
        }

        [Test]
        public void BuildRevealOffsets_PlainAscii_OneStepPerChar()
        {
            var offsets = TextReveal.BuildRevealOffsets("abc");

            Assert.AreEqual(new[] { 1, 2, 3 }, offsets);
        }

        [Test]
        public void BuildRevealOffsets_FinalStep_ReproducesSource()
        {
            const string text = "a<b>bold</b>c";
            var offsets = TextReveal.BuildRevealOffsets(text);

            Assert.Greater(offsets.Length, 0);
            Assert.AreEqual(text.Length, offsets[offsets.Length - 1]);
            Assert.AreEqual(text, text.Substring(0, offsets[offsets.Length - 1]));
        }

        [Test]
        public void BuildRevealOffsets_RichText_NeverRevealsPartialTag()
        {
            const string text = "a<b>bold</b>";
            var offsets = TextReveal.BuildRevealOffsets(text);

            foreach (var offset in offsets)
            {
                var revealed = text.Substring(0, offset);

                // A partial tag would leave a '<' with no matching '>' after it in the revealed substring.
                Assert.LessOrEqual(revealed.LastIndexOf('<'), revealed.LastIndexOf('>'),
                    $"Revealed substring '{revealed}' ends inside a tag.");
            }
        }

        [Test]
        public void BuildRevealOffsets_TagOnly_SingleFullStep()
        {
            const string text = "<b></b>";
            var offsets = TextReveal.BuildRevealOffsets(text);

            Assert.AreEqual(1, offsets.Length);
            Assert.AreEqual(text.Length, offsets[0]);
        }

        [Test]
        public void BuildRevealOffsets_LeadingTag_AttachesToFollowingVisibleStep()
        {
            // The opening <b> is atomic and rides along with the first visible character 'a'.
            const string text = "<b>ab";
            var offsets = TextReveal.BuildRevealOffsets(text);

            Assert.AreEqual(2, offsets.Length);
            Assert.AreEqual("<b>a", text.Substring(0, offsets[0]));
            Assert.AreEqual("<b>ab", text.Substring(0, offsets[1]));
        }

        [Test]
        public void BuildRevealOffsets_ZwjCluster_RevealedAtomically()
        {
            // Family emoji (ZWJ sequence) is 8 UTF-16 chars; it must reveal as one step, then 'x'.
            const string family = "👨‍👩‍👧";
            var text = family + "x";

            var offsets = TextReveal.BuildRevealOffsets(text);

            Assert.AreEqual(2, offsets.Length, "ZWJ cluster must not split into sub-steps.");
            Assert.AreEqual(family.Length, offsets[0]);
            Assert.AreEqual(family, text.Substring(0, offsets[0]));
            Assert.AreEqual(text.Length, offsets[1]);
        }

        [Test]
        public void BuildRevealOffsets_SurrogatePair_NotSplit()
        {
            const string text = "a😀b";
            var offsets = TextReveal.BuildRevealOffsets(text);

            // 'a'(1), '😀' surrogate pair spans two UTF-16 units -> ends at 3, 'b' -> 4.
            Assert.AreEqual(new[] { 1, 3, 4 }, offsets);
        }
    }
}
