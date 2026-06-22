using Unity.Entities;

namespace BovineLabs.Timeline.UI.Data
{
    public static class ControllableSelection
    {
        public static Entity Select(Entity current, Entity candidate)
        {
            return current == Entity.Null || candidate.Index < current.Index ? candidate : current;
        }
    }
}
