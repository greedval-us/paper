using System.Linq;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        void Map()
        {
            bool assembly = game.Run.AssemblyCatalog != null;
            float s = Mathf.Min(W / 1600f, H / 900f);
            float top = 64 * s, side = Mathf.Min(W * .46f, 350 * s), fieldWidth = W - side;
            var map = ui.Panel(page, "Mission map", new Rect(0, top, fieldWidth, H - top), Color.clear);
            var paper = PlayerUi.Graphic<Image>(map, "Mission paper extension", new Rect(0, 0, fieldWidth, H - top));
            paper.color = PlayerUi.Paper;
            PlayerUi.Graphic<PlayerPaperGraphic>(map, "Mission paper ruling", new Rect(0, 0, fieldWidth, H - top));
            var sprite = Resources.Load<Sprite>("Art/Player/MapFieldIllustration");
            Rect illustrationBounds = NotebookFitRect(sprite, new Rect(0, 0, fieldWidth, H - top));
            NotebookSprite(map, "Mission illustration", sprite, illustrationBounds);
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
                float x = Mathf.Clamp(illustrationBounds.x + approachX[i] / approaches[i] * illustrationBounds.width,
                    illustrationBounds.x + 32 * s, illustrationBounds.xMax - 32 * s);
                float arrowY = Mathf.Clamp(illustrationBounds.y + approachY[i] / approaches[i] * illustrationBounds.height,
                    illustrationBounds.y + 34 * s, illustrationBounds.yMax - 34 * s);
                var arrow = PlayerUi.Graphic<PlayerIconGraphic>(map, "Enemy approach", new Rect(x - 28 * s, arrowY - 28 * s, 56 * s, 56 * s));
                arrow.Icon = PlayerIcon.Next; arrow.Sketch = true; arrow.color = NotebookPalette.Red;
                arrow.rectTransform.localEulerAngles = new Vector3(0, 0, i == 0 ? 0 : i == 1 ? 180 : i == 2 ? 90 : -90);
            }

            var info = ui.Panel(page, "Mission information", new Rect(fieldWidth, top, side, H - top), PlayerUi.Card);
            float prepareHeight = Mathf.Max(76 * s, 56 / Layout.Scale);
            float contentHeight = H - top - prepareHeight - 30 * s;
            float detailScale = Mathf.Min(s, contentHeight / (assembly ? 708 : 652));
            var content = ui.Panel(info, "Mission details", new Rect(12 * s, 0, side - 24 * s, contentHeight), Color.clear);
            float width = side - 38 * s;
            NotebookLabel(content, ui.T("mission"), new Rect(6 * s, 8 * detailScale, width - 12 * s, 50 * detailScale), Mathf.RoundToInt(40 * detailScale), alignment: TextAnchor.MiddleCenter);
            float y = 62 * detailScale;
            NotebookMissionArt(content, 0, new Rect(28 * s, y, width - 56 * s, 172 * detailScale)); y += 182 * detailScale;
            float row = 56 * detailScale;
            MissionMetric(content, "Mission waves", PlayerIcon.Target, session.Balance.Waves.Count + " волн", y, width, detailScale, row - 4 * detailScale); y += row;
            MissionMetric(content, "Mission castle health", PlayerIcon.Heart, "Замок " + session.Balance.BaseHealth, y, width, detailScale, row - 4 * detailScale); y += row;
            MissionMetric(content, "Mission starting ink", PlayerIcon.Pencil, "Старт " + session.Balance.StartingInk + " штрихов", y, width, detailScale, row - 4 * detailScale); y += row;
            if (assembly)
            {
                string count = GameText.Resolve(UserText.Message("assembly.player.map_loadout", game.Run.Workshop.LoadoutEntries.Count(entry => entry != null)));
                MissionMetric(content, "Mission album loadout", PlayerIcon.Book, count, y, width, detailScale, row - 4 * detailScale); y += row;
            }
            y += 10 * detailScale;
            NotebookLabel(content, "Защити замок. Строй на свободной земле.", new Rect(10 * s, y, width - 20 * s, 62 * detailScale), Mathf.RoundToInt(27 * detailScale), alignment: TextAnchor.MiddleCenter);
            y += 74 * detailScale;
            NotebookLabel(content, "Чернильницы", new Rect(0, y, width, 34 * detailScale), Mathf.RoundToInt(27 * detailScale), alignment: TextAnchor.MiddleCenter);
            y += 40 * detailScale;
            string[] colors = { "Красная", "Синяя", "Зелёная" };
            for (int i = 0; i < 3; i++)
            {
                float cell = width / 3;
                NotebookMissionArt(content, i + 1, new Rect(i * cell + 14 * s, y, cell - 28 * s, 82 * detailScale));
                NotebookLabel(content, colors[i], new Rect(i * cell, y + 88 * detailScale, cell, 28 * detailScale), Mathf.RoundToInt(20 * detailScale), alignment: TextAnchor.MiddleCenter);
            }
            var prepare = NotebookAction(info, "Prepare mission", new Rect(16 * s, H - top - 20 * s - prepareHeight, side - 32 * s, prepareHeight), PlayerIcon.Pencil,
                "Подготовить оборону", () => game.Navigate(PlayerPage.Preparation), true);
            // Compact windows may use two complete handwritten lines beside the pencil.
            prepare.GetComponentInChildren<Text>().resizeTextMaxSize = Mathf.RoundToInt(26 * s);
            if (assembly)
                NotebookAction(page, "Map album", new Rect(12 * s, H - 80 * s, Mathf.Min(238 * s, fieldWidth - 24 * s), 60 * s), PlayerIcon.Book,
                    GameText.Resolve(UserText.Message("assembly.player.album")), game.OpenAlbum);
            else
                NotebookAction(page, "Map workshop", new Rect(12 * s, H - 80 * s, Mathf.Min(238 * s, fieldWidth - 24 * s), 60 * s), PlayerIcon.Pencil,
                    "Мастерская", () => game.Navigate(PlayerPage.Workshop));
            if (assembly)
            {
                NotebookScreenHeader("Карта", () => game.Navigate(PlayerPage.Home), ProgressCopy());
                progress = page.Find("Notebook header/Mission heading").GetComponentInChildren<Text>();
                progress.name = "Player progress";
            }
            else NotebookScreenHeader("Карта", () => game.Navigate(PlayerPage.Home));
        }

        void MissionMetric(Transform parent, string name, PlayerIcon icon, string copy, float y, float width, float s, float height)
        {
            var row = ui.Panel(parent, name + " row", new Rect(0, y, width, height), Color.clear);
            var symbol = PlayerUi.Graphic<PlayerIconGraphic>(row, "Metric icon", new Rect(12 * s, (height - 38 * s) * .5f, 38 * s, 38 * s));
            symbol.Icon = icon; symbol.Sketch = true; symbol.color = PlayerUi.Ink;
            NotebookInkIcon(symbol, icon == PlayerIcon.Target ? "icon.skull" : NotebookIconId(icon));
            NotebookLabel(row, copy, new Rect(68 * s, 0, width - 74 * s, height), Mathf.RoundToInt(27 * s)).name = name;
            var line = PlayerUi.Graphic<Image>(row, "Ink rule", new Rect(0, height - 1, width, 1));
            line.color = new Color(PlayerUi.Blue.r, PlayerUi.Blue.g, PlayerUi.Blue.b, .35f);
        }
    }
}
