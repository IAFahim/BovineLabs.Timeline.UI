using BovineLabs.Anchor;
using Unity.Collections;
using Unity.Properties;

namespace BovineLabs.Timeline.UI.Data.ViewModel
{
    [IsService]
    public partial class EssenceUIViewModel : SystemObservableObject<EssenceUIViewModel.Data>
    {
        // Anchor UI binding doesn't natively support FixedString, so we expose it as a standard string
        [CreateProperty(ReadOnly = true)] 
        public string DumpText => Value.DumpText.ToString();
        
        [CreateProperty(ReadOnly = true)] 
        public bool IsVisible => Value.IsVisible;

        public partial struct Data
        {
            [SystemProperty] private FixedString4096Bytes dumpText;
            [SystemProperty] private bool isVisible;
        }
    }
}