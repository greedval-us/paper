using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        void Map()
        {
            float s = Mathf.Min(W / 1600f, H / 900f) * game.InterfaceScale;
            float top = 64 * s, side = Mathf.Min(W * .46f, 350 * s), fieldWidth = W - side;
            var map = ui.Panel(page, "Mission map", new Rect(0, top, fieldWidth, H - top), Color.clear);
            map.gameObject.AddComponent<RectMask2D>();
            var landscape = PlayerUi.Graphic<Image>(map, "Mission illustration", new Rect(0, 0, fieldWidth, H - top));
            landscape.sprite = Resources.Load<Sprite>("Art/Player/MapLandscape");
            if (landscape.sprite != null)
            {
                landscape.rectTransform.pivot = new Vector2(.5f, .5f);
                var fit = landscape.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = landscape.sprite.rect.width / landscape.sprite.rect.height;
            }
            else landscape.color = PlayerUi.Paper;
            var session = game.Run.Session;
            var approachX = new float[4]; var approachY = new float[4]; var approaches = new int[4];
            if (session.Battlefield != null)
                foreach (var entrance in session.Battlefield.Entrances)
                {
                    int sideIndex = entrance.X == 0 ? 0 : entrance.X == session.Balance.Width - 1 ? 1 : entrance.Y == 0 ? 2 : 3;
                    approachX[sideIndex] += (entrance.X + .5f) / session.Balance.Width;
                    approachY[sideIndex] += 1 - (entrance.Y + .5f) / session.Balance.Height;
                    approaches[sideIndex]++;
                }
            for (int i = 0; i < 4; i++)
            {
                if (approaches[i] == 0) continue;
                float x = Mathf.Clamp(approachX[i] / approaches[i] * fieldWidth, 32 * s, fieldWidth - 32 * s);
                float arrowY = Mathf.Clamp(approachY[i] / approaches[i] * (H - top), 34 * s, H - top - 34 * s);
                var arrow = PlayerUi.Graphic<PlayerIconGraphic>(map, "Enemy approach", new Rect(x - 28 * s, arrowY - 28 * s, 56 * s, 56 * s));
                arrow.Icon = PlayerIcon.Next; arrow.Sketch = true; arrow.color = NotebookPalette.Red;
                arrow.rectTransform.localEulerAngles = new Vector3(0, 0, i == 0 ? 0 : i == 1 ? 180 : i == 2 ? 90 : -90);
            }

            var info = ui.Panel(page, "Mission information", new Rect(fieldWidth, top, side, H - top), PlayerUi.Card);
            float contentHeight = H - top - 106 * s;
            bool compact = contentHeight < 690 * s;
            var content = ui.Panel(info, "Mission details", new Rect(12 * s, 0, side - 24 * s, contentHeight), Color.clear);
            float width = side - 38 * s;
            NotebookLabel(content, ui.T("mission"), new Rect(6 * s, 8 * s, width - 12 * s, (compact ? 50 : 58) * s), Mathf.RoundToInt((compact ? 35 : 40) * s), alignment: TextAnchor.MiddleCenter);
            float y = (compact ? 62 : 78) * s;
            if (!compact) { NotebookMissionArt(content, 0, new Rect(28 * s, y, width - 56 * s, 158 * s)); y += 168 * s; }
            float row = (compact ? 48 : 58) * s;
            MissionMetric(content, "Mission waves", PlayerIcon.Target, session.Balance.Waves.Count + " волн", y, width, s, row - 4 * s); y += row;
            MissionMetric(content, "Mission castle health", PlayerIcon.Heart, "Замок " + session.Balance.BaseHealth, y, width, s, row - 4 * s); y += row;
            MissionMetric(content, "Mission starting ink", PlayerIcon.Pencil, "Старт " + session.Balance.StartingInk + " штрихов", y, width, s, row - 4 * s); y += row;
            y += (compact ? 8 : 18) * s;
            NotebookLabel(content, "Защити замок. Строй на свободной земле.", new Rect(10 * s, y, width - 20 * s, (compact ? 48 : 70) * s), Mathf.RoundToInt((compact ? 25 : 28) * s), alignment: TextAnchor.MiddleCenter);
            y += (compact ? 56 : 86) * s;
            NotebookLabel(content, "Чернильницы", new Rect(0, y, width, (compact ? 26 : 34) * s), Mathf.RoundToInt(27 * s), alignment: TextAnchor.MiddleCenter);
            y += (compact ? 32 : 42) * s;
            string[] colors = { "Красная", "Синяя", "Зелёная" };
            for (int i = 0; i < 3; i++)
            {
                float cell = width / 3;
                NotebookMissionArt(content, i + 1, new Rect(i * cell + 14 * s, y, cell - 28 * s, (compact ? 64 : 82) * s));
                NotebookLabel(content, colors[i], new Rect(i * cell, y + (compact ? 68 : 88) * s, cell, (compact ? 24 : 30) * s), Mathf.RoundToInt(20 * s), alignment: TextAnchor.MiddleCenter);
            }
            NotebookAction(info, "Prepare mission", new Rect(16 * s, H - top - 96 * s, side - 32 * s, 76 * s), PlayerIcon.Pencil,
                "Подготовить оборону", () => game.Navigate(PlayerPage.Preparation), true);
            NotebookAction(page, "Map workshop", new Rect(12 * s, H - 80 * s, Mathf.Min(238 * s, fieldWidth - 24 * s), 60 * s), PlayerIcon.Pencil,
                "Мастерская", () => game.Navigate(PlayerPage.Workshop));
            NotebookScreenHeader("Карта", () => game.Navigate(PlayerPage.Home));
        }

        void MissionMetric(Transform parent, string name, PlayerIcon icon, string copy, float y, float width, float s, float height)
        {
            var row = ui.Panel(parent, name + " row", new Rect(0, y, width, height), Color.clear);
            var symbol = PlayerUi.Graphic<PlayerIconGraphic>(row, "Metric icon", new Rect(12 * s, (height - 38 * s) * .5f, 38 * s, 38 * s));
            symbol.Icon = icon; symbol.Sketch = true; symbol.color = PlayerUi.Ink;
            NotebookLabel(row, copy, new Rect(68 * s, 0, width - 74 * s, height), Mathf.RoundToInt(27 * s)).name = name;
            var line = PlayerUi.Graphic<Image>(row, "Ink rule", new Rect(0, height - 1, width, 1));
            line.color = new Color(PlayerUi.Blue.r, PlayerUi.Blue.g, PlayerUi.Blue.b, .35f);
        }
    }
}
