using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Static notebook scenery from the same immutable grid used by placement and navigation.</summary>
    internal static class NotebookBattlefieldRenderer
    {
        internal static void Draw(MeshPen pen,BattlefieldView map,NotebookVisualThemeAsset theme)
        {
            if(map==null)return;
            theme=theme??NotebookVisualThemeAsset.Default;
            var roadFill=new Color(.84f,.76f,.59f,.48f);
            var roadEdge=new Color(.43f,.37f,.26f,.43f);
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
            foreach(var prop in map.Props)
            {
                var at=new Vector2(prop.Cell.X+prop.Width*.5f,prop.Cell.Y+prop.Height*.5f);
                string id=prop.Kind==TerrainPropKind.Mountain?"terrain.mountain":prop.Kind==TerrainPropKind.Pine?"terrain.pine":
                    prop.Kind==TerrainPropKind.Broadleaf?"terrain.oak":prop.Kind==TerrainPropKind.Rock?"terrain.rock":"terrain.shrub";
                if(prop.Kind==TerrainPropKind.Mountain&&(prop.Cell.X&1)==0)id="terrain.ridge";
                pen.Rect(prop.Cell.X,prop.Cell.Y,prop.Width,prop.Height,new Color(.39f,.50f,.40f,.06f));
                float scale=Mathf.Min(prop.Width/1.15f,prop.Height/1.05f);
                NotebookArtRenderer.Draw(pen,theme?.FindShape(id),at,scale,theme:theme);
            }
            foreach(var cell in map.BarricadeSites)
            {
                // The cross marks the single legal blocking cell; a completed barricade covers it.
                NotebookArtRenderer.Draw(pen,theme?.FindShape("object.barricade-site"),new Vector2(cell.X+.5f,cell.Y+.5f),
                    .9f,opacity:.75f,theme:theme);
            }
            foreach(var cell in map.Entrances)
            {
                int side=map.Side(cell);
                float rotation=side==1?Mathf.PI*.5f:side==2?-Mathf.PI*.5f:side==8?Mathf.PI:0;
                NotebookArtRenderer.Draw(pen,theme?.FindShape("object.entrance"),new Vector2(cell.X+.5f,cell.Y+.5f),
                    2,rotation,theme:theme);
            }
        }
    }
}
