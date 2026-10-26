using System;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>One filtered atlas material; UGUI retains ownership of stencil and clip variants.</summary>
    public abstract class NotebookAtlasGraphic : MaskableGraphic
    {
        static Material shared;
        public override Texture mainTexture => NotebookSpriteCatalog.Texture;
        public override Material defaultMaterial
        {
            get
            {
                if(shared==null) shared=Resources.Load<Material>("NotebookAtlasUi");
                if(shared==null) throw new InvalidOperationException("NotebookAtlasUi material is missing.");
                return shared;
            }
        }
    }
}
