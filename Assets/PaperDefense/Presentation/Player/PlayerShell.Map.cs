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
            // Taller windows need a readable mission column as well as more vertical space.
            // Widescreen retains the source's roughly 22% sidebar; 4:3 gives it room for ink labels.
            float sidebarScale = Mathf.Max(s, Mathf.Min(H / 900f, W / 1250f));
            float top = 64 * s, side = Mathf.Min(W * .32f, 350 * sidebarScale), fieldWidth = W - side;
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
            float prepareHeight = Mathf.Max(76 * sidebarScale, 56 / Layout.Scale);
            float contentHeight = H - top - prepareHeight - 30 * sidebarScale;
            float detailScale = contentHeight / (assembly ? 708 : 652);
            var content = ui.Panel(info, "Mission details", new Rect(12 * sidebarScale, 0, side - 24 * sidebarScale, contentHeight), Color.clear);
            float width = side - 24 * sidebarScale;
            NotebookLabel(content, ui.T("mission"), new Rect(6 * sidebarScale, 8 * detailScale, width - 12 * sidebarScale, 50 * detailScale), Mathf.RoundToInt(40 * sidebarScale), alignment: TextAnchor.MiddleCenter);
            float y = 62 * detailScale;
            NotebookMissionArt(content, 0, new Rect(28 * sidebarScale, y, width - 56 * sidebarScale, 172 * detailScale)); y += 182 * detailScale;
            float row = 56 * detailScale;
            MissionMetric(content, "Mission waves", PlayerIcon.Target, session.Balance.Waves.Count + " волн", y, width, sidebarScale, row - 4 * detailScale); y += row;
            MissionMetric(content, "Mission castle health", PlayerIcon.Heart, "Замок " + session.Balance.BaseHealth, y, width, sidebarScale, row - 4 * detailScale); y += row;
            MissionMetric(content, "Mission starting ink", PlayerIcon.Pencil, "Старт " + session.Balance.StartingInk + " штрихов", y, width, sidebarScale, row - 4 * detailScale); y += row;
            if (assembly)
            {
                string count = GameText.Resolve(UserText.Message("assembly.player.map_loadout", game.Run.Workshop.LoadoutEntries.Count(entry => entry != null)));
                MissionMetric(content, "Mission album loadout", PlayerIcon.Book, count, y, width, sidebarScale, row - 4 * detailScale); y += row;
            }
            y += 10 * detailScale;
            NotebookLabel(content, "Защити замок. Строй на свободной земле.", new Rect(10 * sidebarScale, y, width - 20 * sidebarScale, 62 * detailScale), Mathf.RoundToInt(27 * sidebarScale), alignment: TextAnchor.MiddleCenter);
            y += 74 * detailScale;
            NotebookLabel(content, "Чернильницы", new Rect(0, y, width, 34 * detailScale), Mathf.RoundToInt(27 * sidebarScale), alignment: TextAnchor.MiddleCenter);
            y += 40 * detailScale;
            string[] colors = { "Красная", "Синяя", "Зелёная" };
            for (int i = 0; i < 3; i++)
            {
                float cell = width / 3;
                NotebookMissionArt(content, i + 1, new Rect(i * cell + 10 * sidebarScale, y, cell - 20 * sidebarScale, 82 * detailScale));
                NotebookLabel(content, colors[i], new Rect(i * cell, y + 88 * detailScale, cell, 28 * detailScale), Mathf.RoundToInt(20 * sidebarScale), alignment: TextAnchor.MiddleCenter);
            }
            var prepare = NotebookAction(info, "Prepare mission", new Rect(16 * sidebarScale, H - top - 20 * sidebarScale - prepareHeight, side - 32 * sidebarScale, prepareHeight), PlayerIcon.Pencil,
                "Подготовить оборону", () => game.Navigate(PlayerPage.Preparation), true);
            // Compact windows may use two complete handwritten lines beside the pencil.
            prepare.GetComponentInChildren<Text>().resizeTextMaxSize = Mathf.RoundToInt(26 * sidebarScale);
            if (assembly)
            {
                var album = NotebookAction(page, "Map album", new Rect(12 * s, H - 80 * s, Mathf.Min(238 * s, fieldWidth - 24 * s), 60 * s), PlayerIcon.Book,
                    GameText.Resolve(UserText.Message("assembly.player.album")), game.OpenAlbum);
                NotebookControlIcon(album, "icon.help");
            }
            else
                NotebookAction(page, "Map workshop", new Rect(12 * s, H - 80 * s, Mathf.Min(238 * s, fieldWidth - 24 * s), 60 * s), PlayerIcon.Pencil,
                    "Мастерская", () => game.Navigate(PlayerPage.Workshop));
            if (assembly)
            {
                NotebookScreenHeader("Карта", () => game.Navigate(PlayerPage.Home), ProgressCopy(), currencyCoin: true);
                progress = page.Find("Notebook header/Mission heading").GetComponentInChildren<Text>();
                progress.name = "Player progress";
            }
            else NotebookScreenHeader("Карта", () => game.Navigate(PlayerPage.Home), currencyCoin: true);
        }

        void MissionMetric(Transform parent, string name, PlayerIcon icon, string copy, float y, float width, float s, float height)
        {
            var row = ui.Panel(parent, name + " row", new Rect(0, y, width, height), Color.clear);
            var symbol = PlayerUi.Graphic<PlayerIconGraphic>(row, "Metric icon", new Rect(12 * s, (height - 38 * s) * .5f, 38 * s, 38 * s));
            symbol.Icon = icon; symbol.Sketch = true; symbol.color = PlayerUi.Ink;
            NotebookInkIcon(symbol, icon == PlayerIcon.Target ? "icon.skull" : icon == PlayerIcon.Book ? "icon.help" : NotebookIconId(icon));
            // A mission value is one readable line. Giving best-fit the whole tall row
            // allowed it to put the denominator of the loadout count on its own line.
            float textHeight = Mathf.Min(height, 32 * s);
            var caption = NotebookLabel(row, copy,
                new Rect(68 * s, (height - textHeight) * .5f, width - 74 * s, textHeight), Mathf.RoundToInt(27 * s));
            caption.name = name;
            caption.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            var line = PlayerUi.Graphic<Image>(row, "Ink rule", new Rect(0, height - 1, width, 1));
            line.color = new Color(PlayerUi.Blue.r, PlayerUi.Blue.g, PlayerUi.Blue.b, .35f);
        }
    }
}
