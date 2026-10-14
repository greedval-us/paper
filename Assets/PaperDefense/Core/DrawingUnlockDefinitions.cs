using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public enum DrawingStencilRole { Body = 0, Barrel = 1 }
    [Serializable] public sealed class DrawingUnlockDefinitionInput
    {
        public string Id, Name, Description;
        public int GoldCost;
        public bool InitiallyUnlocked;
        public DrawingStencilRole Role;
        public List<DrawingContourInput> Contours = new List<DrawingContourInput>();
        public DrawingUnlockDefinition CreateSnapshot() => new DrawingUnlockDefinition(this);
        public DrawingUnlockDefinitionInput CreateCopy()
        {
            var copy=new DrawingUnlockDefinitionInput{Id=Id,Name=Name,Description=Description,GoldCost=GoldCost,InitiallyUnlocked=InitiallyUnlocked,Role=Role};
            if(Contours==null){copy.Contours=null;return copy;}
            foreach(var contour in Contours)
            {
                if(contour==null){copy.Contours.Add(null);continue;}
                var owned=new DrawingContourInput();
                if(contour.Points==null)owned.Points=null;
                else foreach(var point in contour.Points)owned.Points.Add(point==null?null:new DrawingCoordinateInput(point.X,point.Y));
                copy.Contours.Add(owned);
            }
            return copy;
        }
    }
    /// <summary>Reusable authoring geometry. Owning a stencil never changes combat statistics.</summary>
    public sealed class DrawingUnlockDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int GoldCost { get; }
        public bool InitiallyUnlocked { get; }
        public DrawingStencilRole Role { get; }
        public IReadOnlyList<DrawingStroke> Strokes { get; }
        internal DrawingUnlockDefinition(DrawingUnlockDefinitionInput input)
        {
            if(input==null)throw new ArgumentNullException(nameof(input));
            Id=ContentValidation.Id(input.Id,"drawingUnlock.id");
            if(string.IsNullOrWhiteSpace(input.Name))throw new ArgumentException("drawingUnlock.name: required");
            if(input.GoldCost<0)throw new ArgumentException("drawingUnlock.goldCost: must be nonnegative");
            ContentValidation.EnumValue(input.Role,"drawingUnlock.role");
            var template=new DrawingTemplateDefinitionInput{Id=Id,DisplayName=input.Name,Weight=1,Contours=input.Contours}.CreateSnapshot();
            var shape=FreeformGeometry.For(new DrawingSnapshot(new[]{new DrawingPart(input.Role==DrawingStencilRole.Body?DrawingBindings.Base:DrawingBindings.Mount0,template.Contours)})).Part(input.Role==DrawingStencilRole.Body?DrawingBindings.Base:DrawingBindings.Mount0);
            if(input.Role==DrawingStencilRole.Body?shape.Area<=0:shape.Length<=0 || shape.MuzzleOffset.Y<=0)throw new ArgumentException("drawingUnlock: stencil must have usable role geometry");
            Name=input.Name;Description=input.Description??"";GoldCost=input.GoldCost;InitiallyUnlocked=input.InitiallyUnlocked;Role=input.Role;Strokes=template.Contours;
        }
    }
    public static class DrawingUnlockDefaults
    {
        public static List<DrawingUnlockDefinitionInput> Create() => new List<DrawingUnlockDefinitionInput>{
            Shape("drawing.body.basic","body",DrawingStencilRole.Body,0,true,new[]{-.32f,-.35f,.32f,-.35f,.4f,-.27f,.4f,.27f,.32f,.35f,-.32f,.35f,-.4f,.27f,-.4f,-.27f,-.32f,-.35f}),
            Shape("drawing.barrel.basic","barrel",DrawingStencilRole.Barrel,0,true,new[]{-.1f,0,.1f,0,.07f,.6f,-.07f,.6f,-.1f,0}),
            Shape("drawing.sniper","sniper",DrawingStencilRole.Barrel,32,false,new[]{-.055f,0,.055f,0,.055f,.18f,.033f,.22f,.033f,.92f,-.033f,.92f,-.033f,.22f,-.055f,.18f,-.055f,0},new[]{-.055f,.12f,.055f,.12f},new[]{-.033f,.82f,.033f,.82f}),
            Shape("drawing.rapid","rapid",DrawingStencilRole.Barrel,24,false,new[]{-.055f,0,.055f,0,.055f,.24f,.038f,.3f,-.038f,.3f,-.055f,.24f,-.055f,0},new[]{-.055f,.10f,.055f,.10f},new[]{-.055f,.16f,.055f,.16f},new[]{-.055f,.22f,.055f,.22f}),
            Shape("drawing.heavy","heavy",DrawingStencilRole.Barrel,40,false,new[]{-.12f,0,.12f,0,.12f,.3f,.23f,.4f,.23f,.5f,.17f,.56f,-.17f,.56f,-.23f,.5f,-.23f,.4f,-.12f,.3f,-.12f,0},new[]{-.23f,.44f,.23f,.44f},new[]{-.12f,.16f,.12f,.16f})};
        static DrawingUnlockDefinitionInput Shape(string id,string text,DrawingStencilRole role,int cost,bool initial,params float[][] contours)
        {
            var result=new DrawingUnlockDefinitionInput{Id=id,Name=UserText.Message("drawing.stencil."+text+".name"),Description=UserText.Message("drawing.stencil."+text+".description"),Role=role,GoldCost=cost,InitiallyUnlocked=initial};
            foreach(var coordinates in contours)
            {
                var contour=new DrawingContourInput();
                for(int i=0;i<coordinates.Length;i+=2)contour.Points.Add(new DrawingCoordinateInput(coordinates[i],coordinates[i+1]));
                result.Contours.Add(contour);
            }
            return result;
        }
    }
}
