using UnityEngine;
using UnityEngine.UI;

namespace RabbitSurvivor
{
    public class Result : MonoBehaviour
    {
        public GameObject[] titles;

        public void Lose()
        {
            titles[0].SetActive(true);
            ShowMineRecord();
        }

        public void Win()
        {
            titles[1].SetActive(true);
            ShowMineRecord();
        }

        void ShowMineRecord()
        {
            GameManager game = GameManager.instance;
            if (!game.Crops || game.Crops.BestMineCombo < 3 || !game.HudFont)
                return;

            int best = game.Crops.BestMineCombo;
            var center = new Vector2(.5f, .5f);
            RectTransform card = RunHud.Place(transform, "Potato Record", center, center,
                new Vector2(0f, 8f), new Vector2(94f, 33f));
            Image panel = card.gameObject.AddComponent<Image>();
            panel.color = new Color32(26, 46, 58, 231);
            panel.raycastTarget = false;
            AddLabel(card, "Record", $"감자 연쇄 최고 {best}마리", new Vector2(0f, 7f), new Vector2(92f, 13f), 8, game.HudFont);

            RectTransform copy = RunHud.Place(card, "Copy Record", center, center, new Vector2(0f, -8f), new Vector2(50f, 11f));
            Image buttonImage = copy.gameObject.AddComponent<Image>();
            buttonImage.color = new Color32(48, 92, 97, 255);
            Button button = copy.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            Text label = AddLabel(copy, "Label", "기록 복사", Vector2.zero, copy.sizeDelta, 6, game.HudFont);
            button.onClick.AddListener(() => {
                GUIUtility.systemCopyBuffer = $"Rabbit Survivor | 감자 지뢰 하나로 {best}마리 연쇄 처치!";
                label.text = "복사 완료";
            });
        }

        static Text AddLabel(RectTransform parent, string name, string value, Vector2 offset, Vector2 size, int fontSize, Font font)
        {
            var center = new Vector2(.5f, .5f);
            Text label = RunHud.Place(parent, name, center, center, offset, size).gameObject.AddComponent<Text>();
            label.text = value;
            label.font = font;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color32(255, 224, 152, 255);
            label.raycastTarget = false;
            return label;
        }
    }
}
