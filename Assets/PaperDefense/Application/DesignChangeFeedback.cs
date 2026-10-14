using System;
using System.Globalization;
using PaperDefense.Core;

namespace PaperDefense.Application
{
    /// <summary>Explains prepared gameplay values. Does not re-interpret strokes or mutate a blueprint.</summary>
    public static class DesignChangeFeedback
    {
        public static string Footprint(MortarFootprint before, MortarFootprint after, float radius)
        {
            var a=Extent(before,radius);var b=Extent(after,radius);
            string effect=b.Y>a.Y+.005f&&b.X<a.X-.005f?"longer":b.X>a.X+.005f&&b.Y<a.Y-.005f?"wider":"reshaped";
            return UserText.Message("change.footprint",a.Y,b.Y,a.X,b.X,Math.PI*radius*radius,UserText.Message("change.footprint."+effect));
        }
        static Point2 Extent(MortarFootprint shape,float radius)
        {
            if(!shape.IsContour)return new Point2(2*radius/(float)Math.Sqrt(shape.AspectRatio),2*radius*(float)Math.Sqrt(shape.AspectRatio));
            float minX=float.PositiveInfinity,minY=minX,maxX=float.NegativeInfinity,maxY=maxX;
            foreach(var p in shape.Normalized){minX=Math.Min(minX,p.X);minY=Math.Min(minY,p.Y);maxX=Math.Max(maxX,p.X);maxY=Math.Max(maxY,p.Y);}
            return new Point2((maxX-minX)*radius,(maxY-minY)*radius);
        }
        public static string Volley(FanVolley before,FanVolley after,WeaponDefinition weapon)
        {
            // A saved page may outlive a catalog change. Keep its baseline without blocking repair.
            try{before?.Validate(weapon);after?.Validate(weapon);}
            catch(ArgumentException){return UserText.Message("change.volley.incompatible");}
            if(before==null&&after==null)return UserText.Message("change.volley.legacy_same");
            if(after==null)return UserText.Message("change.volley.to_legacy");
            if(before==null)return UserText.Message("change.volley.from_legacy",after.Guides.Count,weapon.VolleyPellets,Allocation(after,weapon.VolleyPellets));
            bool same=before.Guides.Count==after.Guides.Count;
            if(same)for(int i=0;i<before.Guides.Count;i++)same&=Math.Abs(before.Guides[i].AngleDegrees-after.Guides[i].AngleDegrees)<.0001f;
            string effect=same?"same":after.Guides.Count>before.Guides.Count?"split":after.Guides.Count<before.Guides.Count?"concentrated":"redirected";
            return UserText.Message("change.volley",before.Guides.Count,after.Guides.Count,weapon.VolleyPellets,
                Allocation(before,weapon.VolleyPellets),Allocation(after,weapon.VolleyPellets),Spread(before),Spread(after),UserText.Message("change.volley."+effect));
        }
        static float Spread(FanVolley volley)=>volley.Guides[volley.Guides.Count-1].AngleDegrees-volley.Guides[0].AngleDegrees;
        static string Allocation(FanVolley volley,int pellets)
        {var values=volley.Allocate(pellets);var parts=new string[values.Length];for(int i=0;i<values.Length;i++)parts[i]=values[i].ToString(CultureInfo.InvariantCulture);return string.Join("+",parts);}
        public static string Armor(ArmorDistribution before,ArmorDistribution after,float normal,float maximum,bool pending=false)
        {
            var values=new object[10];values[0]=UserText.Message(pending?"change.pending":"change.opening");bool stronger=false,weaker=false;
            for(int side=0;side<4;side++)
            {
                float a=Defense(before,side,normal,maximum),b=Defense(after,side,normal,maximum);
                values[1+side*2]=a*100;values[2+side*2]=b*100;stronger|=b>a+.00001f;weaker|=b<a-.00001f;
            }
            values[9]=UserText.Message(stronger&&weaker?"change.armor.trade":stronger?"change.armor.stronger":weaker?"change.armor.weaker":"change.armor.same");
            return UserText.Message("change.armor",values);
        }
        static float Defense(ArmorDistribution value,int side,float normal,float maximum)
        {int local=(side-(value?.FacingQuarterTurns??0)+4)%4;return DamageRules.EffectiveDefense(normal*(value==null?1:value.Points[local]/3f),maximum);}
        public static string Tower(TowerPreview before,TowerPreview after)
            =>UserText.Message("change.tower",before.Health,after.Health,before.Capacity,after.Capacity,before.Cost,after.Cost,before.BuildSeconds,after.BuildSeconds,after.Load);
        public static string Mount(WeaponPreview before,WeaponPreview after)
            =>UserText.Message("change.mount",after.SlotIndex+1,before.Damage,after.Damage,before.Range,after.Range,before.Interval,after.Interval);
        public static WeaponPreview? FindMount(TowerPreview preview,int slot)
        {if(preview.Weapons!=null)foreach(var item in preview.Weapons)if(item.SlotIndex==slot)return item;return null;}
    }
}
