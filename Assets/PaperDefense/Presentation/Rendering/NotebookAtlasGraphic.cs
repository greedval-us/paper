using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Native geometry uses Unity's standard maskable UI material.</summary>
    public abstract class NotebookAtlasGraphic : MaskableGraphic
    {
        public override Texture mainTexture => Texture2D.whiteTexture;
    }
}
