using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public enum PlayerIcon { Play, Back, Pause, Fast, Home, Map, Book, Settings, Help, Exit, Restart, Check, Heart, Drop, Target, Flag, Trophy, Save, Plus, Trash, Pencil, Next, Copy }

    /// <summary>Resolution-independent control symbols; never dependent on font glyph coverage.</summary>
    public sealed class PlayerIconGraphic : MaskableGraphic
    {
        public PlayerIcon Icon;
        public bool Sketch;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var pen = new MeshPen(null, 256);
            void Line(float x, float y, float a, float b)
            {
                Vector2 from = new Vector2(x, y), to = new Vector2(a, b);
                if (!Sketch) { pen.Line(from, to, .10f, color); return; }
                Vector2 mid = (from + to) * .5f + new Vector2(.012f * Mathf.Sin(x * 17 + b * 23), .013f * Mathf.Cos(y * 13 + a * 19));
                pen.Line(from, mid, .085f, color); pen.Line(mid, to, .10f, color);
                Color echo = color; echo.a *= .2f;
                pen.Line(from + new Vector2(.035f, -.028f), to + new Vector2(.025f, -.018f), .025f, echo);
            }
            void Box(float x, float y, float w, float h) => pen.Rect(x, y, w, h, color);
            void Ring(float radius) => pen.Ring(Vector2.zero, radius, .09f, color, 32);
            void Play(float x) => pen.Triangle(new Vector2(x - .35f, -.62f), new Vector2(x - .35f, .62f), new Vector2(x + .5f, 0), color);
            switch (Icon)
            {
                case PlayerIcon.Plus: Line(-.65f, 0, .65f, 0); Line(0, -.65f, 0, .65f); break;
                case PlayerIcon.Next: Line(-.7f, 0, .7f, 0); Line(.7f, 0, .1f, .6f); Line(.7f, 0, .1f, -.6f); break;
                case PlayerIcon.Save:
                    Line(-.65f, -.65f, .65f, -.65f); Line(.65f, -.65f, .65f, .4f); Line(.65f, .4f, .4f, .65f); Line(.4f, .65f, -.65f, .65f); Line(-.65f, .65f, -.65f, -.65f);
                    Line(-.3f, .65f, -.3f, .13f); Line(-.3f, .13f, .3f, .13f); Line(.3f, .13f, .3f, .65f); Box(-.35f, -.65f, .7f, .45f); break;
                case PlayerIcon.Trash:
                    Line(-.65f, .4f, .65f, .4f); Line(-.2f, .65f, .2f, .65f); Line(-.5f, .4f, -.4f, -.65f); Line(.5f, .4f, .4f, -.65f); Line(-.4f, -.65f, .4f, -.65f); Line(-.15f, .15f, -.15f, -.4f); Line(.15f, .15f, .15f, -.4f); break;
                case PlayerIcon.Pencil:
                    Line(-.65f, -.65f, -.45f, -.1f); Line(-.45f, -.1f, .35f, .7f); Line(.35f, .7f, .7f, .35f); Line(.7f, .35f, -.1f, -.45f); Line(-.1f, -.45f, -.65f, -.65f); Line(.2f, .5f, .5f, .2f); break;
                case PlayerIcon.Copy:
                    Line(-.65f, -.3f, -.65f, .65f); Line(-.65f, .65f, .3f, .65f); Line(-.3f, -.65f, .65f, -.65f); Line(.65f, -.65f, .65f, .3f); Line(.65f, .3f, -.3f, .3f); Line(-.3f, .3f, -.3f, -.65f); break;
                case PlayerIcon.Play: Play(0); break;
                case PlayerIcon.Pause: Box(-.48f, -.58f, .28f, 1.16f); Box(.20f, -.58f, .28f, 1.16f); break;
                case PlayerIcon.Fast: Play(-.35f); Play(.38f); break;
                case PlayerIcon.Back: Line(.7f, 0, -.7f, 0); Line(-.7f, 0, -.1f, .6f); Line(-.7f, 0, -.1f, -.6f); break;
                case PlayerIcon.Check: Line(-.65f, 0, -.18f, -.45f); Line(-.18f, -.45f, .72f, .57f); break;
                case PlayerIcon.Exit: Line(-.2f, -.65f, -.65f, -.65f); Line(-.65f, -.65f, -.65f, .65f); Line(-.65f, .65f, -.2f, .65f); Line(-.1f, 0, .7f, 0); Line(.7f, 0, .3f, .4f); Line(.7f, 0, .3f, -.4f); break;
                case PlayerIcon.Home: Line(-.75f, .02f, 0, .7f); Line(0, .7f, .75f, .02f); Line(-.52f, .1f, -.52f, -.6f); Line(.52f, .1f, .52f, -.6f); Line(-.52f, -.6f, .52f, -.6f); Box(-.13f, -.6f, .26f, .47f); break;
                case PlayerIcon.Book: Line(0, -.6f, 0, .6f); Line(-.72f, -.5f, 0, -.65f); Line(.72f, -.5f, 0, -.65f); Line(-.72f, .65f, 0, .5f); Line(.72f, .65f, 0, .5f); Line(-.72f, -.5f, -.72f, .65f); Line(.72f, -.5f, .72f, .65f); break;
                case PlayerIcon.Map: Line(-.72f, -.6f, -.72f, .48f); Line(-.72f, .48f, -.22f, .7f); Line(-.22f, .7f, .25f, .48f); Line(.25f, .48f, .72f, .7f); Line(.72f, .7f, .72f, -.4f); Line(.72f, -.4f, .25f, -.6f); Line(.25f, -.6f, -.22f, -.4f); Line(-.22f, -.4f, -.72f, -.6f); Line(-.22f, -.4f, -.22f, .7f); Line(.25f, -.6f, .25f, .48f); break;
                case PlayerIcon.Settings:
                    Ring(.45f); Ring(.17f);
                    for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; Line(Mathf.Cos(a) * .43f, Mathf.Sin(a) * .43f, Mathf.Cos(a) * .7f, Mathf.Sin(a) * .7f); } break;
                case PlayerIcon.Restart:
                    if (!Sketch) { Ring(.57f); pen.Triangle(new Vector2(-.7f, .2f), new Vector2(-.8f, .8f), new Vector2(-.2f, .52f), color); break; }
                    for (int i = 0; i < 27; i++) { float a = (-220 + i * 10) * Mathf.Deg2Rad, b = a + 10 * Mathf.Deg2Rad; Line(Mathf.Cos(a) * .6f, Mathf.Sin(a) * .6f, Mathf.Cos(b) * .6f, Mathf.Sin(b) * .6f); }
                    pen.Triangle(new Vector2(.27f, .7f), new Vector2(.78f, .66f), new Vector2(.47f, .22f), color); break;
                case PlayerIcon.Target: Ring(.5f); Ring(.15f); Line(-.8f, 0, -.4f, 0); Line(.4f, 0, .8f, 0); Line(0, -.8f, 0, -.4f); Line(0, .4f, 0, .8f); break;
                case PlayerIcon.Heart:
                    pen.Disc(new Vector2(-.3f, .23f), .39f, color, 20); pen.Disc(new Vector2(.3f, .23f), .39f, color, 20);
                    pen.Triangle(new Vector2(-.65f, .1f), new Vector2(.65f, .1f), new Vector2(0, -.65f), color); break;
                case PlayerIcon.Drop: pen.Disc(new Vector2(0, -.19f), .47f, color, 24); pen.Triangle(new Vector2(-.44f, 0), new Vector2(.44f, 0), new Vector2(.05f, .8f), color); break;
                case PlayerIcon.Flag: Line(-.5f, -.7f, -.5f, .72f); Line(-.5f, .65f, .65f, .65f); Line(.65f, .65f, .35f, .2f); Line(.35f, .2f, -.5f, .2f); break;
                case PlayerIcon.Trophy: Line(-.5f, .65f, .5f, .65f); Line(-.5f, .65f, -.3f, -.15f); Line(.5f, .65f, .3f, -.15f); Line(-.3f, -.15f, .3f, -.15f); Line(0, -.15f, 0, -.65f); Line(-.4f, -.65f, .4f, -.65f); break;
                default: Ring(.7f); Line(-.25f, .25f, 0, .4f); Line(0, .4f, .25f, .2f); Line(.25f, .2f, 0, -.05f); Line(0, -.05f, 0, -.2f); pen.Disc(new Vector2(0, -.44f), .06f, color, 8); break;
            }
            pen.CopyTo(vh, rectTransform.rect.center, Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .48f);
        }
    }
}
