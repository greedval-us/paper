using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        static Font coverLettering;
        static Texture2D fortressCover;
        static Texture2D FortressCover => fortressCover != null ? fortressCover : fortressCover =
            Resources.Load<Texture2D>("FieldHeadquarters/FortressCover");

        void BuildNotebookHome()
        {
            NotebookPageBackdrop("Home illustration");
            bool assembly = game.Run.AssemblyCatalog != null;
            bool continuation = game.Run.HasMissionCheckpoint;
            float continuationHeight = continuation ? 132 : 0;
            bool compact = W < 1060;
            float left = Mathf.Clamp(W * .055f, 24, 94);
            float actionWidth = compact ? W - left * 2 : Mathf.Clamp(W * .35f, 385, 600);
            float top = compact ? Mathf.Max(72, (H - 512) * .38f) : Mathf.Max(65, (H - 510) * .48f);
            float actionTop = compact ? Mathf.Max(235, H * .53f) : top + 224;
            // Leave space for the fixed commands even in a short landscape window.
            actionTop = Mathf.Min(actionTop, H - 253 - continuationHeight);
            float titleWidth = compact ? W * .45f : actionWidth;
            float titleHeight = compact ? Mathf.Min(actionTop - top - 48, 156) : 162;
            AddCoverArt(compact, actionTop);
            var brand = ui.Panel(page, "Home wordmark", new Rect(left, top, titleWidth, titleHeight + 46), Color.clear);
            var title = HomeLettering(brand, "ДЕРЖИ\nЛИНИЮ", new Rect(0, 0, titleWidth, titleHeight),
                compact ? 72 : 84);
            if (coverLettering == null)
                coverLettering = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Liberation Serif", "Noto Serif", "Times New Roman" }, 84);
            if (coverLettering != null) title.font = coverLettering;
            title.fontStyle = FontStyle.Bold;
            title.lineSpacing = .84f;
            title.resizeTextMinSize = 24;
            var rule = PlayerUi.Graphic<Image>(brand, "Title underline", new Rect(2, titleHeight + 7, 42, 2));
            rule.color = FieldMenuSkin.Border;
            NotebookLabel(brand, "Собери свою оборону", new Rect(0, titleHeight + 18, titleWidth, 28),
                compact ? 19 : 22, FieldMenuSkin.Muted);

            var actions = ui.Panel(page, "Home actions", new Rect(left, actionTop, actionWidth, 240 + continuationHeight), Color.clear);
            Button resume = null, discard = null;
            if (continuation) resume = BuildHomeMissionContinuation(actions, actionWidth, out discard);
            var play = NotebookAction(actions, "Open campaign", new Rect(0, continuationHeight, actionWidth, continuation ? 52 : 64), PlayerIcon.Play,
                game.Run.CampaignEnabled ? "Открыть кампанию" : "Играть", game.OpenCampaign, !continuation);
            float half = (actionWidth - 12) * .5f;
            Button album = assembly ? NotebookAction(actions, "Open album", new Rect(0, continuationHeight + 76, half, 48), PlayerIcon.Book,
                GameText.Resolve(PaperDefense.Core.UserText.Message("assembly.player.album")), game.OpenAlbum) : null;
            var workshop = NotebookAction(actions, "workshop", new Rect(assembly ? half + 12 : 0, continuationHeight + 76, assembly ? half : actionWidth, 48), PlayerIcon.Pencil, ui.T("workshop"), () => game.Navigate(PlayerPage.Workshop));
            var settings = NotebookAction(actions, "settings", new Rect(0, continuationHeight + 134, half, 46), PlayerIcon.Settings, ui.T("settings"), () => game.OpenSettings());
            var help = NotebookAction(actions, "help", new Rect(half + 12, continuationHeight + 134, half, 46), PlayerIcon.Help, "Помощь", () => game.OpenSettings(true));
            var exit = NotebookAction(actions, "exit", new Rect(0, continuationHeight + 196, 145, 44), PlayerIcon.Exit, "Выйти",
                () => Confirm("Выйти из игры?", continuation ? "Сохранённая миссия, чертежи и золото останутся в профиле." : "Сохранённые чертежи и золото останутся в профиле.", () => UnityEngine.Application.Quit()),
                false);
            if (resume != null)
            {
                HomeNavigation(resume, null, play, null, discard);
                HomeNavigation(discard, null, play, resume);
            }
            HomeNavigation(play, resume != null && resume.interactable ? resume : discard, album ?? workshop);
            if (album != null) HomeNavigation(album, play, settings, null, workshop);
            HomeNavigation(workshop, play, assembly ? help : settings, album);
            HomeNavigation(settings, album ?? workshop, exit, null, help);
            HomeNavigation(help, workshop, exit, settings);
            HomeNavigation(exit, settings, null);

            if (assembly)
            {
                float profileRight = W - left;
                var profile = ui.Panel(page, "Home profile", new Rect(profileRight - 368, 12, 368, 38), PlayerUi.Paper);
                var coin = PlayerUi.Graphic<WorkshopIconGraphic>(profile, "Profile coin", new Rect(12, 8, 22, 22));
                coin.Icon = WorkshopIcon.Cost;
                coin.color = FieldMenuSkin.Accent;
                gold = NotebookLabel(profile, ui.T("gold", game.Run.Workshop.Gold), new Rect(42, 2, 118, 34), 17);
                gold.name = "Player gold";
                var divider = PlayerUi.Graphic<Image>(profile, "Profile divider", new Rect(166, 9, 1, 20));
                divider.color = FieldMenuSkin.Border;
                progress = NotebookLabel(profile, ProgressCopy(), new Rect(174, 2, 181, 34), 16,
                    FieldMenuSkin.Muted, TextAnchor.MiddleRight);
                progress.name = "Player progress";
            }
        }

        void AddCoverArt(bool compact, float actionTop)
        {
            var texture = FortressCover;
            if (texture == null) return;
            float ratio = texture.width / (float)texture.height;
            float width = compact ? Mathf.Min(W * .56f, (actionTop - 40) * ratio) : Mathf.Min(W * .63f, H * .84f * ratio);
            float height = width / ratio;
            float y = compact ? actionTop - height - 3 : (H - height) * .49f;
            var art = PlayerUi.Graphic<HomeCoverGraphic>(page, "Paper fortress engraving", new Rect(W - width, y, width, height));
            art.texture = texture;
        }

        static void HomeNavigation(Button button, Button up, Button down, Button left = null, Button right = null)
        {
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = up; navigation.selectOnDown = down;
            navigation.selectOnLeft = left; navigation.selectOnRight = right;
            button.navigation = navigation;
        }

        Text HomeLettering(Transform parent, string copy, Rect bounds, float fontSize)
        {
            var label = ui.Label(parent, copy, bounds, Mathf.Max(12, Mathf.RoundToInt(fontSize)), PlayerUi.Ink, title: true);
            PlayerUi.Place(label.rectTransform, bounds);
            label.resizeTextForBestFit = true;
            label.resizeTextMaxSize = Mathf.Max(12, Mathf.RoundToInt(fontSize));
            label.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(fontSize * .7f));
            return label;
        }

    }
}
