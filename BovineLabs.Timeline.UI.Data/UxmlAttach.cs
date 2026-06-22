namespace BovineLabs.Timeline.UI.Data
{
    public enum AttachOp : byte
    {
        Root,
        AppendChild,
        InsertAt
    }

    public struct AttachPlan
    {
        public AttachOp Op;
        public int Index;
    }

    public static class UxmlAttach
    {
        public static AttachPlan PlanAttach(UxmlAttachmentMode mode, bool hasTarget, bool hasParent, int targetIndex)
        {
            if (!hasTarget)
                return new AttachPlan { Op = AttachOp.Root };

            switch (mode)
            {
                case UxmlAttachmentMode.AppendToElement:
                    return new AttachPlan { Op = AttachOp.AppendChild };
                case UxmlAttachmentMode.InsertBeforeElement when hasParent:
                    return new AttachPlan { Op = AttachOp.InsertAt, Index = targetIndex };
                case UxmlAttachmentMode.InsertAfterElement when hasParent:
                    return new AttachPlan { Op = AttachOp.InsertAt, Index = targetIndex + 1 };
                default:
                    return new AttachPlan { Op = AttachOp.Root };
            }
        }
    }
}
