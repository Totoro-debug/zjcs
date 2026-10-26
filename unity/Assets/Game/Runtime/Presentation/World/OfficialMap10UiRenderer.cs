using System;

namespace Zjcs.Presentation.World
{
    /// <summary>Source compatibility for existing map import tools.</summary>
    [Obsolete("Use OfficialWorldUiRenderer for shared mainline map UI.")]
    public sealed class OfficialMap10UiRenderer : OfficialWorldUiRenderer
    {
        public OfficialMap10UiRenderer(OfficialMap10UiCatalog catalog) : base(catalog) { }
    }
}
