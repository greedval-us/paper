using System;
using System.Collections.Generic;
namespace PaperDefense.Core
{
    public sealed class FanGuide
    {
        public const int MaximumPoints=96;
        public IReadOnlyList<Point2> Points { get; }
        public float AngleDegrees { get; }
        public FanGuide(IEnumerable<Point2> points)
        {
            var owned=new List<Point2>();
            if(points==null)throw new ArgumentNullException(nameof(points));
            foreach(var p in points)
            {
                if(owned.Count>=MaximumPoints||!Finite(p.X)||!Finite(p.Y)||Math.Abs(p.X)>4||Math.Abs(p.Y)>4)
                    throw new ArgumentException(UserText.Message("volley.bad_points"));
                owned.Add(p);
            }
            if(owned.Count<2||owned[0].X*owned[0].X+owned[0].Y*owned[0].Y>.04f)
                throw new ArgumentException(UserText.Message("volley.start"));
            var end=owned[owned.Count-1];
            if(end.X*end.X+end.Y*end.Y<.25f)throw new ArgumentException(UserText.Message("volley.short"));
            AngleDegrees=(float)(Math.Atan2(end.X,end.Y)*180/Math.PI);
            if(Math.Abs(AngleDegrees)>85.00001f)throw new ArgumentException(UserText.Message("volley.front"));
            owned[0]=default;Points=owned.AsReadOnly();
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public static FanGuide FromAngle(float degrees)
        {
            if(!Finite(degrees)||Math.Abs(degrees)>85)throw new ArgumentException(UserText.Message("volley.front"));
            double angle=degrees*Math.PI/180;
            return new FanGuide(new[]{default(Point2),new Point2((float)Math.Sin(angle)*2.5f,(float)Math.Cos(angle)*2.5f)});
        }
    }
    public sealed class FanVolley
    {
        public const int MaximumGuides=6;
        public IReadOnlyList<FanGuide> Guides { get; }
        public FanVolley(IEnumerable<FanGuide> guides)
        {
            var owned=new List<FanGuide>();if(guides==null)throw new ArgumentNullException(nameof(guides));
            foreach(var guide in guides){if(guide==null||owned.Count>=MaximumGuides)throw new ArgumentException(UserText.Message("volley.count"));owned.Add(guide);}
            if(owned.Count==0)throw new ArgumentException(UserText.Message("volley.count"));
            owned.Sort((a,b)=>a.AngleDegrees.CompareTo(b.AngleDegrees));
            for(int i=1;i<owned.Count;i++)if(owned[i].AngleDegrees-owned[i-1].AngleDegrees<3.99999f)throw new ArgumentException(UserText.Message("volley.separation"));
            Guides=owned.AsReadOnly();
        }
        public int[] Allocate(int pellets)
        {
            if(pellets<Guides.Count||pellets>64)throw new ArgumentException(UserText.Message("volley.pellets"));
            var result=new int[Guides.Count];for(int i=0;i<result.Length;i++)result[i]=pellets/result.Length+(i<pellets%result.Length?1:0);return result;
        }
        public void Validate(WeaponDefinition weapon)
        {
            if(!weapon.SupportsVolley)throw new ArgumentException(UserText.Message("volley.weapon"));
            Allocate(weapon.VolleyPellets);
            foreach(var guide in Guides)if(Math.Abs(guide.AngleDegrees)>Math.Min(85,weapon.Stats.ConeAngleDegrees/2)+.00001f)
                throw new ArgumentException(UserText.Message("volley.angle",Math.Min(85,weapon.Stats.ConeAngleDegrees/2)));
        }
        public Point2 Direction(int guide,Point2 origin,Point2 aim)
        {
            double dx=(double)aim.X-origin.X,dy=(double)aim.Y-origin.Y,length=Math.Sqrt(dx*dx+dy*dy);
            if(length<.00001){dx=1;dy=0;}else{dx/=length;dy/=length;}
            double angle=Guides[guide].AngleDegrees*Math.PI/180;
            return new Point2((float)(dx*Math.Cos(angle)+dy*Math.Sin(angle)),(float)(dy*Math.Cos(angle)-dx*Math.Sin(angle)));
        }
    }
}
