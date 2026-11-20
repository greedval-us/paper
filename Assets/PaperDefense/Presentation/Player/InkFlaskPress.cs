using UnityEngine;
using UnityEngine.EventSystems;
using System;

namespace PaperDefense.Presentation
{
    /// <summary>Claim the reward that was shown when the pointer went down. Keyboard submits use the visible offer.</summary>
    public sealed class InkFlaskPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        internal int OfferId;
        internal Guid OfferRun;
        Guid pressedRun;
        int pressedId, releasedFrame = -1;
        internal bool PointerReleased => releasedFrame == Time.frameCount;
        internal Guid ClaimRun => PointerReleased ? pressedRun : OfferRun;
        internal int ClaimId => PointerReleased ? pressedId : OfferId;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            pressedId = OfferId;
            pressedRun = OfferRun;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) releasedFrame = Time.frameCount;
        }
    }
}
