using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Static notebook scenery from the same immutable grid used by placement and navigation.</summary>
    internal static class NotebookBattlefieldRenderer
    {
        internal static void Draw(MeshPen pen,BattlefieldView map,NotebookVisualThemeAsset theme,bool darkSurface=false,int hordeSides=0,int hordeBands=0)
        {
            if(map==null)return;
            theme=theme??NotebookVisualThemeAsset.Default;
            var roadFill=MeshPen.Alpha(NightNotebookEffectPalette.Tone(NotebookPalette.Blue,NightNotebookArt.Grid,theme,darkSurface),.035f);
            var roadEdge=MeshPen.Alpha(NightNotebookEffectPalette.Tone(NotebookPalette.Navy,NightNotebookArt.Ink,theme,darkSurface),.24f);
            foreach(var cell in map.Roads)
                pen.Rect(cell.X-.025f,cell.Y-.025f,1.05f,1.05f,roadFill);
            foreach(var cell in map.Roads)
            {
                float x=cell.X,y=cell.Y;
                if(!map.IsRoad(new Cell(cell.X-1,cell.Y)))pen.Line(new Vector2(x,y),new Vector2(x,y+1),.07f,roadEdge);
                if(!map.IsRoad(new Cell(cell.X+1,cell.Y)))pen.Line(new Vector2(x+1,y),new Vector2(x+1,y+1),.07f,roadEdge);
                if(!map.IsRoad(new Cell(cell.X,cell.Y-1)))pen.Line(new Vector2(x,y),new Vector2(x+1,y),.07f,roadEdge);
                if(!map.IsRoad(new Cell(cell.X,cell.Y+1)))pen.Line(new Vector2(x,y+1),new Vector2(x+1,y+1),.07f,roadEdge);
                if((cell.X+cell.Y)%3==0)
                    pen.Disc(new Vector2(x+.5f,y+.5f),.075f,MeshPen.Alpha(roadEdge,.45f),5,.65f);
            }
            int hatchBudget=512;
            if(map.Style==CampaignTerrainStyle.FourFields) CampaignTerrainDrawing.DrawTilledFields(pen,map,theme,darkSurface);
            foreach(var prop in map.Props)
            {
                if(map.Style!=CampaignTerrainStyle.Plain)
                {
                    CampaignTerrainDrawing.Draw(pen,map,prop,theme,darkSurface);
                    continue;
                }
                DrawOccupiedTerrain(pen,prop,theme,darkSurface,ref hatchBudget);
                var at=new Vector2(prop.Cell.X+prop.Width*.5f,prop.Cell.Y+prop.Height*.5f);
                string id=prop.Kind==TerrainPropKind.Mountain?"terrain.mountain":prop.Kind==TerrainPropKind.Pine?"terrain.pine":
                    prop.Kind==TerrainPropKind.Broadleaf?"terrain.oak":prop.Kind==TerrainPropKind.Rock?"terrain.rock":"terrain.shrub";
                if(prop.Kind==TerrainPropKind.Mountain&&(prop.Cell.X&1)==0)id="terrain.ridge";
                float scale=Mathf.Min(prop.Width/1.15f,prop.Height/1.05f);
                // Fit the authored silhouette to its occupied terrain rectangle. The
                // pine's actual width is .67 U, so an assumed 1.15 U left sparse dots.
                if(NotebookSpriteCatalog.TryGetBounds(id,out var bounds)&&bounds.width>0&&bounds.height>0)
                {
                    scale=.94f*Mathf.Min(prop.Width/bounds.width,prop.Height/bounds.height);
                    at-=bounds.center*scale;
                }
                NightNotebookEffectPalette.DrawResource(pen,id,at,theme,darkSurface,scale,opacity:.95f);
            }
            foreach(var cell in map.BarricadeSites)
            {
                // The cross marks the single legal blocking cell; a completed barricade covers it.
                NightNotebookEffectPalette.DrawResource(pen,"object.barricade-site",new Vector2(cell.X+.5f,cell.Y+.5f),
                    theme,darkSurface,.9f,opacity:.75f);
            }
            if(map.Style==CampaignTerrainStyle.RiverBridges) CampaignTerrainDrawing.DrawBridges(pen,map,theme,darkSurface);
            for(int entrance=0;entrance<map.Entrances.Count;entrance++)
            {
                var cell=map.Entrances[entrance];
                int side=map.Side(cell);
                bool incoming=hordeSides!=0 && map.IsWaveEntrance(entrance,hordeSides,hordeBands);
                float rotation=side==1?-Mathf.PI*.5f:side==2?Mathf.PI*.5f:side==4?Mathf.PI:0;
                NightNotebookEffectPalette.DrawResource(pen,"object.entrance",new Vector2(cell.X+.5f,cell.Y+.5f),
                    theme,darkSurface,2,rotation,opacity:hordeSides==0||incoming?1:.24f);
                if(hordeSides!=0&&!incoming)continue;
                float arrow=EntranceScale(pen);
                if(!incoming&&arrow<=2)continue;
                var direction=side==1?Vector2.right:side==2?Vector2.left:side==4?Vector2.down:Vector2.up;
                var across=new Vector2(-direction.y,direction.x);
                var at=new Vector2(cell.X+.5f,cell.Y+.5f);
                Color ink=MeshPen.Alpha(NightNotebookEffectPalette.Tone(NotebookPalette.Red,theme.Color(NotebookInkChannel.Danger),theme,darkSurface),.88f);
                var tip=at+direction*(arrow*.28f);
                pen.Line(at-direction*(arrow*.16f),tip,.035f,ink);
                pen.Line(tip,tip-direction*(arrow*.17f)+across*(arrow*.13f),.035f,ink);
                pen.Line(tip,tip-direction*(arrow*.17f)-across*(arrow*.13f),.035f,ink);
                if(incoming)
                {
                    var nose=at+direction*(arrow*.78f);
                    var rear=nose-direction*(arrow*.30f);
                    pen.Line(rear+across*(arrow*.25f),nose,arrow*.08f,ink);
                    pen.Line(nose,rear-across*(arrow*.25f),arrow*.08f,ink);
                }
            }
        }

        internal static float EntranceScale(MeshPen pen)
        {
            float unitsPerPixel=Mathf.Max(pen.WorldUnitsPerPixel,Mathf.Max(pen.ArtUnitsPerPixel,pen.MinimumLineWidth/.75f));
            // A compact 136 px mission preview fits a 64-cell map at about
            // 1.9 px/U. Let its decorative arrow reach ten pixels as well.
            return Mathf.Clamp(12*unitsPerPixel/.5f,2,12);
        }

        internal static void DrawOccupiedTerrain(MeshPen pen,TerrainPropView prop,NotebookVisualThemeAsset theme,
            bool darkSurface,ref int hatchBudget)
        {
            // The symbol describes the material; the full rectangle describes every
            // blocked cell. Long two-cell ridges must never look like one tiny rock.
            float left=prop.Cell.X,right=left+prop.Width,bottom=prop.Cell.Y,top=bottom+prop.Height;
            Color fill=MeshPen.Alpha(NightNotebookEffectPalette.Tone(NotebookPalette.Pencil,NightNotebookArt.Body,theme,darkSurface),.10f);
            Color edge=MeshPen.Alpha(NightNotebookEffectPalette.Tone(NotebookPalette.Navy,NightNotebookArt.Ink,theme,darkSurface),.32f);
            Color hatch=MeshPen.Alpha(NightNotebookEffectPalette.Tone(NotebookPalette.Pencil,NightNotebookArt.Grid,theme,darkSurface),.14f);
            pen.Rect(left,bottom,prop.Width,prop.Height,fill);
            float inset=Mathf.Min(.22f,Mathf.Max(.06f,
                Mathf.Max(pen.MinimumLineWidth,pen.MinimumStrokePixels*pen.WorldUnitsPerPixel)*.5f+pen.WorldUnitsPerPixel));
            left+=inset;right-=inset;bottom+=inset;top-=inset;
            if(right<=left||top<=bottom)return;
            pen.Line(new Vector2(left,bottom),new Vector2(right,bottom),.045f,edge);
            pen.Line(new Vector2(right,bottom),new Vector2(right,top),.045f,edge);
            pen.Line(new Vector2(right,top),new Vector2(left,top),.045f,edge);
            pen.Line(new Vector2(left,top),new Vector2(left,bottom),.045f,edge);
            float width=right-left,height=top-bottom;
            for(float offset=-height+1.4f;offset<width&&hatchBudget>0;offset+=1.4f)
            {
                Vector2 a=offset<0?new Vector2(left,bottom-offset):new Vector2(left+offset,bottom);
                Vector2 b=offset+height>width?new Vector2(right,bottom+width-offset):new Vector2(left+offset+height,top);
                if((b-a).sqrMagnitude<.01f)continue;
                pen.Line(a,b,.025f,hatch);hatchBudget--;
            }
        }
    }
}
