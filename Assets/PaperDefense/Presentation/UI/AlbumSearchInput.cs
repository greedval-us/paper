using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Keyboard shortcuts for the active album and its preparation picker.</summary>
    public sealed class AlbumSearchInput : InputField
    {
        static AlbumSearchInput dismissed;
        static int dismissalFrame = -1;
        internal Func<GameObject> RestoreFocus;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.ctrlKey.isPressed && keyboard.fKey.wasPressedThisFrame && IsInteractable())
                Focus();
        }

        internal void Focus()
        {
            Select();
            ActivateInputField();
        }

        public override void OnUpdateSelected(BaseEventData data)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && ConsumeEscape(transform))
            {
                data.Use();
                return;
            }
            base.OnUpdateSelected(data);
        }

        internal static bool ConsumeEscape(Transform scope)
        {
            // The EventSystem, the enclosing modal and the player can see one key on the same frame.
            if (dismissalFrame == Time.frameCount && dismissed != null && dismissed.transform.IsChildOf(scope)) return true;
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == null || !selected.transform.IsChildOf(scope) ||
                !selected.TryGetComponent<AlbumSearchInput>(out var field) || !field.IsInteractable()) return false;
            dismissed = field; dismissalFrame = Time.frameCount;
            field.text = "";
            field.DeactivateInputField();
            EventSystem.current.SetSelectedGameObject(field.RestoreFocus?.Invoke());
            return true;
        }
    }

    internal sealed class AlbumSearchLens : MaskableGraphic
    {
        static readonly MeshPen pen = new MeshPen(null, 128);

        protected override void OnPopulateMesh(VertexHelper geometry)
        {
            geometry.Clear();
            pen.Clear();
            pen.Ring(new Vector2(-1.5f, 1.5f), 5.2f, 1.7f, FieldMenuSkin.Muted, 24);
            pen.Line(new Vector2(2.4f, -2.4f), new Vector2(7.5f, -7.5f), 2.2f, FieldMenuSkin.Muted);
            var bounds = rectTransform.rect;
            pen.CopyTo(geometry, bounds.center, Mathf.Min(bounds.width, bounds.height) / 20);
        }
    }
}
