using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Scopes keyboard navigation to a sheet and restores the covered controls on close.</summary>
    [ExecuteAlways]
    public sealed class WorkshopModal : MonoBehaviour
    {
        readonly List<(CanvasGroup group, bool interactable)> covered = new List<(CanvasGroup, bool)>();
        GameObject previousFocus;
        Action close;
        bool active;

        public static void Open(GameObject sheet, Action close, GameObject previousFocus = null)
        {
            if (!sheet.TryGetComponent<WorkshopModal>(out var scope)) scope = sheet.AddComponent<WorkshopModal>();
            scope.Release();
            scope.active = true;
            scope.close = close;
            scope.previousFocus = previousFocus ?? EventSystem.current?.currentSelectedGameObject;
            foreach (Transform sibling in sheet.transform.parent)
            {
                if (sibling == sheet.transform || !sibling.gameObject.activeInHierarchy || sibling.name == "WorkshopTooltip") continue;
                if (!sibling.TryGetComponent<CanvasGroup>(out var group)) group = sibling.gameObject.AddComponent<CanvasGroup>();
                scope.covered.Add((group, group.interactable));
                group.interactable = false;
            }
            foreach (var control in sheet.GetComponentsInChildren<Selectable>())
                if (control.IsInteractable()) { EventSystem.current?.SetSelectedGameObject(control.gameObject); break; }
        }

        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame &&
                (selected == null || selected.transform.IsChildOf(transform))) close?.Invoke();
        }

        void OnDisable() => Release();
        void Release()
        {
            if (!active) return;
            active = false;
            foreach (var entry in covered) if (entry.group != null) entry.group.interactable = entry.interactable;
            covered.Clear();
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (EventSystem.current != null && (selected == null || selected.transform.IsChildOf(transform)))
            {
                GameObject restore = previousFocus != null && previousFocus.activeInHierarchy ? previousFocus : null;
                if (restore == null && transform.parent != null)
                    foreach (var control in transform.parent.GetComponentsInChildren<Selectable>())
                        if (!control.transform.IsChildOf(transform) && control.IsInteractable()) { restore = control.gameObject; break; }
                EventSystem.current.SetSelectedGameObject(restore);
            }
            previousFocus = null;
        }
    }
}
