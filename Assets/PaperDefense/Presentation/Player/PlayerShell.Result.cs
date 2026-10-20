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
        Text resultRewardTotals, resultCompletedWaves, resultRewardStatus, resultReceipt, resultProgress, resultXpCaption, resultDefeatWave;
        Image resultXpFill;
        Rect resultXpTrack;
        Button resultRetrySave, resultHome;
        Rect resultStatusFullBounds, resultStatusRetryBounds;

        void ResetAlbumResult()
        {
            resultRewardTotals = resultCompletedWaves = resultRewardStatus = resultReceipt = resultProgress = null;
            resultXpCaption = resultDefeatWave = null; resultXpFill = null;
            resultRetrySave = resultHome = null;
        }

        void AlbumResult()
        {
            bool victory = game.Run.Session.Phase == RunPhase.Victory;
            NotebookPageBackdrop("Art/Player/FoundationPageDecor", "Result landscape border");
            if (!victory)
            {
                var border = page.Find("Result landscape border")?.GetComponent<Image>();
                if (border != null) border.color = new Color(1, 1, 1, .22f);
            }
            float scale = Mathf.Min(W / 1600f, H / 900f);
            var composition = ui.Panel(page, "Result composition", new Rect((W - 1600 * scale) * .5f,
                (H - 900 * scale) * .5f, 1600 * scale, 900 * scale), Color.clear);
            Rect R(float x, float y, float width, float height) => new Rect(x * scale, y * scale, width * scale, height * scale);
            int F(float size) => Mathf.Max(16, Mathf.RoundToInt(size * scale));
            ResultLandscape(composition, scale, victory);
            if (!victory) ui.Panel(composition, "Defeat results sheet", R(1006, 70, 528, 756), PlayerUi.Card);

            var banner = ui.Panel(composition, "Result title ribbon", victory ? R(365, 4, 870, 118) : R(140, 62, 810, 150), Color.clear);
            ResultEmblem(banner, NotebookResultEmblem.Ribbon, victory ? R(0, 0, 870, 118) : R(0, 0, 810, 150));
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
            resultRewardTotals = NotebookLabel(rewards, "", victory ? R(286, 4, 510, 64) : R(76, 100, 392, 114),
                F(victory ? 35 : 38), alignment: victory ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
            resultRewardTotals.name = "Result reward totals";
            if (victory)
            {
                ResultAtlasIcon(rewards, "icon.flag", R(12, 9, 44, 60));
                PlayerUi.Place(resultCompletedWaves.rectTransform, R(62, 4, 214, 68));
                ResultEmblem(rewards, NotebookResultEmblem.Coin, R(278, 18, 42, 46));
                PlayerUi.Place(resultRewardTotals.rectTransform, R(332, 4, 430, 64));
                resultRewardTotals.resizeTextMaxSize = F(31);
                var star = ResultEmblem(rewards, NotebookResultEmblem.XpStar, R(770, 20, 38, 42));
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
                NotebookLabel(breakdown, JourneyCopy("result.breakdown", totals.WaveGold, totals.WaveXp, totals.VictoryGold, totals.VictoryXp),
                    R(16, 0, 868, 48), F(23), PlayerUi.Ink, TextAnchor.MiddleCenter);
            }
            var progressSheet = ui.Panel(victory ? composition : rewards, "Result progress sheet", victory ? R(350, 292, 900, 76) : R(14, 238, 460, 30), victory ? PlayerUi.Card : Color.clear);
            resultProgress = NotebookLabel(progressSheet, "", victory ? R(20, 0, 860, 34) : R(6, 0, 448, 30),
                F(victory ? 27 : 23), alignment: TextAnchor.MiddleCenter);
            resultProgress.name = "Result progress";
            if (victory)
            {
                ResultAtlasIcon(progressSheet, "icon.shield", R(20, 36, 34, 30));
                resultXpCaption = NotebookLabel(progressSheet, "", R(62, 34, 240, 32), F(21));
                resultXpCaption.name = "Result XP interval";
                var track = ui.Panel(progressSheet, "Result XP track", R(316, 41, 550, 21), PlayerUi.Paper);
                resultXpTrack = new Rect(3 * scale, 3 * scale, 544 * scale, 15 * scale);
                resultXpFill = PlayerUi.Graphic<Image>(track, "Result XP progress", resultXpTrack);
                resultXpFill.color = NotebookPalette.Blue;
            }
            bool pendingSave = game.Run.HasPendingRunRewards || game.Run.Workshop.HasUnsavedChanges;
            float reportWidth = victory ? pendingSave ? 900 : 600 : 488, reportHeight = victory ? 210 : 208;
            var report = ui.Panel(composition, "Result mission report", victory ? R((1600 - reportWidth) * .5f, 594, reportWidth, reportHeight) : R(1026, 378, reportWidth, reportHeight), PlayerUi.Card);
            NotebookAction(report, "Result statistics", R(16, victory ? 90 : 4, reportWidth - 32, 44), PlayerIcon.Book,
                "Статистика миссии", OpenAlbumResultStatistics);
            var stats = game.Run.Session.Statistics;
            string reportCopy = ui.T("result_stats_left", stats.BuildInvestment + stats.UpgradeInvestment, stats.RefundedInk,
                stats.LostInvestment, stats.PeakAliveEnemies);
            if (victory)
            {
                string[] financialRows = reportCopy.Split('\n');
                for (int metric = 0; metric < 3 && metric < financialRows.Length; metric++)
                {
                    ResultAtlasIcon(report, "icon.pencil", R(17, 4 + metric * 27, 27, 27));
                    NotebookLabel(report, financialRows[metric], R(52, 4 + metric * 27, reportWidth - 70, 27), F(23), PlayerUi.Ink);
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
                NotebookLabel(report, completeHistory ? "Потеряно башен · " + lostTowers : "Потеряно построек · " + stats.DestroyedStructures,
                    R(18, 54, reportWidth - 36, 32), F(24)).name = completeHistory ? "Result lost towers" : "Result total losses";
                NotebookLabel(report, completeHistory ? "Потеряно сооружений · " + (stats.DestroyedStructures - lostTowers) : "Утрачено штрихов · " + stats.LostInvestment,
                    R(18, 90, reportWidth - 36, 32), F(24)).name = completeHistory ? "Result lost supports" : "Result lost investment";
            }
            float statusTop = victory ? 136 : reportHeight - 80, statusHeight = victory ? 40 : 48;
            resultRewardStatus = NotebookLabel(report, "", R(18, statusTop, reportWidth - 36, statusHeight), F(20), alignment: TextAnchor.MiddleCenter);
            resultRewardStatus.name = "Result reward status";
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
            NotebookAction(composition, "Result album", victory ? R(navigationLeft + buttonWidth + gap, footer, buttonWidth, 64) : R(1026, 674, 488, 56), PlayerIcon.Book,
                JourneyCopy("result.album"), game.ReturnToAlbumFromResult);
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
                if (!victory) art.color = new Color(1, 1, 1, .22f);
            }
            for (int side = 0; side < 2; side++)
            {
                float edge = side == 0 ? -34 : 1320;
                Scenery("terrain.mountain", R(edge - 34, 40, 390, 240));
                Scenery("terrain.ridge", R(edge + (side == 0 ? 182 : -220), 152, 310, 154));
                for (int tree = 0; tree < 8; tree++)
                {
                    float treeWidth = 74 + tree % 3 * 22;
                    float x = side == 0 ? 6 + tree % 3 * 82 : 1510 - tree % 3 * 78;
                    float y = 224 + tree * 73;
                    Scenery("terrain.pine", R(x, y, treeWidth, treeWidth * 1.5f));
                    Scenery("terrain.shrub", R(x + 30, y + treeWidth * 1.1f, 100, 58));
                    if (tree % 2 == 1) Scenery("terrain.rock", R(x - 20, y + treeWidth * 1.1f, 100, 72));
                }
            }
            if (victory)
            {
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
            if (resultRewardTotals == null || game.Page != PlayerPage.Result) return;
            var session = game.Run.Session;
            bool victory = session.Phase == RunPhase.Victory;
            var totals = RunRewardTotals.Calculate(session.CompletedWaves, victory);
            var receipt = game.Run.Workshop.FindRunReward(session.RunId);
            bool saved = receipt != null && receipt.Waves >= session.CompletedWaves && (!victory || receipt.VictoryBonus);
            resultCompletedWaves.text = victory ? "Волны\n" + session.CompletedWaves + " / " + session.Balance.Waves.Count
                : JourneyCopy("result.waves", session.CompletedWaves, session.Balance.Waves.Count);
            resultRewardTotals.text = JourneyCopy("result.totals", totals.Gold, totals.TotalXp);
            if (!victory) resultRewardTotals.text = resultRewardTotals.text.Replace("    ", "\n");
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

    public enum NotebookResultEmblem { Ribbon, Coin, XpStar, Laurel }

    /// <summary>Small pen ornaments around live result labels; no reward text is baked into artwork.</summary>
    public sealed class NotebookResultEmblemGraphic : MaskableGraphic
    {
        public NotebookResultEmblem Kind;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var pen = new MeshPen(null, 512);
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
            if (Kind == NotebookResultEmblem.Ribbon)
            {
                Polygon(new[] { P(.12f, .28f), P(.01f, .34f), P(.06f, .59f), P(.01f, .89f), P(.17f, .82f) }, paper);
                Polygon(new[] { P(.88f, .28f), P(.99f, .34f), P(.94f, .59f), P(.99f, .89f), P(.83f, .82f) }, paper);
                Polygon(new[] { P(.10f, .18f), P(.27f, .075f), P(.51f, .04f), P(.75f, .08f), P(.90f, .18f),
                    P(.88f, .88f), P(.67f, .94f), P(.43f, .94f), P(.12f, .88f) }, paper);
                Line(.10f, .18f, .12f, .88f); Line(.90f, .18f, .88f, .88f);
                for (int i = 0; i < 5; i++)
                {
                    float offset = i * .018f;
                    Line(.027f + offset, .40f, .074f + offset, .47f);
                    Line(.973f - offset, .40f, .926f - offset, .47f);
                }
            }
            else if (Kind == NotebookResultEmblem.Laurel)
            {
                Line(.77f, .94f, .39f, .66f); Line(.39f, .66f, .27f, .36f); Line(.27f, .36f, .43f, .06f);
                for (int leaf = 0; leaf < 5; leaf++)
                {
                    float y = .18f + leaf * .14f, x = .30f + Mathf.Max(0, y - .38f) * .6f;
                    Polygon(new[] { P(x, y + .05f), P(x - .23f, y - .10f), P(x - .18f, y + .07f) }, paper);
                    Polygon(new[] { P(x + .02f, y + .05f), P(x + .30f, y - .03f), P(x + .14f, y + .15f) }, paper);
                }
            }
            else
            {
                float radius = Mathf.Min(bounds.width, bounds.height) * .44f;
                if (Kind == NotebookResultEmblem.Coin)
                {
                    pen.Disc(Vector2.zero, radius, new Color32(250, 217, 102, 255), 32, 1.13f);
                    pen.Ring(Vector2.zero, radius, stroke * 1.4f, ink, 32, 1.13f);
                    pen.Ring(Vector2.zero, radius * .73f, stroke, new Color32(172, 116, 25, 255), 32, 1.13f);
                    for (int hatch = 0; hatch < 3; hatch++)
                        pen.Line(new Vector2(-radius * .36f + hatch * radius * .15f, -radius * .45f),
                            new Vector2(radius * .25f + hatch * radius * .15f, radius * .45f), stroke, new Color32(195, 143, 38, 255));
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
                }
            }
            pen.CopyTo(vh, bounds.center, 1);
        }
    }
}
