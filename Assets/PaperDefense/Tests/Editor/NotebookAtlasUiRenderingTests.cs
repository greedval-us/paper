using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class NotebookAtlasUiRenderingTests
    {
        [Test] public void SharedAtlasMaterialKeepsUguiStencilVariantsAndLeavesOrdinaryGraphicsAlone()
        {
            var shared=Resources.Load<Material>("NotebookAtlasUi"); Assert.That(shared,Is.Not.Null);
            var canvas=new GameObject("Atlas material fixture",typeof(RectTransform),typeof(Canvas));
            try
            {
                var mask=new GameObject("Stencil mask",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Mask));
                mask.transform.SetParent(canvas.transform,false);
                var child=new GameObject("Atlas child",typeof(RectTransform),typeof(CanvasRenderer),typeof(PaperDefense.Presentation.WeaponAssemblyGraphic));
                child.transform.SetParent(mask.transform,false);
                var graphic=child.GetComponent<PaperDefense.Presentation.WeaponAssemblyGraphic>();
                Assert.That(graphic.defaultMaterial,Is.SameAs(shared));
                Assert.That(graphic.mainTexture,Is.SameAs(PaperDefense.Presentation.NotebookSpriteCatalog.Texture));
                var stencil=graphic.materialForRendering;
                Assert.That(stencil.shader,Is.SameAs(shared.shader));
                Assert.That(stencil.GetInt("_StencilComp"),Is.EqualTo((int)CompareFunction.Equal));
                Assert.That(stencil.GetInt("_Stencil"),Is.EqualTo(1));
                Assert.That(mask.GetComponent<Image>().defaultMaterial,Is.Not.SameAs(shared));
                var text=new GameObject("Ordinary text",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));
                text.transform.SetParent(canvas.transform,false);
                Assert.That(text.GetComponent<Text>().defaultMaterial,Is.Not.SameAs(shared));
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        Shader Filter()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) Assert.Ignore("The pixel regression requires a graphics device.");
            var shader=Resources.Load<Shader>("NotebookAtlasUi");
            Assert.That(shader,Is.Not.Null,"Atlas-backed UI needs its bounded LOD0 filter shader.");
            Assert.That(shader.isSupported,Is.True); return shader;
        }

        [Test] public void TinySpriteNeverSamplesTheAdjacentColourAcrossTheFourTexelGutter()
        {
            var shader=Filter(); var texture=new Texture2D(256,256,TextureFormat.RGBA32,false,true);
            try
            {
                texture.filterMode=FilterMode.Bilinear; texture.wrapMode=TextureWrapMode.Clamp;
                var pixels=new Color[256*256];
                for(int y=16;y<176;y++)
                {
                    for(int x=16;x<176;x++) pixels[y*256+x]=Color.blue;
                    for(int x=180;x<256;x++) pixels[y*256+x]=Color.red;
                }
                texture.SetPixels(pixels); texture.Apply(false);
                // The final covered pixel lies .64 source texels from the sprite's edge.
                // An unbounded quarter-screen-pixel tap would reach the red neighbour.
                var actual=Render(shader,texture,new Rect(30,30,2.51f,2.51f),new Rect(16f/256,16f/256,160f/256,160f/256));
                var edge=actual[31*64+32];
                Assert.That(actual[31*64+31].a,Is.GreaterThan(.99f),"The opaque sprite centre must remain opaque.");
                Assert.That(actual[31*64+31].b,Is.GreaterThan(.99f),"The opaque centre must retain the source blue.");
                Assert.That(edge.a,Is.GreaterThan(.01f),"The final real sprite pixel must still contribute coverage.");
                Assert.That(edge.r,Is.LessThan(.01f),"A tiny blue sprite must not inherit the next atlas entry's red pixels.");
                // Averaging opaque blue (0,0,1,1) with clear gutter (0,0,0,0) gives (0,0,a,a).
                // UGUI then premultiplies RGB, so the exact blue over a clear target is a*a.
                Assert.That(edge.b,Is.EqualTo(edge.a*edge.a).Within(2f/255),"Edge RGBA: "+edge);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [TestCase(true,false)] [TestCase(false,true)]
        public void FilterPreservesTheIntersectionOfStencilMaskAndRectClip(bool clip,bool stencil)
        {
            var filter=Filter(); var standard=Shader.Find("UI/Default"); Assert.That(standard,Is.Not.Null);
            string description="";
            var expected=Render(standard,Texture2D.whiteTexture,new Rect(0,0,64,64),new Rect(0,0,1,1),clip,stencil,
                describe:target=>description="device="+SystemInfo.graphicsDeviceType+", depthStencil="+target.depthStencilFormat+", clip="+clip+", stencil="+stencil);
            Assert.That(expected[32*64+24].a,Is.GreaterThan(.99f),"Stock UI fixture must draw its interior first; "+description);
            var actual=Render(filter,Texture2D.whiteTexture,new Rect(0,0,64,64),new Rect(0,0,1,1),clip,stencil);
            Assert.That(actual[32*64+24].a,Is.GreaterThan(.99f),"Filtered interior; "+description);
            if(stencil)
            {
                Assert.That(expected[32*64+40].a,Is.LessThan(.01f),"Stock stencil exterior; "+description);
                Assert.That(actual[32*64+40].a,Is.LessThan(.01f),"Filtered stencil exterior; "+description);
            }
            if(clip)
            {
                Assert.That(expected[32*64+8].a,Is.LessThan(.01f),"Stock clip exterior; "+description);
                Assert.That(actual[32*64+8].a,Is.LessThan(.01f),"Filtered clip exterior; "+description);
                Assert.That(actual[8*64+24].a,Is.LessThan(.01f),"Below the clip rectangle; "+description);
            }
        }

        [Test] public void CameraCanvasCombinesTheRealStencilMaskAndRectMask2D()
        {
            Filter(); var standard=Shader.Find("UI/Default"); Assert.That(standard,Is.Not.Null);
            var stockMaterial=new Material(standard);
            try
            {
                var expected=RenderCombinedCanvas(stockMaterial);
                var actual=RenderCombinedCanvas(null);
                // Test visible coverage: camera pipelines may intentionally preserve or replace target alpha.
                foreach(var point in new[]{new Vector2Int(24,32),new Vector2Int(40,32),new Vector2Int(8,32),new Vector2Int(24,8)})
                {
                    int index=point.y*64+point.x;
                    float visible=point==new Vector2Int(24,32)?1:0;
                    Assert.That(expected[index].r,Is.EqualTo(visible).Within(2f/255),"Stock Camera/Canvas mask coverage at "+point);
                    Assert.That(actual[index].r,Is.EqualTo(visible).Within(2f/255),"Filtered Camera/Canvas mask coverage at "+point);
                    Assert.That(actual[index].g,Is.EqualTo(expected[index].g).Within(2f/255));
                    Assert.That(actual[index].b,Is.EqualTo(expected[index].b).Within(2f/255));
                    Assert.That(actual[index].a,Is.EqualTo(expected[index].a).Within(2f/255));
                }
            }
            finally { Object.DestroyImmediate(stockMaterial); }
        }

        static Color[] RenderCombinedCanvas(Material overrideMaterial)
        {
            var previous=RenderTexture.active;
            var target=new RenderTexture(64,64,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var readback=new Texture2D(64,64,TextureFormat.RGBA32,false,true);
            var owner=new GameObject("Isolated Camera Canvas mask fixture");
            try
            {
                Assert.That(target.Create(),Is.True);
                var cameraObject=new GameObject("UI capture camera",typeof(Camera)); cameraObject.transform.SetParent(owner.transform,false);
                var camera=cameraObject.GetComponent<Camera>(); camera.enabled=false; camera.targetTexture=target;
                camera.orthographic=true; camera.orthographicSize=32; camera.nearClipPlane=.1f; camera.farClipPlane=100;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.clear; camera.cullingMask=1<<31;
                var canvasObject=new GameObject("Actual Canvas",typeof(RectTransform),typeof(Canvas));
                canvasObject.layer=31; canvasObject.transform.SetParent(owner.transform,false);
                var canvas=canvasObject.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera;
                canvas.worldCamera=camera; canvas.planeDistance=10; canvas.scaleFactor=1;
                RectTransform Rect(Transform parent,string name,Vector2 at,Vector2 size,params Type[] components)
                {
                    var go=new GameObject(name,typeof(RectTransform)); go.layer=31; go.transform.SetParent(parent,false);
                    var rect=go.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;
                    rect.anchoredPosition=at; rect.sizeDelta=size;
                    foreach(var component in components) go.AddComponent(component);
                    return rect;
                }
                var mask=Rect(canvas.transform,"Actual Mask",Vector2.zero,new Vector2(32,64),typeof(CanvasRenderer),typeof(Image),typeof(Mask));
                mask.GetComponent<Image>().color=Color.white; mask.GetComponent<Mask>().showMaskGraphic=false;
                var clip=Rect(mask,"Actual RectMask2D",new Vector2(16,16),new Vector2(32,32),typeof(RectMask2D));
                var child=Rect(clip,"Actual atlas graphic",new Vector2(-16,-16),new Vector2(64,64),typeof(CanvasRenderer),typeof(NotebookAtlasUiFixtureGraphic));
                if(overrideMaterial!=null) child.GetComponent<NotebookAtlasUiFixtureGraphic>().material=overrideMaterial;
                Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active=target; readback.ReadPixels(new Rect(0,0,64,64),0,0); readback.Apply(false);
                return readback.GetPixels();
            }
            finally
            {
                RenderTexture.active=previous; Object.DestroyImmediate(owner);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(readback);
            }
        }

        [TestCase(0)] [TestCase(1)]
        public void UniformSamplingPreservesDefaultUiColourAlphaAndCanvasGammaContract(int gammaVertices)
        {
            var filter=Filter(); var standard=Shader.Find("UI/Default"); Assert.That(standard,Is.Not.Null);
            int previous=Shader.GetGlobalInt("_UIVertexColorAlwaysGammaSpace");
            try
            {
                Shader.SetGlobalInt("_UIVertexColorAlwaysGammaSpace",gammaVertices);
                var colour=new Color(.2f,.4f,.6f,.5f);
                var expected=Render(standard,Texture2D.whiteTexture,new Rect(0,0,64,64),new Rect(.5f,.5f,0,0),colour:colour)[32*64+32];
                var actual=Render(filter,Texture2D.whiteTexture,new Rect(0,0,64,64),new Rect(.5f,.5f,0,0),colour:colour)[32*64+32];
                Assert.That(actual.r,Is.EqualTo(expected.r).Within(2f/255));
                Assert.That(actual.g,Is.EqualTo(expected.g).Within(2f/255));
                Assert.That(actual.b,Is.EqualTo(expected.b).Within(2f/255));
                Assert.That(actual.a,Is.EqualTo(expected.a).Within(2f/255));
            }
            finally { Shader.SetGlobalInt("_UIVertexColorAlwaysGammaSpace",previous); }
        }

        static Color[] Render(Shader shader,Texture texture,Rect area,Rect uv,bool clip=false,bool stencil=false,Color? colour=null,Action<RenderTexture> describe=null)
        {
            var previous=RenderTexture.active; float previousZ=Shader.GetGlobalFloat("unity_GUIZTestMode");
            var previousScreen=Shader.GetGlobalVector("_ScreenParams");
            var target=new RenderTexture(64,64,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var readback=new Texture2D(64,64,TextureFormat.RGBA32,false,true);
            var material=new Material(shader){mainTexture=texture};
            Material writer=null,reader=null; Mesh quad=null,mask=null;
            try
            {
                Assert.That(target.Create(),Is.True); RenderTexture.active=target;
                describe?.Invoke(target);
                Shader.SetGlobalFloat("unity_GUIZTestMode",(float)CompareFunction.Always);
                // No Camera populates built-in uniforms in this immediate offscreen fixture.
                // UI mask softness derives pixel size from the projection and target dimensions.
                Shader.SetGlobalVector("_ScreenParams",new Vector4(64,64,1+1f/64,1+1f/64));
                GL.Clear(true,true,Color.clear);
                if(clip)
                {
                    material.EnableKeyword("UNITY_UI_CLIP_RECT");
                    material.SetVector("_ClipRect",new Vector4(16,16,48,48));
                }
                if(stencil)
                {
                    writer=StencilMaterial.Add(material,1,StencilOp.Replace,CompareFunction.Always,(ColorWriteMask)0,255,255);
                    if(clip)
                    {
                        // Keep the reader's clip variant, with an independent unclipped writer rectangle.
                        // No keyword/cache mutation occurs between the two immediate draw commands.
                        writer.SetVector("_ClipRect",new Vector4(-32767,-32767,32767,32767));
                    }
                    reader=StencilMaterial.Add(material,1,StencilOp.Keep,CompareFunction.Equal,ColorWriteMask.All,1,0);
                    mask=Quad(new Rect(0,0,32,64),new Rect(.5f,.5f,0,0),Color.white);
                    Draw(writer,mask);
                }
                quad=Quad(area,uv,colour??Color.white); Draw(reader??material,quad);
                readback.ReadPixels(new Rect(0,0,64,64),0,0); readback.Apply(false);
                return readback.GetPixels();
            }
            finally
            {
                StencilMaterial.Remove(writer); StencilMaterial.Remove(reader);
                RenderTexture.active=previous; Shader.SetGlobalFloat("unity_GUIZTestMode",previousZ);
                Shader.SetGlobalVector("_ScreenParams",previousScreen);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(readback);
                Object.DestroyImmediate(material); if(quad!=null)Object.DestroyImmediate(quad); if(mask!=null)Object.DestroyImmediate(mask);
            }
        }

        static void Draw(Material material,Mesh mesh)
        {
            GL.PushMatrix();
            try { GL.LoadPixelMatrix(0,64,0,64); Assert.That(material.SetPass(0),Is.True); Graphics.DrawMeshNow(mesh,Matrix4x4.identity); }
            finally { GL.PopMatrix(); }
        }
        static Mesh Quad(Rect r,Rect uv,Color colour)
        {
            var mesh=new Mesh();
            mesh.vertices=new[]{new Vector3(r.xMin,r.yMin,0),new Vector3(r.xMax,r.yMin,0),new Vector3(r.xMax,r.yMax,0),new Vector3(r.xMin,r.yMax,0)};
            mesh.uv=new[]{uv.min,new Vector2(uv.xMax,uv.yMin),uv.max,new Vector2(uv.xMin,uv.yMax)};
            mesh.colors=new[]{colour,colour,colour,colour}; mesh.triangles=new[]{0,1,2,0,2,3}; return mesh;
        }
    }

    /// <summary>Real atlas and shared material, with constant white-texel UVs to isolate mask coverage.</summary>
    public sealed class NotebookAtlasUiFixtureGraphic : PaperDefense.Presentation.NotebookAtlasGraphic
    {
        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); var r=rectTransform.rect; var uv=PaperDefense.Presentation.NotebookSpriteCatalog.WhiteUV;
            var points=new[]{r.min,new Vector2(r.xMax,r.yMin),r.max,new Vector2(r.xMin,r.yMax)};
            var vertices=new UIVertex[4];
            for(int i=0;i<4;i++)
            { vertices[i]=UIVertex.simpleVert; vertices[i].position=points[i]; vertices[i].color=Color.white; vertices[i].uv0=uv; }
            target.AddUIVertexQuad(vertices);
        }
    }
}
