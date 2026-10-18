using System;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Five socket categories for a local foundation or weapon edit; the enclosing sheet owns save/cancel.</summary>
    public sealed class ComponentAssemblyPanel
    {
        readonly WorkshopUi ui;
        readonly WorkshopDraftSession draft;
        readonly RectTransform tabs;
        float width = 976, height = 538;
        public RectTransform Root { get; }
        public ComponentCatalogPanel Catalog { get; }

        public ComponentAssemblyPanel(Transform parent, RunController run, WorkshopDraftSession draft,
            UserTextResolver resolver, Action changed)
        {
            this.draft = draft ?? throw new ArgumentNullException(nameof(draft));
            ui = new WorkshopUi(resolver);
            Root = ui.Panel(parent, "Component assembly", new Rect(0, 0, width, height), Color.clear);
            tabs = ui.Panel(Root, "Component categories", new Rect(0, 0, width, 54), Color.clear);
            Catalog = new ComponentCatalogPanel(Root, run, draft, resolver, changed);
            Refresh();
        }

        public void Show() { Root.gameObject.SetActive(true); Refresh(); }
        public void Hide() { Catalog.ClosePurchase(); Root.gameObject.SetActive(false); }
        public void Reflow(float width, float height)
        {
            this.width = Mathf.Max(300, width); this.height = Mathf.Max(260, height);
            WorkshopUi.Place(Root, new Rect(0, 0, this.width, this.height)); Refresh();
        }
        public void Refresh()
        {
            WorkshopUi.ClearViews(tabs);
            WorkshopUi.Place(tabs, new Rect(0, 0, width, 54));
            int count = draft.Categories.Count;
            float tabWidth = (width - Mathf.Max(0, count - 1) * 8) / Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                var category = draft.Categories[i];
                var button = ui.Button(tabs, "Component category " + category, new Rect(i * (tabWidth + 8), 0, tabWidth, 50),
                    UserText.Message("assembly.component.category." + category), () => { draft.SelectCategory(category); Refresh(); });
                ui.SetSelected(button, draft.ActiveCategory == category);
            }
            WorkshopUi.Place(Catalog.Root, new Rect(0, 66, width, height - 66));
            Catalog.Reflow(width, height - 66);
        }
    }
}
