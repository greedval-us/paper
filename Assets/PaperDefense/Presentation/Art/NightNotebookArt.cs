using System;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Readable paper machines inside the existing certified part envelopes.</summary>
    public static partial class NightNotebookArt
    {
        public const string Revision = "paper-mechanisms-v5";
        public static readonly Color Paper = new Color32(20, 44, 38, 255);
        public static readonly Color Ink = new Color32(241, 231, 203, 255);
        public static readonly Color Pencil = new Color32(182, 198, 171, 255);
        // Material stays below the ivory contour without changing shared sketch ink.
        public static readonly Color Body = new Color32(155, 168, 145, 255);
        public static readonly Color Accent = new Color32(214, 171, 93, 255);
        internal static readonly Color GlassInk = new Color32(101, 175, 177, 255);
        internal static readonly Color Copper = new Color32(187, 131, 87, 255);
        internal static readonly Color BladeSteel = new Color32(124, 151, 167, 255);
        internal static readonly Color RedEnamel = new Color32(214, 135, 111, 255);
        public static readonly Color Grid = new Color32(145, 176, 155, 255);

        // Deliberately uneven corners belong to the drawing, never to the mechanical bounds.
        static readonly float[] Plate = { .06f,.20f, .18f,.06f, .83f,.08f, .95f,.24f, .93f,.82f, .80f,.94f, .15f,.92f, .05f,.75f };
        static readonly float[] LightPlate = { .10f,.16f, .80f,.08f, .94f,.32f, .84f,.91f, .17f,.93f, .05f,.66f };
        static readonly float[] Frame = { .04f,.26f, .13f,.08f, .88f,.10f, .96f,.29f, .90f,.52f, .10f,.50f };
        static readonly float[] HeavyFrame = { .04f,.25f, .17f,.07f, .84f,.06f, .96f,.27f, .91f,.64f, .08f,.66f };
        static readonly float[] Shield = { .13f,.08f, .79f,.05f, .94f,.26f, .85f,.87f, .48f,.96f, .07f,.75f };
        static readonly float[] Diamond = { .49f,.04f, .95f,.48f, .51f,.95f, .05f,.53f };
        static readonly float[] Fan = { .30f,.05f, .73f,.07f, .96f,.87f, .82f,.96f, .09f,.92f, .04f,.77f };
        static readonly float[] Barrel = { .31f,.05f, .70f,.04f, .76f,.89f, .66f,.96f, .29f,.94f, .25f,.15f };
        static readonly float[] ShortBarrel = { .22f,.06f, .81f,.08f, .88f,.78f, .77f,.95f, .17f,.93f, .11f,.22f };
        static readonly float[] LongBarrel = { .40f,.04f, .64f,.05f, .69f,.94f, .34f,.96f };
        static readonly float[] WideBarrel = { .33f,.06f, .73f,.05f, .96f,.85f, .89f,.95f, .10f,.93f, .04f,.80f };
        static readonly float[] Staple = { .12f,.07f, .88f,.09f, .92f,.94f, .68f,.95f, .65f,.35f, .35f,.34f, .33f,.95f, .07f,.93f };
        static readonly float[] Bracket = { .08f,.08f, .30f,.07f, .34f,.69f, .71f,.70f, .74f,.09f, .94f,.11f, .92f,.94f, .10f,.93f };

        internal static void DrawPart(AssemblyVisualFragment fragment, MeshPen pen, Vector2 at,
            float opacity, Color tint, float uniformScale, AssemblyMechanicsPose mechanics = default)
        {
            if (pen == null || fragment.Region.width <= 0 || fragment.Region.height <= 0) return;
            var p = new Drawing(fragment, pen, at, opacity, tint, uniformScale, mechanics);
            string id = fragment.PartId ?? "";
            switch (Category(fragment))
            {
                case ComponentCategory.Frame: DrawFrame(p, id); break;
                case ComponentCategory.Hull: DrawHull(p, id); break;
                case ComponentCategory.Armor: DrawArmor(p, id); break;
                case ComponentCategory.Feed: DrawFeed(p, id); break;
                case ComponentCategory.Turn: DrawTurn(p, id); break;
                case ComponentCategory.Mechanism: DrawMechanism(p, id); break;
                case ComponentCategory.Head: DrawHead(p, id); break;
                case ComponentCategory.Sight: DrawSight(p, id); break;
                case ComponentCategory.Magazine: DrawMagazine(p, id); break;
                case ComponentCategory.Trigger: DrawTrigger(p, id); break;
                case ComponentCategory.Site: DrawSite(p, id); break;
                case ComponentCategory.Function: DrawFunction(p, id); break;
                case ComponentCategory.Shell: DrawShell(p, id); break;
                case ComponentCategory.Utility: DrawUtility(p, id); break;
                case ComponentCategory.Mount: DrawMount(p, id); break;
                default: p.Outline(Plate); p.Connect(.5f, .08f, .5f, .22f); break;
            }
        }

        static bool Has(string id, string value) => id.IndexOf(value, StringComparison.Ordinal) >= 0;
        static ComponentCategory Category(AssemblyVisualFragment f)
        {
            string id = f.PartId ?? "";
            if (id.StartsWith("frame.", StringComparison.Ordinal)) return ComponentCategory.Frame;
            if (id.StartsWith("hull.", StringComparison.Ordinal)) return ComponentCategory.Hull;
            if (id.StartsWith("armor.", StringComparison.Ordinal)) return ComponentCategory.Armor;
            if (id.StartsWith("feed.", StringComparison.Ordinal)) return ComponentCategory.Feed;
            if (id.StartsWith("turn.", StringComparison.Ordinal)) return ComponentCategory.Turn;
            if (id.StartsWith("core.", StringComparison.Ordinal)) return ComponentCategory.Mechanism;
            if (id.StartsWith("head.", StringComparison.Ordinal)) return ComponentCategory.Head;
            if (id.StartsWith("sight.", StringComparison.Ordinal)) return ComponentCategory.Sight;
            if (id.StartsWith("magazine.", StringComparison.Ordinal)) return ComponentCategory.Magazine;
            if (id.StartsWith("trigger.", StringComparison.Ordinal)) return ComponentCategory.Trigger;
            if (id.StartsWith("site.", StringComparison.Ordinal)) return ComponentCategory.Site;
            if (id.StartsWith("function.", StringComparison.Ordinal)) return ComponentCategory.Function;
            if (id.StartsWith("shell.", StringComparison.Ordinal)) return ComponentCategory.Shell;
            if (id.StartsWith("utility.", StringComparison.Ordinal)) return ComponentCategory.Utility;
            if (id.StartsWith("mount.", StringComparison.Ordinal)) return ComponentCategory.Mount;
            // New catalogue IDs still receive the drawing of their existing category/layer.
            if (f.SlotIndex >= 0 && f.Layer == 5) return ComponentCategory.Turn;
            int category = f.SlotIndex >= 0 && f.Layer >= 6 ? f.Layer - 6 : f.Layer;
            return category >= 0 && category <= (int)ComponentCategory.Mount ? (ComponentCategory)category : ComponentCategory.Hull;
        }

        static void DrawUtility(Drawing p, string id)
        {
            if(Has(id,"armor"))
            { p.Outline(Shield);p.Seam(.48f,.20f,.48f,.78f);p.Seam(.21f,.38f,.77f,.38f);p.AccentBox(.40f,.46f,.16f,.08f); }
            else if(Has(id,"range"))
            {
                p.SolidBox(.12f,.08f,.29f,.16f);p.Beam(.26f,.22f,.30f,.56f,.15f);
                p.Beam(.30f,.56f,.73f,.80f,.14f);p.Disc(.30f,.56f,.11f,.11f);
                p.Beam(.73f,.80f,.78f,.58f,.11f);
                p.SolidBox(.66f,.50f,.25f,.09f);p.SolidBox(.66f,.44f,.06f,.10f);p.SolidBox(.85f,.44f,.06f,.10f);
                p.AccentBox(.25f,.53f,.10f,.05f);
            }
            else if(Has(id,"economy"))
            {
                p.SolidBox(.11f,.10f,.78f,.14f);p.SolidBox(.19f,.26f,.29f,.48f);
                p.Disc(.335f,.74f,.145f,.08f);p.SolidBox(.29f,.81f,.09f,.11f);
                p.Beam(.48f,.38f,.70f,.38f,.10f);p.SolidBox(.61f,.38f,.18f,.37f);
                p.SolidBox(.66f,.76f,.08f,.15f);p.SolidBox(.56f,.87f,.29f,.06f);
                p.Seam(.26f,.40f,.41f,.40f);p.AccentBox(.26f,.51f,.15f,.08f);
            }
            else
            {
                // A reservoir narrows into the working nozzle rather than a target symbol.
                p.Disc(.50f,.70f,.29f,.22f);p.SolidBox(.28f,.45f,.44f,.21f);
                p.Beam(.50f,.48f,.50f,.22f,.18f);p.SolidBox(.41f,.11f,.18f,.13f);
                p.SolidBox(.34f,.36f,.32f,.07f);p.AccentBox(.42f,.64f,.16f,.06f);
                p.Seam(.44f,.18f,.56f,.18f);
            }
        }
        readonly struct Drawing
        {
            readonly AssemblyVisualFragment fragment;
            readonly MeshPen pen;
            readonly Rect area;
            readonly Vector2 at;
            readonly float uniformScale, width, manualWidth, minimumFoldSpan;
            readonly Color ink, seam, pencil, accent, wash, paper, body, foldShadow;
            readonly bool manual, detailed, seams;
            readonly AssemblyMechanicsPose mechanics;
            readonly bool magazine;
            internal float Fire => mechanics.Fire;
            internal float Activity => mechanics.Activity;
            internal float Reload => mechanics.Reload;
            internal bool Reloading => mechanics.Reloading;
            internal AssemblyWeaponFamily WeaponFamily => fragment.WeaponFamily;
            internal bool Tiny { get; }

            internal Drawing(AssemblyVisualFragment fragment, MeshPen pen, Vector2 at, float opacity, Color tint, float uniformScale,
                AssemblyMechanicsPose mechanics)
            {
                this.mechanics = mechanics;
                this.fragment=fragment; this.pen=pen; this.at=at; this.uniformScale=uniformScale;
                float scale=Mathf.Max(.000001f,Mathf.Abs(fragment.Scale*uniformScale));
                float span=Mathf.Min(fragment.Region.width,fragment.Region.height)*scale;
                var category=Category(fragment);
                magazine = category == ComponentCategory.Magazine;
                bool band=category==ComponentCategory.Frame || category==ComponentCategory.Hull ||
                    category==ComponentCategory.Site || category==ComponentCategory.Shell;
                width=Mathf.Min(.010f*uniformScale,span*(band?.09f:.065f));
                float floor=Mathf.Max(Mathf.Max(pen.MinimumLineWidth,pen.MinimumStrokePixels*pen.WorldUnitsPerPixel),pen.ArtUnitsPerPixel*.85f);
                float spread=Mathf.Max(width,floor)*.5f+pen.WorldUnitsPerPixel;
                float detailUnits=Mathf.Max(pen.WorldUnitsPerPixel,pen.ArtUnitsPerPixel);
                Tiny=detailUnits>0 && span/detailUnits<11;
                bool smallSupport = Tiny && (category == ComponentCategory.Site || category == ComponentCategory.Function ||
                    category == ComponentCategory.Shell || category == ComponentCategory.Utility);
                // A full pixel rim would eat the slit between neighboring spike blades
                // and the open jaw of the crane when a mechanism is only six pixels tall.
                manual=spread>span*.20f || smallSupport;
                manualWidth=span*(smallSupport ? .065f : .15f);
                float padding=(manual?manualWidth*.6f:spread)+Mathf.Min(.0001f,span*.01f);
                float inset=padding/scale;
                area=new Rect(fragment.Region.xMin+inset,fragment.Region.yMin+inset,
                    Mathf.Max(.000001f,fragment.Region.width-inset*2),Mathf.Max(.000001f,fragment.Region.height-inset*2));
                float alpha=Mathf.Clamp01(opacity)*tint.a;
                ink=tint==Color.white?Ink:tint; ink.a=alpha;
                if (mechanics.Hit > 0) { ink=Color.Lerp(ink,Accent,mechanics.Hit*.65f); ink.a=alpha; }
                seam=Paper; seam.a=alpha*.92f;
                pencil=Paper; pencil.a=alpha*.62f;
                accent=Accent; accent.a=alpha*.90f;
                body=Body; body.a=alpha*.96f;
                if (mechanics.Hit > 0) { body=Color.Lerp(body,Ink,mechanics.Hit*.40f); body.a=alpha*.96f; }
                wash=body;
                paper=Paper; paper.a=alpha;
                foldShadow=Paper; foldShadow.a=alpha*.23f;
                minimumFoldSpan=Mathf.Max(detailUnits*8, width*3);
                seams=!manual && (detailUnits<=0 || span/detailUnits>=11);
                detailed=!manual && (detailUnits<=0 || span/detailUnits>=18)
                    && Mathf.Min(area.width,area.height)>=.04f;
            }
            Vector2 Point(float x,float y)
            {
                if (magazine && mechanics.Reloading)
                    y=Mathf.Clamp01(y + Mathf.Sin(mechanics.Reload*Mathf.PI)*.16f);
                return at+fragment.Transform(new Vector2(area.xMin+area.width*x,area.yMin+area.height*y))*uniformScale;
            }
            internal void Hinged(float[] points, float x, float y, float angle)
            {
                int count=points.Length/2; Vector2 center=Vector2.zero;
                for(int i=0;i<count;i++) center+=HingePoint(points[i*2],points[i*2+1],x,y,angle);
                center/=count;
                for(int i=0;i<count;i++)
                {
                    int next=(i+1)%count;
                    Vector2 a=HingePoint(points[i*2],points[i*2+1],x,y,angle);
                    Vector2 b=HingePoint(points[next*2],points[next*2+1],x,y,angle);
                    pen.Triangle(center,a,b,body); StrokePoints(a,b,ink,1);
                }
            }
            Vector2 HingePoint(float px,float py,float x,float y,float angle)
            {
                float dx=px-x,dy=py-y,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
                return Point(Mathf.Clamp01(x+dx*c-dy*s),Mathf.Clamp01(y+dx*s+dy*c));
            }
            internal void Rotor(float cx,float cy,float radius,float phase)
            {
                float angle=phase*Mathf.PI*2;
                Connect(cx-Mathf.Cos(angle)*radius,cy-Mathf.Sin(angle)*radius,
                    cx+Mathf.Cos(angle)*radius,cy+Mathf.Sin(angle)*radius);
            }
            Vector2 FullPoint(float x,float y)
                => at+fragment.Transform(new Vector2(fragment.Region.xMin+fragment.Region.width*x,
                    fragment.Region.yMin+fragment.Region.height*y))*uniformScale;
            internal void BandBox(float x,float y,float w,float h,bool dark=false)
                => MaterialPanel(FullPoint(x,y),FullPoint(x+w,y),FullPoint(x+w,y+h),FullPoint(x,y+h),dark);
            Vector2 LocalPoint(float x,float y)
                => at+fragment.Transform(new Vector2(Mathf.Clamp(x,area.xMin,area.xMax),Mathf.Clamp(y,area.yMin,area.yMax)))*uniformScale;
            void Stroke(float ax,float ay,float bx,float by,Color color,float weight=1)
                => StrokePoints(Point(ax,ay),Point(bx,by),color,weight);
            void StrokePoints(Vector2 a,Vector2 b,Color color,float weight)
            {
                if (!manual) { pen.Line(a,b,width*weight,color); return; }
                Vector2 delta=b-a;
                if (delta.sqrMagnitude<.0000000001f) return;
                Vector2 side=new Vector2(-delta.y,delta.x).normalized*manualWidth*weight*.5f;
                pen.Quad(a-side,b-side,b+side,a+side,color);
            }
            internal void Line(float ax,float ay,float bx,float by,bool fine=false)
                => Stroke(ax,ay,bx,by,fine?pencil:ink,fine?.37f:1);
            internal void Seam(float ax,float ay,float bx,float by)
            { if(seams) Stroke(ax,ay,bx,by,seam,.37f); }
            internal void Detail(float ax,float ay,float bx,float by)
            { if(detailed) Stroke(ax,ay,bx,by,pencil,.33f); }
            internal void Connect(float ax,float ay,float bx,float by) => Stroke(ax,ay,bx,by,accent,.70f);
            internal bool HasSupport => fragment.Region.yMin < -.065f;
            internal float SupportBottom => area.yMin;
            internal void LocalLine(float ax,float ay,float bx,float by,bool fine=false)
                => StrokePoints(LocalPoint(ax,ay),LocalPoint(bx,by),fine?pencil:ink,fine?.37f:1);
            internal void LocalSeam(float ax,float ay,float bx,float by)
            { if(seams) StrokePoints(LocalPoint(ax,ay),LocalPoint(bx,by),seam,.37f); }
            internal void LocalConnect(float ax,float ay,float bx,float by)
                => StrokePoints(LocalPoint(ax,ay),LocalPoint(bx,by),accent,.70f);
            internal void LocalEllipse(float cx,float cy,float rx,float ry,bool fine=false)
                => EllipseRing(cx,cy,rx,ry,fine,true);
            void Fill(float[] points,Color color)
            {
                int count=points.Length/2;
                if(count<3) return;
                Vector2 center=Vector2.zero;
                for(int i=0;i<count;i++) center+=Point(points[i*2],points[i*2+1]);
                center/=count;
                // A center fan preserves fill intent in the stock-alias adapter; a pair of
                // rectangular triangles would be mistaken for a screen-space stroke.
                for(int i=0;i<count;i++)
                {
                    int next=(i+1)%count;
                    pen.Triangle(center,Point(points[i*2],points[i*2+1]),Point(points[next*2],points[next*2+1]),color);
                }
            }
            // Only convex pieces form a solid material face. Open frames keep their holes.
            internal void Mask(float[] convexPoints) => Fill(convexPoints,body);
            void FilledQuad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
            {
                Vector2 center=(a+b+c+d)*.25f;
                pen.Triangle(center,a,b,color);pen.Triangle(center,b,c,color);
                pen.Triangle(center,c,d,color);pen.Triangle(center,d,a,color);
            }
            void MaterialPanel(Vector2 a,Vector2 b,Vector2 c,Vector2 d,bool dark)
            {
                FilledQuad(a,b,c,d,dark?paper:body);
                if (dark || !detailed || Mathf.Min((b-a).sqrMagnitude,(d-a).sqrMagnitude) < minimumFoldSpan*minimumFoldSpan)
                    return;
                // One bent lower lip, cut back at its right-hand corner. All points are
                // inside this material face, including on rotated or moving parts.
                Vector2 lip = Vector2.Lerp(a,d,.29f);
                Vector2 ridge = Vector2.Lerp(Vector2.Lerp(a,b,.82f),Vector2.Lerp(d,c,.82f),.29f);
                pen.Triangle(a,b,ridge,foldShadow);
                pen.Triangle(a,ridge,lip,foldShadow);
                pen.Triangle(b,c,ridge,foldShadow);
            }
            internal void Fold(float ax,float ay,float bx,float by,float cx,float cy)
            {
                if (detailed) pen.Triangle(Point(ax,ay),Point(bx,by),Point(cx,cy),foldShadow);
            }
            internal void SolidBox(float x,float y,float w,float h,bool dark=false)
            {
                MaterialPanel(Point(x,y),Point(x+w,y),Point(x+w,y+h),Point(x,y+h),dark);
                Box(x,y,w,h,dark);
            }
            internal void AccentBox(float x,float y,float w,float h)
                => FilledQuad(Point(x,y),Point(x+w,y),Point(x+w,y+h),Point(x,y+h),accent);
            internal void PaperFace(float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy)
            {
                FilledQuad(Point(ax, ay), Point(bx, by), Point(cx, cy), Point(dx, dy), body);
                Line(ax, ay, bx, by); Line(bx, by, cx, cy);
                Line(cx, cy, dx, dy); Line(dx, dy, ax, ay);
            }
            internal void PaperFacet(float ax, float ay, float bx, float by, float cx, float cy, bool brass = false)
                => pen.Triangle(Point(ax, ay), Point(bx, by), Point(cx, cy), brass ? accent : foldShadow);
            internal void ColorBox(float x,float y,float w,float h,Color color)
            {
                color.a *= ink.a;
                FilledQuad(Point(x,y),Point(x+w,y),Point(x+w,y+h),Point(x,y+h),color);
            }
            internal void Inlay(float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy, Color color)
            {
                color.a *= ink.a;
                FilledQuad(Point(ax,ay),Point(bx,by),Point(cx,cy),Point(dx,dy),color);
            }
            internal void InlayDisc(float x, float y, float rx, float ry, Color color)
            {
                color.a *= ink.a;
                int steps = Tiny ? 8 : 16;
                Vector2 center = Point(x,y), previous = Point(x+rx,y);
                for (int i = 1; i <= steps; i++)
                {
                    float angle = i*2*Mathf.PI/steps;
                    var next = Point(x+rx*Mathf.Cos(angle), y+ry*Mathf.Sin(angle));
                    pen.Triangle(center, previous, next, color);
                    previous = next;
                }
            }
            internal void Disc(float cx,float cy,float rx,float ry,bool dark=false)
            {
                int steps=detailed?20:12;
                Vector2 center=Point(cx,cy), previous=Point(cx+rx,cy);
                for(int i=1;i<=steps;i++)
                {
                    float angle=i*Mathf.PI*2/steps;
                    Vector2 next=Point(cx+Mathf.Cos(angle)*rx,cy+Mathf.Sin(angle)*ry);
                    pen.Triangle(center,previous,next,dark?paper:body);previous=next;
                }
                Ellipse(cx,cy,rx,ry,dark);
            }
            internal void Beam(float ax,float ay,float bx,float by,float thickness)
            {
                Vector2 delta=new Vector2(bx-ax,by-ay);
                if(delta.sqrMagnitude<.000001f) return;
                Vector2 side=new Vector2(-delta.y,delta.x).normalized*(thickness*.5f);
                Vector2 a=new Vector2(ax,ay)-side,b=new Vector2(bx,by)-side;
                Vector2 c=new Vector2(bx,by)+side,d=new Vector2(ax,ay)+side;
                FilledQuad(Point(a.x,a.y),Point(b.x,b.y),Point(c.x,c.y),Point(d.x,d.y),body);
                Line(a.x,a.y,b.x,b.y);Line(b.x,b.y,c.x,c.y);Line(c.x,c.y,d.x,d.y);Line(d.x,d.y,a.x,a.y);
            }
            internal void LocalSolidBox(float x,float y,float w,float h,bool dark=false)
            {
                MaterialPanel(LocalPoint(x,y),LocalPoint(x+w,y),LocalPoint(x+w,y+h),LocalPoint(x,y+h),dark);
                LocalLine(x,y,x+w,y,dark);LocalLine(x+w,y,x+w,y+h,dark);
                LocalLine(x+w,y+h,x,y+h,dark);LocalLine(x,y+h,x,y,dark);
            }
            internal void TaperedSupport(float y0, float left0, float right0, float y1, float left1, float right1)
            {
                var a = LocalPoint(left0, y0);
                var b = LocalPoint(right0, y0);
                var c = LocalPoint(right1, y1);
                var d = LocalPoint(left1, y1);
                MaterialPanel(a, b, c, d, false);
                StrokePoints(a, b, ink, 1); StrokePoints(b, c, ink, 1);
                StrokePoints(c, d, ink, 1); StrokePoints(d, a, ink, 1);
            }
            internal void LocalDisc(float cx,float cy,float rx,float ry,bool dark=false)
            {
                int steps=detailed?20:12;
                Vector2 center=LocalPoint(cx,cy),previous=LocalPoint(cx+rx,cy);
                for(int i=1;i<=steps;i++)
                {
                    float angle=i*Mathf.PI*2/steps;
                    Vector2 next=LocalPoint(cx+Mathf.Cos(angle)*rx,cy+Mathf.Sin(angle)*ry);
                    pen.Triangle(center,previous,next,dark?paper:body);previous=next;
                }
                LocalEllipse(cx,cy,rx,ry,dark);
            }
            internal void Outline(float[] points,bool fill=true)
            {
                if(fill) Fill(points,wash);
                int count=points.Length/2;
                for(int i=0;i<count;i++)
                {
                    int next=(i+1)%count;
                    Line(points[i*2],points[i*2+1],points[next*2],points[next*2+1]);
                }
            }
            internal void Box(float x,float y,float w,float h,bool fine=false)
            { Line(x,y,x+w,y,fine);Line(x+w,y,x+w,y+h,fine);Line(x+w,y+h,x,y+h,fine);Line(x,y+h,x,y,fine); }
            internal void Ellipse(float cx,float cy,float rx,float ry,bool fine=false)
                => EllipseRing(cx,cy,rx,ry,fine,false);
            void EllipseRing(float cx,float cy,float rx,float ry,bool fine,bool local)
            {
                int steps=detailed?20:12;
                float weight=fine?.37f:1f;
                float half=manual?manualWidth*weight*.5f:
                    Mathf.Max(Mathf.Max(width*weight,pen.MinimumLineWidth),pen.MinimumStrokePixels*pen.WorldUnitsPerPixel)*.5f;
                Color color=fine?pencil:ink;
                float step=Mathf.PI*2/steps;
                Vector2 previous=EllipsePoint(cx,cy,rx,ry,0,local);
                for(int i=1;i<=steps;i++)
                {
                    Vector2 next=EllipsePoint(cx,cy,rx,ry,i*step,local);
                    StrokePoints(previous,next,color,weight);previous=next;
                }
                // Bevel the butt-ended segments without replacing their centerlines.
                // The stock alias adapter can still preserve the world stroke floor.
                previous=EllipsePoint(cx,cy,rx,ry,-step,local);
                Vector2 current=EllipsePoint(cx,cy,rx,ry,0,local);
                for(int i=1;i<=steps;i++)
                {
                    Vector2 next=EllipsePoint(cx,cy,rx,ry,i*step,local);
                    Vector2 incoming=current-previous,outgoing=next-current;
                    Vector2 a=new Vector2(-incoming.y,incoming.x).normalized*half;
                    Vector2 b=new Vector2(-outgoing.y,outgoing.x).normalized*half;
                    // The alias reconstructs this bevel as a short line. Stay above
                    // NotebookArtRenderer's 1e-7 squared cutoff, including float rounding.
                    if((a-b).sqrMagnitude>=.00000011f)
                        pen.Quad(current-a,current-b,current+a,current+b,color);
                    previous=current;current=next;
                }
            }
            Vector2 EllipsePoint(float cx,float cy,float rx,float ry,float angle,bool local)
            {
                float x=cx+Mathf.Cos(angle)*rx,y=cy+Mathf.Sin(angle)*ry;
                return local?LocalPoint(x,y):Point(x,y);
            }
            internal void Dot(float x,float y,float radius) { if(detailed) Ellipse(x,y,radius,radius,true); }
            internal void Mark(float x,float y,float radius) { Line(x-radius,y,x+radius,y,true);Line(x,y-radius,x,y+radius,true); }
            internal void Cross(float x,float y,float size) { Line(x-size,y,x+size,y);Line(x,y-size,x,y+size); }
            internal void Bolt() { Line(.60f,.15f,.36f,.51f);Line(.36f,.51f,.64f,.47f);Line(.64f,.47f,.41f,.84f); }
            internal void Hatch(float x,float y,float w,float h)
            {
                if(!detailed) return;
                for(int i=0;i<3;i++) Detail(x+w*i*.25f,y,x+w*(i*.25f+.25f),y+h);
            }
        }
    }
}
