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
        public AlbumPanel Album { get; private set; }
        AlbumFilter albumFilter;
        string albumSelected;

        void RememberAlbumView()
        {
            if (Album?.Root != null) { albumFilter = Album.Filter; albumSelected = Album.SelectedId; }
            Album = null;
        }
        public void SelectAlbumOnReturn(string id)
        {
            if (id == null || !game.Run.Workshop.Entries.Any(e => e.Id == id)) return;
            albumSelected = id;
            var entry = game.Run.Workshop.Entries.First(e => e.Id == id);
            bool standard = game.Run.AssemblyCatalog.Starters.Any(e => e.Id == id);
            if (albumFilter == AlbumFilter.Mine && standard || albumFilter == AlbumFilter.Standard && !standard ||
                albumFilter == AlbumFilter.Towers && entry.Kind == AlbumEntryKind.Structure ||
                albumFilter == AlbumFilter.Structures && entry.Kind != AlbumEntryKind.Structure)
                albumFilter = AlbumFilter.All;
        }
        void BuildAlbum()
        {
            Album = new AlbumPanel(page, game.Run, TextResourcesAsset.Default, game.EditAlbumEntry, game.CloseAlbum);
            Album.Reflow(W, H); Album.SetFilter(albumFilter); Album.SelectEntry(albumSelected);
            var controls = Album.Root.GetComponentsInChildren<Selectable>();
            var focus = controls.FirstOrDefault(c => c.name == "Album choose " + Album.SelectedId && c.IsInteractable())
                ?? controls.FirstOrDefault(c => c.name == "Album filter " + Album.Filter && c.IsInteractable());
            if (focus != null) EventSystem.current?.SetSelectedGameObject(focus.gameObject);
        }
        void OpenAssemblyPicker(int slot)
        {
            var previous = EventSystem.current?.currentSelectedGameObject;
            var shade = ui.Panel(page, "Assembly album picker", new Rect(0, 0, W, H), PlayerUi.Paper, true);
            System.Action restore = () => {
                var replacement = page.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "Replace album entry " + slot);
                if (replacement != null) EventSystem.current?.SetSelectedGameObject(replacement.gameObject);
            };
            System.Action close = () => { WorkshopUi.RemoveView(shade.gameObject); restore(); };
            var picker = new AlbumPanel(shade, game.Run, TextResourcesAsset.Default,
                entry => { close(); game.EditAlbumEntry(entry); }, close,
                () => { close(); Show(PlayerPage.Preparation); restore(); }, slot);
            picker.Reflow(W, H);
            WorkshopModal.Open(shade.gameObject, close, previous);
        }

        string ProgressCopy() => GameText.Resolve(UserText.Message("assembly.player.progress",
            game.Run.Workshop.Level, game.Run.Workshop.TotalXp));
        void PlayerProgress(Rect bounds)
        {
            progress = NotebookLabel(page, ProgressCopy(), bounds, 20, alignment: TextAnchor.MiddleRight);
            progress.name = "Player progress";
        }
    }
}
