using BovineLabs.Timeline.UI.Data;
using NUnit.Framework;

namespace BovineLabs.Timeline.UI.Tests
{
    public class UxmlAttachTests
    {
        [Test]
        public void NoTarget_AppendToElement_PlansRoot()
        {
            var plan = UxmlAttach.PlanAttach(UxmlAttachmentMode.AppendToElement, false, false, 3);

            Assert.AreEqual(AttachOp.Root, plan.Op);
        }

        [Test]
        public void NoTarget_InsertBeforeElement_PlansRoot()
        {
            var plan = UxmlAttach.PlanAttach(UxmlAttachmentMode.InsertBeforeElement, false, false, 3);

            Assert.AreEqual(AttachOp.Root, plan.Op);
        }

        [Test]
        public void AppendToElement_HasTarget_PlansAppendChild()
        {
            var plan = UxmlAttach.PlanAttach(UxmlAttachmentMode.AppendToElement, true, true, 3);

            Assert.AreEqual(AttachOp.AppendChild, plan.Op);
        }

        [Test]
        public void AppendToRoot_HasTarget_PlansRoot()
        {
            var plan = UxmlAttach.PlanAttach(UxmlAttachmentMode.AppendToRoot, true, true, 3);

            Assert.AreEqual(AttachOp.Root, plan.Op);
        }

        [Test]
        public void InsertBeforeElement_HasParent_PlansInsertAtIndex()
        {
            var plan = UxmlAttach.PlanAttach(UxmlAttachmentMode.InsertBeforeElement, true, true, 3);

            Assert.AreEqual(AttachOp.InsertAt, plan.Op);
            Assert.AreEqual(3, plan.Index);
        }

        [Test]
        public void InsertAfterElement_HasParent_PlansInsertAtIndexPlusOne()
        {
            var plan = UxmlAttach.PlanAttach(UxmlAttachmentMode.InsertAfterElement, true, true, 3);

            Assert.AreEqual(AttachOp.InsertAt, plan.Op);
            Assert.AreEqual(4, plan.Index);
        }

        [Test]
        public void InsertBeforeElement_NoParent_PlansRoot()
        {
            var plan = UxmlAttach.PlanAttach(UxmlAttachmentMode.InsertBeforeElement, true, false, 3);

            Assert.AreEqual(AttachOp.Root, plan.Op);
        }

        [Test]
        public void InsertAfterElement_NoParent_PlansRoot()
        {
            var plan = UxmlAttach.PlanAttach(UxmlAttachmentMode.InsertAfterElement, true, false, 3);

            Assert.AreEqual(AttachOp.Root, plan.Op);
        }
    }
}
