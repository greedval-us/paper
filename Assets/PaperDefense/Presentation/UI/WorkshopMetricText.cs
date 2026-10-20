using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Standard full-text rendering with a native line pitch fitted to the notebook rows.</summary>
    public sealed class WorkshopMetricText : Text
    {
        readonly TextGenerator measurement = new TextGenerator();
        public const float Baseline = 27;

        public static WorkshopMetricText Create(WorkshopUi ui, Transform parent, string name, string value, Rect bounds)
        {
            var label = WorkshopUi.Graphic<WorkshopMetricText>(parent,name,bounds);
            label.font = ui.BodyFont; label.fontSize = 20; label.fontStyle = FontStyle.Bold;
            label.text = ui.Resolve(value); label.color = WorkshopUi.Blue;
            label.alignment = TextAnchor.UpperLeft; label.lineSpacing = 1;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = false;
            label.supportRichText = false;
            label.FitRows();
            return label;
        }

        public void FitRows()
        {
            if(font == null || rectTransform.rect.width<=0) return;
            fontSize=20; fontStyle=FontStyle.Bold; resizeTextForBestFit=false;
            foreach(var row in (text??"").TrimEnd('\r','\n').Split('\n'))
            {
                while(fontSize>17)
                {
                    var widthSettings=GetGenerationSettings(Vector2.zero);
                    if(measurement.GetPreferredWidth(row,widthSettings)/pixelsPerUnit<=rectTransform.rect.width) break;
                    fontSize--;
                }
            }
            var settings=GetGenerationSettings(new Vector2(2000,1000)); settings.lineSpacing=1;
            settings.resizeTextForBestFit=false; settings.verticalOverflow=VerticalWrapMode.Overflow;
            measurement.Populate("А\nА",settings);
            if(measurement.lines.Count>=2)
            {
                float native=Mathf.Abs(measurement.lines[1].topY-measurement.lines[0].topY)/pixelsPerUnit;
                if(native>.001f) lineSpacing=Baseline/native;
            }
        }

        protected override void OnDestroy()
        { ((System.IDisposable)measurement).Dispose(); base.OnDestroy(); }
    }
}
