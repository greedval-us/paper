using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerBattleHud
    {
        RectTransform flaskReward;
        Text flaskCount, flaskAmount;
        readonly Text[] flaskStocks = new Text[3];
        readonly PlayerTooltip[] flaskHints = new PlayerTooltip[3];
        readonly PlayerIconGraphic[] flaskSymbols = new PlayerIconGraphic[3];
        readonly Button[] flaskChoices = new Button[3];
        readonly InkFlaskPress[] flaskPresses = new InkFlaskPress[3];
        readonly int[] shownInkStocks = { -1, -1, -1 };
        int shownFlaskYield = -1;
        Vector2 messageRestPosition;
        float lastFlaskClaim = float.NegativeInfinity;

        void BuildInkFlasks(RectTransform parent, PlayerLayout layout)
        {
            float x = layout.Field.x + 6, y = layout.Field.y + 4;
            const float width = 374, height = 76;
            flaskReward = ui.Panel(parent, "Ink flask reward", new Rect(x, y + 38, width, height), PlayerUi.Card, true);
            var title = ui.Label(flaskReward, "Выбери флакон за волну", new Rect(8, 3, 197, 27), 15, title: true);
            title.name = "Ink flask prompt";
            flaskAmount = ui.Label(flaskReward, "", new Rect(205, 3, 85, 26), 14, alignment: TextAnchor.MiddleCenter);
            flaskAmount.name = "Ink flask yield";
            flaskCount = ui.Label(flaskReward, "", new Rect(291, 3, 29, 26), 14, PlayerUi.Muted, alignment: TextAnchor.MiddleRight);
            flaskCount.name = "Ink flask count";
            ui.Label(flaskReward, "F1–F3", new Rect(width - 57, 3, 48, 26), 14, PlayerUi.Muted, alignment: TextAnchor.MiddleRight);
            if (albumMessageBadge != null) messageRestPosition = albumMessageBadge.anchoredPosition;

            for (int i = 0; i < flaskStocks.Length; i++)
            {
                int index = i;
                var color = (InkColor)(i + 1);
                var stock = ui.Panel(parent, "Ink stock " + color, new Rect(x + i * 68, y, 62, 32), PlayerUi.Card, true);
                var focus = stock.gameObject.AddComponent<Selectable>();
                focus.targetGraphic = stock.GetComponent<PlayerCardGraphic>();
                focus.transition = Selectable.Transition.None;
                var hint = flaskHints[i] = stock.gameObject.AddComponent<PlayerTooltip>();
                hint.Display = text => ui.Tooltip?.Invoke(text);
                flaskSymbols[i] = PlayerUi.Graphic<PlayerIconGraphic>(stock, "Ink drop", new Rect(5, 6, 18, 20));
                flaskSymbols[i].Icon = PlayerIcon.Drop;
                flaskSymbols[i].color = FlaskTone(color);
                flaskStocks[i] = ui.Label(stock, "", new Rect(26, 2, 34, 28), 14);
                flaskStocks[i].name = "Ink stock amount " + color;

                var choice = flaskChoices[i] = ui.IconButton(flaskReward, "Claim flask " + color,
                    new Rect(7 + i * 121, 32, 117, 36), PlayerIcon.Drop, "",
                    () => ClaimFlask(index, color), art: "object.well." + color.ToString().ToLowerInvariant());
                flaskPresses[i] = choice.gameObject.AddComponent<InkFlaskPress>();
                var bottle = choice.GetComponentInChildren<NotebookArtGraphic>();
                PlayerUi.Place(bottle.rectTransform, new Rect(1, 2, 31, 32));
                ui.Label(choice.transform, ColorName(color), new Rect(30, 2, 85, 32), 14,
                    alignment: TextAnchor.MiddleCenter);
            }
            flaskReward.gameObject.SetActive(false);
        }

        void RefreshInkFlasks(IRunReadModel session)
        {
            bool available = session.PendingInkFlasks > 0 && !game.Finished;
            flaskReward.gameObject.SetActive(available);
            if (albumMessageBadge != null)
                albumMessageBadge.anchoredPosition = messageRestPosition + (available ? Vector2.down * (flaskReward.rect.height + 6) : Vector2.zero);
            flaskCount.text = session.PendingInkFlasks > 1 ? "×" + session.PendingInkFlasks : "";
            if (shownFlaskYield != session.InkPerFlask) flaskAmount.text = "+" + session.InkPerFlask + " чернил";
            for (int i = 0; i < flaskStocks.Length; i++)
            {
                var color = (InkColor)(i + 1);
                int amount = session.ColoredInk(color);
                if (shownInkStocks[i] != amount || shownFlaskYield != session.InkPerFlask)
                {
                    shownInkStocks[i] = amount;
                    flaskStocks[i].text = amount.ToString();
                    flaskHints[i].Message = FlaskInkName(color) + " · запас " + amount +
                        "\nПосле волны выбери флакон: +" + session.InkPerFlask + " чернил одного цвета. Их можно потратить на цвет башни.";
                }
                if (shownFlaskYield != session.InkPerFlask)
                {
                    var role = color == InkColor.Red ? "периодический урон" : color == InkColor.Blue ? "замедление" : "ослабление защиты";
                    flaskChoices[i].GetComponent<PlayerTooltip>().Message = "F" + (i + 1) + " · " + FlaskInkName(color) + " · +" + session.InkPerFlask +
                        "\nЭффект башни — " + role + ". Потратить чернила можно через U.";
                }
                flaskSymbols[i].color = FlaskTone(color);
                flaskPresses[i].OfferRun = session.RunId;
                flaskPresses[i].OfferId = session.NextInkFlaskId;
                flaskChoices[i].interactable = available && game.CanBuild;
            }
            shownFlaskYield = session.InkPerFlask;
        }

        internal void HandleFlaskKeys()
        {
            if (flaskReward == null || !flaskReward.gameObject.activeInHierarchy) return;
            var keyboard = Keyboard.current;
            if (keyboard == null || !game.CanBuild) return;
            if (keyboard.f1Key.wasPressedThisFrame) ClaimFlask(0, InkColor.Red, true);
            else if (keyboard.f2Key.wasPressedThisFrame) ClaimFlask(1, InkColor.Blue, true);
            else if (keyboard.f3Key.wasPressedThisFrame) ClaimFlask(2, InkColor.Green, true);
        }

        void ClaimFlask(int index, InkColor color, bool hotkey = false)
        {
            if (!game.CanBuild || !flaskReward.gameObject.activeInHierarchy || Time.unscaledTime - lastFlaskClaim < .4f) return;
            var press = flaskPresses[index];
            if (!game.Run.ClaimInkFlask(hotkey ? press.OfferRun : press.ClaimRun, hotkey ? press.OfferId : press.ClaimId, color, out _)) return;
            lastFlaskClaim = Time.unscaledTime;
            if (!game.Drawing.IsPlacingTower)
                game.Drawing.SetMessage(FlaskInkName(color) + " +" + game.Run.Session.InkPerFlask);
            bool returnFocus = !press.PointerReleased && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == flaskChoices[index].gameObject
                && game.Run.Session.PendingInkFlasks == 0;
            Refresh();
            if (returnFocus) EventSystem.current.SetSelectedGameObject(flaskHints[index].gameObject);
        }

        static string FlaskInkName(InkColor color) => color == InkColor.Red ? "Красные чернила" : color == InkColor.Blue ? "Синие чернила" : "Зелёные чернила";
        static Color FlaskTone(InkColor color) => color == InkColor.Red ? NotebookPalette.Red : color == InkColor.Blue ? PlayerUi.Blue : NotebookPalette.Green;
    }
}
