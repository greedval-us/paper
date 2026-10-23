using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        Text resultGoldReward, resultXpReward, resultCompletedWaves, resultRewardStatus, resultReceipt, resultProgress, resultXpCaption, resultDefeatWave;
        PlayerIconGraphic resultSavedMark;
        Image resultXpFill;
        Rect resultXpTrack;
        Button resultRetrySave, resultHome;
        Rect resultStatusFullBounds, resultStatusRetryBounds;

        void ResetAlbumResult()
        {
            resultGoldReward = resultXpReward = resultCompletedWaves = resultRewardStatus = resultReceipt = resultProgress = null;
            resultXpCaption = resultDefeatWave = null; resultXpFill = null;
            resultSavedMark = null;
            resultRetrySave = resultHome = null;
        }

        void AlbumResult()
        {
            bool victory = game.Run.Session.Phase == RunPhase.Victory;
            NotebookPageBackdrop("Art/Player/FoundationPageDecor", "Result landscape border");
            var border = page.Find("Result landscape border")?.GetComponent<Image>();
            if (border != null) border.color = new Color(1, 1, 1, victory ? .32f : .16f);
            float scale = Mathf.Min(W / 1600f, H / 900f);
            var composition = ui.Panel(page, "Result composition", new Rect((W - 1600 * scale) * .5f,
                (H - 900 * scale) * .5f, 1600 * scale, 900 * scale), Color.clear);
            Rect R(float x, float y, float width, float height) => new Rect(x * scale, y * scale, width * scale, height * scale);
            int F(float size) => Mathf.Max(16, Mathf.RoundToInt(size * scale));
            ResultLandscape(composition, scale, victory);
            if (!victory) ui.Panel(composition, "Defeat results sheet", R(1006, 70, 528, 756), PlayerUi.Card);

            var banner = ui.Panel(composition, "Result title ribbon", victory ? R(365, 4, 870, 118) : R(140, 62, 810, 150), Color.clear);
            ResultEmblem(banner, victory ? NotebookResultEmblem.Ribbon : NotebookResultEmblem.TornRibbon,
                victory ? R(0, 0, 870, 118) : R(0, 0, 810, 150));
            if (victory)
            {
                ResultEmblem(composition, NotebookResultEmblem.Laurel, R(306, 31, 62, 100));
                var rightLaurel = ResultEmblem(composition, NotebookResultEmblem.Laurel, R(1232, 31, 62, 100));
                rightLaurel.rectTransform.localScale = new Vector3(-1, 1, 1);
            }
            NotebookLabel(banner, JourneyCopy(victory ? "result.victory" : "result.defeat"),
                new Rect((victory ? 74 : 72) * scale, 0, (victory ? 722 : 666) * scale, (victory ? 110 : 142) * scale),
                F(victory ? 64 : 82), alignment: TextAnchor.MiddleCenter);
            NotebookLabel(composition, ui.T("mission"), victory ? R(520, 108, 560, 42) : R(240, 220, 610, 52),
                F(32), alignment: TextAnchor.MiddleCenter);

            var rewards = ui.Panel(composition, "Result reward sheet", victory ? R(390, 156, 820, 80) : R(1026, 90, 488, 276), PlayerUi.Card);
            resultCompletedWaves = NotebookLabel(rewards, "", victory ? R(18, 8, 268, 58) : R(20, 8, 448, 44), F(28));
            resultCompletedWaves.name = "Result completed waves";
            resultGoldReward = NotebookLabel(rewards, "", victory ? R(332, 4, 212, 68) : R(76, 100, 392, 54),
                F(victory ? 31 : 38));
            resultGoldReward.name = "Result gold reward";
            resultXpReward = NotebookLabel(rewards, "", victory ? R(608, 4, 202, 68) : R(76, 158, 392, 54),
                F(victory ? 31 : 38));
            resultXpReward.name = "Result XP reward";
            if (victory)
            {
                ResultAtlasIcon(rewards, "icon.flag", R(12, 9, 44, 60));
                PlayerUi.Place(resultCompletedWaves.rectTransform, R(62, 4, 214, 68));
                ResultEmblem(rewards, NotebookResultEmblem.Coin, R(278, 18, 42, 46));
                foreach (float dividerX in new[] { 264f, 548f })
                {
                    var divider = PlayerUi.Graphic<Image>(rewards, "Reward column divider", R(dividerX, 14, 1, 52));
                    divider.color = new Color(PlayerUi.Blue.r, PlayerUi.Blue.g, PlayerUi.Blue.b, .42f);
                }
                var star = ResultEmblem(rewards, NotebookResultEmblem.XpStar, R(558, 20, 38, 42));
                NotebookLabel(star.transform, "XP", R(3, 5, 32, 32), F(15), Color.white, TextAnchor.MiddleCenter);
            }
            else
            {
                resultDefeatWave = NotebookLabel(rewards, "", R(20, 54, 448, 38), F(29), NotebookPalette.Red);
                resultDefeatWave.name = "Result defeat wave";
                ResultEmblem(rewards, NotebookResultEmblem.Coin, R(20, 111, 42, 46));
                var star = ResultEmblem(rewards, NotebookResultEmblem.XpStar, R(20, 168, 44, 44));
                NotebookLabel(star.transform, "XP", R(3, 5, 38, 34), F(15), Color.white, TextAnchor.MiddleCenter);
            }
            var totals = RunRewardTotals.Calculate(game.Run.Session.CompletedWaves, victory);
            if (victory)
            {
                var breakdown = ui.Panel(composition, "Result reward breakdown", R(350, 244, 900, 48), PlayerUi.Card);
                string[] sources = JourneyCopy("result.breakdown", totals.WaveGold, totals.WaveXp, totals.VictoryGold, totals.VictoryXp)
                    .Split(new[] { "    " }, System.StringSplitOptions.None);
                for (int source = 0; source < sources.Length; source++)
                    NotebookLabel(breakdown, sources[source], R(14 + source * 450, 0, 422, 48), F(23), PlayerUi.Ink, TextAnchor.MiddleCenter);
                var divider = PlayerUi.Graphic<Image>(breakdown, "Reward source divider", R(450, 10, 1, 28));
                divider.color = new Color(PlayerUi.Blue.r, PlayerUi.Blue.g, PlayerUi.Blue.b, .42f);
            }
            var progressSheet = ui.Panel(victory ? composition : rewards, "Result progress sheet", victory ? R(350, 292, 900, 76) : R(14, 238, 460, 30), victory ? PlayerUi.Card : Color.clear);
            resultProgress = NotebookLabel(progressSheet, "", victory ? R(62, 8, 306, 56) : R(6, 0, 448, 30),
                F(victory ? 27 : 23), alignment: TextAnchor.MiddleCenter);
            resultProgress.name = "Result progress";
            if (victory)
            {
                ResultAtlasIcon(progressSheet, "icon.shield", R(20, 21, 34, 34));
                resultXpCaption = NotebookLabel(progressSheet, "", R(392, 3, 470, 30), F(21), alignment: TextAnchor.MiddleCenter);
                resultXpCaption.name = "Result XP interval";
                var track = ui.Panel(progressSheet, "Result XP track", R(386, 43, 480, 20), PlayerUi.Paper);
                resultXpTrack = new Rect(3 * scale, 3 * scale, 474 * scale, 14 * scale);
                resultXpFill = PlayerUi.Graphic<Image>(track, "Result XP progress", resultXpTrack);
                resultXpFill.color = NotebookPalette.Blue;
            }
            bool pendingSave = game.Run.HasPendingRunRewards || game.Run.Workshop.HasUnsavedChanges;
            float reportWidth = victory ? pendingSave ? 900 : 600 : 488, reportHeight = victory ? 210 : 208;
            var report = ui.Panel(composition, "Result mission report", victory ? R((1600 - reportWidth) * .5f, 594, reportWidth, reportHeight) : R(1026, 378, reportWidth, reportHeight), PlayerUi.Card);
            var statisticsButton = NotebookAction(report, "Result statistics", R(16, victory ? 90 : 4, reportWidth - 32, 44), PlayerIcon.Book,
                "Статистика миссии", OpenAlbumResultStatistics);
            NotebookControlIcon(statisticsButton, "icon.help");
            var stats = game.Run.Session.Statistics;
            string reportCopy = ui.T("result_stats_left", stats.BuildInvestment + stats.UpgradeInvestment, stats.RefundedInk,
                stats.LostInvestment, stats.PeakAliveEnemies);
            if (victory)
            {
                string[] financialRows = reportCopy.Split('\n');
                for (int metric = 0; metric < 3 && metric < financialRows.Length; metric++)
                {
                    ResultAtlasIcon(report, "icon.pencil", R(17, 4 + metric * 27, 27, 27));
                    int separator = financialRows[metric].LastIndexOf('·');
                    string caption = separator < 0 ? financialRows[metric] : financialRows[metric].Substring(0, separator).Trim();
                    string value = separator < 0 ? "" : financialRows[metric].Substring(separator + 1).Trim();
                    NotebookLabel(report, caption, R(52, 4 + metric * 27, reportWidth - 220, 27), F(23), PlayerUi.Ink);
                    NotebookLabel(report, value, R(reportWidth - 160, 4 + metric * 27, 138, 27), F(25), PlayerUi.Ink, TextAnchor.MiddleRight);
                    var rule = PlayerUi.Graphic<Image>(report, "Financial ink rule", R(16, 30 + metric * 27, reportWidth - 32, 1));
                    rule.color = new Color(.12f, .28f, .52f, .28f);
                }
            }
            else
            {
                bool completeHistory = stats.Breaches.Count == stats.DestroyedStructures;
                int lostTowers = stats.Breaches.Count(loss => loss.Kind == StructureKind.Gun || loss.Kind == StructureKind.Piercer ||
                    loss.Kind == StructureKind.Mortar || loss.Kind == StructureKind.Fan || loss.Kind == StructureKind.Ricochet ||
                    loss.Kind == StructureKind.Stapler || loss.Kind == StructureKind.Blotter);
                string[] lossLabels = { completeHistory ? "Потеряно башен" : "Потеряно построек", completeHistory ? "Потеряно сооружений" : "Утрачено штрихов" };
                int[] lossValues = { completeHistory ? lostTowers : stats.DestroyedStructures, completeHistory ? stats.DestroyedStructures - lostTowers : stats.LostInvestment };
                for (int loss = 0; loss < lossLabels.Length; loss++)
                {
                    float lossY = 54 + loss * 36;
                    ui.Art(report, loss == 0 ? "object.castle" : "ui.tool.wall", R(18, lossY, 32, 32));
                    NotebookLabel(report, lossLabels[loss], R(62, lossY, reportWidth - 148, 32), F(24));
                    var value = NotebookLabel(report, lossValues[loss].ToString(), R(reportWidth - 78, lossY, 56, 32), F(28), alignment: TextAnchor.MiddleRight);
                    value.name = loss == 0 ? completeHistory ? "Result lost towers" : "Result total losses" : completeHistory ? "Result lost supports" : "Result lost investment";
                    var line = PlayerUi.Graphic<Image>(report, "Loss row divider", R(18, lossY + 33, reportWidth - 36, 1));
                    line.color = new Color(PlayerUi.Blue.r, PlayerUi.Blue.g, PlayerUi.Blue.b, .30f);
                }
            }
            float statusTop = victory ? 136 : reportHeight - 80, statusHeight = victory ? 40 : 48;
            resultRewardStatus = NotebookLabel(report, "", R(18, statusTop, reportWidth - 36, statusHeight), F(20), alignment: TextAnchor.MiddleCenter);
            resultRewardStatus.name = "Result reward status";
            resultSavedMark = PlayerUi.Graphic<PlayerIconGraphic>(report, "Result saved confirmation", R(20, statusTop + 5, 28, 28));
            resultSavedMark.Icon = PlayerIcon.Check; resultSavedMark.Sketch = true;
            resultSavedMark.color = new Color32(32, 103, 60, 255);
            resultStatusFullBounds = R(18, statusTop, reportWidth - 36, statusHeight);
            float saveWidth = victory ? 320 : 210;
            resultStatusRetryBounds = R(18, statusTop, reportWidth - saveWidth - 54, statusHeight);
            resultReceipt = NotebookLabel(report, "", R(18, reportHeight - 31, reportWidth - 36, 27), F(17), PlayerUi.Muted, TextAnchor.MiddleCenter);
            resultReceipt.name = "Result receipt";
            resultRetrySave = NotebookAction(report, "Result retry save", R(reportWidth - saveWidth - 18, statusTop, saveWidth, victory ? 40 : 46),
                PlayerIcon.Save, "Повторить сохранение", () => { game.Run.RetrySave(out _); RefreshAlbumResult(); }, true);
            float buttonWidth = victory ? 330 : 260, gap = victory ? 42 : 18, footer = 816, left = victory ? 263 : 253;
            resultHome = NotebookAction(composition, "Result home", R(24, 16, 218, 54), PlayerIcon.Home,
                JourneyCopy("result.home"), game.ReturnToHome);
            float navigationLeft = victory ? left : left + buttonWidth + gap;
            NotebookAction(composition, "Result map", victory ? R(navigationLeft, footer, buttonWidth, 64) : R(1114, 758, 312, 48), PlayerIcon.Map,
                "На карту", game.ReturnToMap);
            var album = NotebookAction(composition, "Result album", victory ? R(navigationLeft + buttonWidth + gap, footer, buttonWidth, 64) : R(1026, 674, 488, 56), PlayerIcon.Book,
                JourneyCopy("result.album"), game.ReturnToAlbumFromResult);
            NotebookControlIcon(album, "icon.workshop");
            NotebookAction(composition, "Result retry", victory ? R(navigationLeft + (buttonWidth + gap) * 2, footer, buttonWidth, 64) : R(1026, 598, 488, 64), PlayerIcon.Restart,
                "Ещё попытка", game.Retry, true);
            RefreshAlbumResult();
            EventSystem.current?.SetSelectedGameObject(resultHome.gameObject);
        }

        void ResultLandscape(Transform parent, float scale, bool victory)
        {
            Rect R(float x, float y, float width, float height) => new Rect(x * scale, y * scale, width * scale, height * scale);
            // Reuse the separate game sprites. No concept screen is baked behind live reward labels.
            void Scenery(string id, Rect bounds)
            {
                var art = ui.Art(parent, id, bounds);
                art.color = new Color(1, 1, 1, victory ? .40f : .16f);
            }
            for (int side = 0; side < 2; side++)
            {
                float edge = side == 0 ? -34 : 1320;
                Scenery("terrain.mountain", R(edge - 34, 40, 330, 174));
                Scenery("terrain.ridge", R(edge + (side == 0 ? 182 : -220), 146, 270, 112));
                for (int tree = 0; tree < 5; tree++)
                {
                    float treeWidth = 64 + tree % 3 * 18;
                    float x = side == 0 ? 6 + tree % 3 * 82 : 1510 - tree % 3 * 78;
                    float y = 254 + tree * 116;
                    Scenery("terrain.pine", R(x, y, treeWidth, treeWidth * 1.5f));
                    Scenery("terrain.shrub", R(x + 30, y + treeWidth * 1.1f, 100, 58));
                    if (tree % 2 == 1) Scenery("terrain.rock", R(x - 20, y + treeWidth * 1.1f, 100, 72));
                }
            }
            if (victory)
            {
                ResultEmblem(parent, NotebookResultEmblem.Courtyard, R(300, 474, 1000, 136));
                var frozen = game.Run.RunLoadout?.Entries.Where(entry => entry != null).ToArray() ?? System.Array.Empty<AlbumEntry>();
                var towers = frozen.Where(entry => entry.Kind == AlbumEntryKind.Tower || entry.Kind == AlbumEntryKind.Legacy).Take(4).ToArray();
                Rect[] towerBounds = { R(326, 432, 232, 158), R(468, 374, 238, 170), R(1042, 432, 232, 158), R(894, 374, 238, 170) };
                for (int i = 0; i < towers.Length; i++) ResultFrozenAssembly(parent, towers[i], towerBounds[i]);
                var wall = frozen.FirstOrDefault(entry => entry.Snapshot?.Recipe.Structure?.Family == AssemblyStructureFamily.Wall);
                ResultFrozenAssembly(parent, wall, R(400, 512, 170, 90));
                ResultFrozenAssembly(parent, wall, R(1030, 512, 170, 90));
                ui.Art(parent, "object.castle", R(586, 368, 428, 232));
                ResultFrozenAssembly(parent, frozen.FirstOrDefault(entry => entry.Snapshot?.Recipe.Structure?.Family == AssemblyStructureFamily.Repair), R(526, 502, 162, 100));
                ResultFrozenAssembly(parent, frozen.FirstOrDefault(entry => entry.Snapshot?.Recipe.Structure?.Family == AssemblyStructureFamily.Generator), R(912, 506, 144, 96));
                ResultAtlasIcon(parent, "icon.flag", R(306, 418, 68, 92));
                ResultAtlasIcon(parent, "icon.flag", R(1230, 430, 68, 92));
                for (int fallen = 0; fallen < 4; fallen++)
                {
                    float y = 336 + fallen * 116;
                    ResultFallen(parent, fallen % 2 == 0 ? "doodle" : "brute", R(180 + fallen % 2 * 64, y, 100, 64), -78);
                    ResultFallen(parent, fallen % 2 == 0 ? "runner" : "doodle", R(1292 + fallen % 2 * 64, y + 24, 100, 64), 82);
                }
            }
            else
            {
                NotebookSprite(parent, "Defeat castle rubble illustration", Resources.Load<Sprite>("Art/Player/DefeatCastleRubble"), R(34, 264, 964, 540));
            }
        }

        Image ResultAtlasIcon(Transform parent, string id, Rect bounds)
        {
            NotebookSpriteCatalog.TryGet(id, out var sprite);
            return NotebookSprite(parent, "Result " + id + " illustration", sprite, bounds);
        }

        void ResultFallen(Transform parent, string family, Rect bounds, float fallbackRoll)
        {
            string baseShape = "enemy." + family + ".sketch.idle", deathClip = "enemy." + family + ".sketch.death";
            var clip = NotebookVisualThemeAsset.Default?.FindClip(deathClip);
            var art = ui.Art(parent, baseShape, bounds);
            art.name = "Result fallen " + family;
            art.Bind(baseShape, deathClip);
            art.SetTime((clip?.Duration ?? 0) * .7f);
            // Authored death clips currently fade upright figures. The epilogue's static
            // decorations lie on the ground; this roll never reaches live combat actors.
            if (clip == null || clip.Frames.All(frame => Mathf.Abs(frame.Rotation) < .001f))
            {
                art.rectTransform.pivot = new Vector2(.5f, .5f);
                art.rectTransform.anchoredPosition += new Vector2(bounds.width * .5f, -bounds.height * .5f);
                art.rectTransform.localEulerAngles = new Vector3(0, 0, fallbackRoll);
            }
        }

        void ResultFrozenAssembly(Transform parent, AlbumEntry entry, Rect bounds)
        {
            if (entry == null) return;
            if (entry.Kind == AlbumEntryKind.Legacy)
            { NotebookBlueprintIllustration(parent, entry.LegacyBlueprint, bounds); return; }
            var drawing = PlayerUi.Graphic<WeaponAssemblyGraphic>(parent, "Result frozen assembly " + entry.Id, bounds);
            drawing.SetRecipe(entry.Snapshot);
        }

        void RefreshAlbumResult()
        {
            if (resultGoldReward == null || game.Page != PlayerPage.Result) return;
            var session = game.Run.Session;
            bool victory = session.Phase == RunPhase.Victory;
            var totals = RunRewardTotals.Calculate(session.CompletedWaves, victory);
            var receipt = game.Run.Workshop.FindRunReward(session.RunId);
            bool saved = receipt != null && receipt.Waves >= session.CompletedWaves && (!victory || receipt.VictoryBonus);
            resultCompletedWaves.text = victory ? "Волны\n" + session.CompletedWaves + " / " + session.Balance.Waves.Count
                : JourneyCopy("result.waves", session.CompletedWaves, session.Balance.Waves.Count);
            string[] rewardColumns = JourneyCopy("result.totals", totals.Gold, totals.TotalXp)
                .Split(new[] { "    " }, System.StringSplitOptions.None);
            resultGoldReward.text = rewardColumns[0];
            resultXpReward.text = rewardColumns.Length > 1 ? rewardColumns[1] : "";
            if (resultDefeatWave != null) resultDefeatWave.text = session.WaveIndex > 0
                ? "Поражение на волне " + session.WaveIndex : "Поражение до первой волны";
            resultProgress.text = ProgressCopy();
            if (resultXpFill != null)
            {
                var levelProgress = MetaProgression.GetLevelProgress(game.Run.Workshop.TotalXp);
                var fill = resultXpTrack; fill.width *= levelProgress.Normalized;
                PlayerUi.Place(resultXpFill.rectTransform, fill);
                resultXpCaption.text = levelProgress.NextLevelXp.HasValue
                    ? levelProgress.TotalXp + " / " + levelProgress.NextLevelXp.Value + " XP"
                    : levelProgress.TotalXp + " XP · максимум";
            }
            resultRewardStatus.text = session.CompletedWaves == 0 ? JourneyCopy("result.no_reward") : saved
                ? game.Run.Workshop.SaveState == ProfileSaveState.MemoryOnly ? GameText.Resolve(game.Run.Workshop.SaveMessage) : JourneyCopy("result.saved")
                : JourneyCopy("result.pending");
            resultRewardStatus.color = saved ? new Color32(32, 103, 60, 255) : PlayerUi.Ink;
            resultSavedMark.enabled = saved && game.Run.Workshop.SaveState != ProfileSaveState.MemoryOnly && session.CompletedWaves > 0;
            resultReceipt.text = receipt != null ? JourneyCopy("result.receipt", receipt.Gold, receipt.TotalXp) : "";
            bool retry = game.Run.HasPendingRunRewards || game.Run.Workshop.HasUnsavedChanges;
            PlayerUi.Place(resultRewardStatus.rectTransform, retry ? resultStatusRetryBounds : resultStatusFullBounds);
            if (resultRetrySave.gameObject.activeSelf != retry)
            {
                bool hadFocus = EventSystem.current?.currentSelectedGameObject == resultRetrySave.gameObject;
                resultRetrySave.gameObject.SetActive(retry);
                if (!retry && hadFocus) EventSystem.current?.SetSelectedGameObject(resultHome.gameObject);
            }
        }

        NotebookResultEmblemGraphic ResultEmblem(Transform parent, NotebookResultEmblem kind, Rect bounds)
        {
            var emblem = PlayerUi.Graphic<NotebookResultEmblemGraphic>(parent, "Result " + kind, bounds);
            emblem.Kind = kind; return emblem;
        }

        void OpenAlbumResultStatistics()
        {
            var content = Sheet("Статистика миссии", 1040, 590, out float sheetWidth);
            float width = sheetWidth - 48;
            var modal = content.parent.parent.gameObject;
            var shield = page.Find("Modal input shield").gameObject;
            System.Action close = () =>
            {
                modal.SetActive(false); shield.SetActive(false); Destroy(modal); Destroy(shield);
            };
            var stats = game.Run.Session.Statistics;
            NotebookLabel(content, ui.T("result_stats_left", stats.BuildInvestment + stats.UpgradeInvestment,
                stats.RefundedInk, stats.LostInvestment, stats.PeakAliveEnemies), new Rect(16, 8, width * .5f - 24, 240), 23);
            NotebookLabel(content, ui.T("result_stats_right", Mathf.RoundToInt(stats.TotalDamage), stats.TotalStructuresBuilt,
                stats.DestroyedStructures, stats.Upgrades.Count), new Rect(width * .5f + 8, 8, width * .5f - 24, 240), 23);
            NotebookAction(content, "Close result statistics", new Rect(16, 310, width - 32, 52), PlayerIcon.Back,
                "Вернуться к итогам", close, true);
            ScopeSheet(content, close);
        }
    }

    public enum NotebookResultEmblem { Ribbon, Coin, XpStar, Laurel, TornRibbon, Courtyard }

    /// <summary>Small pen ornaments around live result labels; no reward text is baked into artwork.</summary>
    public sealed class NotebookResultEmblemGraphic : MaskableGraphic
    {
        public NotebookResultEmblem Kind;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var pen = new MeshPen(null, 4096);
            Rect bounds = rectTransform.rect;
            Color ink = PlayerUi.Ink, paper = PlayerUi.Paper;
            float stroke = Mathf.Max(.8f, Mathf.Min(bounds.width, bounds.height) * .018f);
            Vector2 P(float x, float y) => new Vector2((x - .5f) * bounds.width, (.5f - y) * bounds.height);
            void Line(float x, float y, float a, float b) => pen.Line(P(x, y), P(a, b), stroke, ink);
            void Polygon(Vector2[] points, Color fill)
            {
                Vector2 center = Vector2.zero;
                foreach (var point in points) center += point;
                center /= points.Length;
                for (int i = 0; i < points.Length; i++)
                {
                    Vector2 a = points[i], b = points[(i + 1) % points.Length];
                    pen.Triangle(center, a, b, fill); pen.Line(a, b, stroke, ink);
                }
            }
            void FineLine(float x, float y, float a, float b, float opacity = .55f)
                => pen.Line(P(x, y), P(a, b), stroke * .52f, new Color(ink.r, ink.g, ink.b, opacity));
            void FoldHatching(bool right)
            {
                // Keep the handwritten title area clear; dense strokes belong to the folded ends.
                for (int hatch = 0; hatch < 12; hatch++)
                {
                    float x = .030f + hatch * .0062f, y = .39f + Mathf.Sin(hatch * 1.7f) * .009f;
                    float a = x + .028f, b = y + .23f;
                    FineLine(right ? 1 - x : x, y, right ? 1 - a : a, b, .72f);
                }
            }
            if (Kind == NotebookResultEmblem.Courtyard)
            {
                Color ground = new Color(ink.r, ink.g, ink.b, .22f);
                pen.Ring(Vector2.zero, bounds.width * .48f, stroke, ground, 64, bounds.height / bounds.width * .86f, dashed: true);
                for (int hatch = 0; hatch < 14; hatch++)
                {
                    float x = .10f + hatch * .06f;
                    pen.Line(P(x, .72f), P(x + .02f, .78f), stroke, ground);
                }
            }
            else if (Kind == NotebookResultEmblem.TornRibbon)
            {
                Polygon(new[] { P(.04f, .22f), P(.20f, .10f), P(.48f, .06f), P(.74f, .10f), P(.95f, .20f),
                    P(.91f, .31f), P(.96f, .35f), P(.92f, .47f), P(.94f, .74f), P(.76f, .82f),
                    P(.49f, .84f), P(.22f, .91f), P(.06f, .96f), P(.09f, .78f), P(.035f, .81f), P(.07f, .59f) }, paper);
                Line(.05f, .16f, .13f, .28f); Line(.06f, .31f, .14f, .20f);
                Line(.88f, .16f, .97f, .24f); Line(.90f, .84f, .98f, .88f);
                for (int slash = 0; slash < 5; slash++)
                    Line(.035f + slash * .012f, .41f, .09f + slash * .012f, .48f);
                for (int hatch = 0; hatch < 12; hatch++)
                {
                    float y = .27f + hatch * .044f;
                    FineLine(.069f, y, .093f, y + .045f, .7f);
                    FineLine(.924f, y - .045f, .945f, y - .005f, .6f);
                }
                FineLine(.18f, .125f, .30f, .095f, .46f);
                FineLine(.71f, .12f, .86f, .16f, .46f);
                FineLine(.17f, .861f, .32f, .848f, .42f);
                FineLine(.70f, .801f, .86f, .766f, .42f);
                // Fibres and pen flecks sit beside the tear rather than across live lettering.
                for (int fleck = 0; fleck < 9; fleck++)
                {
                    float y = .17f + fleck * .079f;
                    FineLine(.024f, y, .035f, y + .014f, .75f);
                    FineLine(.969f, y + .019f, .982f, y + .006f, .65f);
                }
            }
            else if (Kind == NotebookResultEmblem.Ribbon)
            {
                Polygon(new[] { P(.12f, .28f), P(.01f, .34f), P(.06f, .59f), P(.01f, .89f), P(.17f, .82f) }, paper);
                Polygon(new[] { P(.88f, .28f), P(.99f, .34f), P(.94f, .59f), P(.99f, .89f), P(.83f, .82f) }, paper);
                FoldHatching(false); FoldHatching(true);
                var band = new Vector2[42];
                for (int point = 0; point <= 20; point++)
                {
                    float t = point / 20f;
                    band[point] = P(.10f + t * .80f, .18f - Mathf.Sin(t * Mathf.PI) * .13f);
                    band[41 - point] = P(.12f + t * .76f, .88f + Mathf.Sin(t * Mathf.PI) * .06f);
                }
                Polygon(band, paper);
                Line(.10f, .18f, .12f, .88f); Line(.90f, .18f, .88f, .88f);
                for (int point = 0; point < 20; point++)
                {
                    float a = point / 20f, b = (point + 1) / 20f;
                    FineLine(.108f + a * .784f, .207f - Mathf.Sin(a * Mathf.PI) * .13f,
                        .108f + b * .784f, .207f - Mathf.Sin(b * Mathf.PI) * .13f, .35f);
                    FineLine(.13f + a * .74f, .852f + Mathf.Sin(a * Mathf.PI) * .06f,
                        .13f + b * .74f, .852f + Mathf.Sin(b * Mathf.PI) * .06f, .38f);
                }
                for (int hatch = 0; hatch < 8; hatch++)
                {
                    float y = .28f + hatch * .065f;
                    FineLine(.102f, y, .115f, y + .035f, .7f);
                    FineLine(.898f, y, .885f, y + .035f, .7f);
                }
            }
            else if (Kind == NotebookResultEmblem.Laurel)
            {
                Vector2 Branch(float t)
                {
                    float u = 1 - t;
                    return new Vector2(.77f, .94f) * (u * u * u) + new Vector2(.13f, .64f) * (3 * u * u * t)
                        + new Vector2(.21f, .28f) * (3 * u * t * t) + new Vector2(.44f, .03f) * (t * t * t);
                }
                for (int step = 0; step < 22; step++)
                {
                    Vector2 a = Branch(step / 22f), b = Branch((step + 1) / 22f);
                    Line(a.x, a.y, b.x, b.y);
                    FineLine(a.x + .020f, a.y, b.x + .020f, b.y, .5f);
                }
                for (int leaf = 0; leaf < 6; leaf++)
                for (int side = 0; side < 2; side++)
                {
                    Vector2 root = Branch(.15f + leaf * .135f);
                    float sign = side == 0 ? -1 : 1;
                    Vector2 tip = root + new Vector2(sign * .26f, -.13f);
                    Vector2 middle = Vector2.Lerp(root, tip, .54f);
                    Polygon(new[] { P(root.x, root.y), P(middle.x - sign * .03f, middle.y - .04f), P(tip.x, tip.y),
                        P(middle.x + sign * .05f, middle.y + .04f) }, new Color32(239, 237, 211, 255));
                    Line(root.x, root.y, tip.x, tip.y);
                    for (int vein = 0; vein < 3; vein++)
                    {
                        Vector2 baseVein = Vector2.Lerp(root, tip, .28f + vein * .18f);
                        FineLine(baseVein.x, baseVein.y, baseVein.x - sign * .035f, baseVein.y - .036f, .8f);
                        FineLine(baseVein.x, baseVein.y, baseVein.x + sign * .042f, baseVein.y + .025f, .75f);
                    }
                }
            }
            else
            {
                float radius = Mathf.Min(bounds.width, bounds.height) * .44f;
                if (Kind == NotebookResultEmblem.Coin)
                {
                    pen.Disc(Vector2.zero, radius, new Color32(250, 217, 102, 255), 32, 1.13f);
                    pen.Ring(Vector2.zero, radius, stroke * 1.4f, ink, 32, 1.13f);
                    Color gold = new Color32(172, 116, 25, 255);
                    pen.Ring(new Vector2(radius * .035f, radius * .025f), radius * .79f, stroke * .72f, gold, 40, 1.13f);
                    pen.Ring(new Vector2(-radius * .025f, radius * .035f), radius * .67f, stroke * .45f, new Color32(255, 242, 175, 255), 40, 1.13f);
                    for (int rim = 0; rim < 24; rim++)
                    {
                        float angle = rim * Mathf.PI / 12;
                        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 1.13f);
                        pen.Line(direction * radius * .85f, direction * radius * .94f, stroke * .45f, gold);
                    }
                    // Engraved crown: the coin remains a small symbol, not an extra text label.
                    Vector2[] crown = { new Vector2(-.43f, .19f), new Vector2(-.21f, -.04f), new Vector2(0, .31f),
                        new Vector2(.21f, -.04f), new Vector2(.43f, .19f), new Vector2(.31f, -.31f), new Vector2(-.31f, -.31f) };
                    for (int point = 0; point < crown.Length; point++)
                        pen.Line(crown[point] * radius, crown[(point + 1) % crown.Length] * radius, stroke * .9f, gold);
                    for (int hatch = 0; hatch < 5; hatch++)
                        pen.Line(new Vector2(-.29f + hatch * .12f, -.31f) * radius,
                            new Vector2(-.22f + hatch * .10f, -.15f) * radius, stroke * .46f, gold);
                }
                else
                {
                    var points = new Vector2[12];
                    for (int i = 0; i < points.Length; i++)
                    {
                        float angle = Mathf.PI * .5f + i * Mathf.PI / 6;
                        points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * (i % 2 == 0 ? 1 : .70f);
                    }
                    Polygon(points, ink);
                    for (int point = 0; point < points.Length; point++)
                    {
                        Vector2 a = points[point] * .77f, b = points[(point + 1) % points.Length] * .77f;
                        pen.Line(a, b, stroke * .62f, new Color32(235, 236, 210, 255));
                        Vector2 inner = points[point] * .84f, outer = points[point] * .96f;
                        pen.Line(inner, outer, stroke * .45f, new Color32(113, 147, 203, 255));
                    }
                }
            }
            pen.CopyTo(vh, bounds.center, 1);
        }
    }
}
