using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Incoming opacity only; layout, focus and semantic input groups remain authoritative.</summary>
    [DisallowMultipleComponent]
    public sealed class UiMenuMotion : MonoBehaviour
    {
        const string BackdropName = "Field headquarters backdrop";
        static int suppressedOpenings;
        [SerializeField, HideInInspector] CanvasGroup opacityGroup;
        [SerializeField, HideInInspector] List<CanvasGroup> backdropGroups = new List<CanvasGroup>();
        readonly List<CanvasGroup> currentBackdrops = new List<CanvasGroup>();
        RectTransform scope;
        PlayerGameRoot player;
        float elapsed, duration, startingOpacity;
        bool screen;

        public float Opacity => opacityGroup != null ? opacityGroup.alpha : 1;
        public bool IsAnimating { get; private set; }
        public CanvasGroup OpacityGroup => opacityGroup;

        public static UiMenuMotion PlayScreen(RectTransform target, bool animate = true)
            => Play(target, true, animate);

        public static UiMenuMotion PlayWindow(GameObject modal, bool animate = true)
            => modal != null ? Play(modal.transform as RectTransform, false, animate) : null;

        static UiMenuMotion Play(RectTransform target, bool screen, bool animate)
        {
            if (target == null) return null;
            var motion = target.GetComponent<UiMenuMotion>() ?? target.gameObject.AddComponent<UiMenuMotion>();
            motion.Finish();
            motion.scope = target;
            motion.screen = screen;
            motion.player = target.GetComponentInParent<PlayerGameRoot>(true);
            if (motion.opacityGroup == null)
            {
                var visual = FindVisualTarget(target);
                if (visual != null) motion.opacityGroup = visual.gameObject.AddComponent<CanvasGroup>();
            }
            // Install backdrop ownership even for an instant first build. Later entrances can
            // then reuse it without mistaking an existing group for a semantic authority.
            motion.RefreshBackdrop();
            if (motion.opacityGroup == null || !animate || suppressedOpenings > 0 ||
                !UnityEngine.Application.isPlaying || !motion.isActiveAndEnabled ||
                motion.player != null && motion.player.ReducedMotion)
            {
                motion.Finish();
                return motion;
            }
            motion.elapsed = 0;
            motion.duration = screen ? .16f : .18f;
            motion.startingOpacity = screen ? .6f : .25f;
            motion.IsAnimating = true;
            motion.opacityGroup.alpha = motion.startingOpacity;
            motion.SetBackdropExclusion(true);
            return motion;
        }

        static RectTransform FindVisualTarget(RectTransform root)
        {
            if (root.GetComponent<CanvasGroup>() == null) return root;
            RectTransform fallback = null;
            foreach (var candidate in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (candidate == root || candidate.GetComponent<CanvasGroup>() != null || !HasVisibleGraphic(candidate)) continue;
                if (fallback == null) fallback = candidate;
                string name = candidate.name;
                if (name.IndexOf("paper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("sheet", StringComparison.OrdinalIgnoreCase) >= 0) return candidate;
            }
            return fallback;
        }

        static bool HasVisibleGraphic(RectTransform target)
        {
            foreach (var graphic in target.GetComponentsInChildren<Graphic>(true))
                if (graphic.enabled && graphic.color.a > 0) return true;
            return false;
        }

        void Update()
        {
            if (!IsAnimating) return;
            if (opacityGroup == null || player != null && player.ReducedMotion)
            {
                Finish();
                return;
            }
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float remaining = 1 - progress;
            opacityGroup.alpha = Mathf.Lerp(startingOpacity, 1, 1 - remaining * remaining * remaining);
            if (progress >= 1) Finish();
        }

        public void Finish()
        {
            IsAnimating = false;
            elapsed = 0;
            if (opacityGroup != null) opacityGroup.alpha = 1;
            SetBackdropExclusion(false);
        }

        /// <summary>Called by the theme after it replaces or reparents its noninteractive map.</summary>
        public void RefreshBackdrop()
        {
            SetBackdropExclusion(false);
            currentBackdrops.Clear();
            for (int index = backdropGroups.Count - 1; index >= 0; index--)
                if (backdropGroups[index] == null) backdropGroups.RemoveAt(index);
            if (!screen || scope == null) return;
            foreach (var image in scope.GetComponentsInChildren<RawImage>(true))
            {
                if (image.name != BackdropName || image.raycastTarget || !image.gameObject.activeSelf) continue;
                var group = image.GetComponent<CanvasGroup>();
                if (group == opacityGroup || group != null && !backdropGroups.Contains(group)) continue;
                if (group == null)
                {
                    group = image.gameObject.AddComponent<CanvasGroup>();
                    backdropGroups.Add(group);
                }
                if (!CanExcludeBackdrop(image, group)) continue;
                currentBackdrops.Add(group);
                group.ignoreParentGroups = IsAnimating;
            }
        }

        bool CanExcludeBackdrop(RawImage image, CanvasGroup backdropGroup)
        {
            if (opacityGroup == null || !image.transform.IsChildOf(opacityGroup.transform) || image.canvas == null) return false;
            for (Transform parent = image.transform; parent != null; parent = parent.parent)
            {
                var group = parent.GetComponent<CanvasGroup>();
                // Ignoring parents escapes ancestor groups, including semantic
                // visibility outside the entrance target. Exclude only a wholly owned path.
                if (group != null && group != opacityGroup && group != backdropGroup) return false;
            }
            return true;
        }

        void SetBackdropExclusion(bool value)
        {
            foreach (var group in value ? currentBackdrops : backdropGroups)
                if (group != null) group.ignoreParentGroups = value;
        }

        public static void FinishIn(Transform root)
        {
            if (root == null) return;
            foreach (var motion in root.GetComponentsInChildren<UiMenuMotion>(true)) motion.Finish();
        }

        public static IDisposable SuppressOpenings(bool suppress = true) => new OpeningSuppression(suppress);

        sealed class OpeningSuppression : IDisposable
        {
            bool active;
            public OpeningSuppression(bool suppress)
            {
                active = suppress;
                if (active) suppressedOpenings++;
            }
            public void Dispose()
            {
                if (!active) return;
                active = false;
                suppressedOpenings--;
            }
        }

        void OnDisable() => Finish();

        void OnDestroy()
        {
            Finish();
            Release(opacityGroup);
            foreach (var group in backdropGroups) Release(group);
            backdropGroups.Clear();
        }

        static void Release(CanvasGroup group)
        {
            if (group == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(group);
            else DestroyImmediate(group);
        }
    }
}
