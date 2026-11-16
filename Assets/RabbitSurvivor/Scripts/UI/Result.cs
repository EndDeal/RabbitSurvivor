using UnityEngine;
using UnityEngine.UI;

namespace RabbitSurvivor
{
    public class Result : MonoBehaviour
    {
        const string DefeatTitle = "DEAD";
        const string VictoryTitle = "SURVIVED!";

        public GameObject[] titles;
        [SerializeField] Font titleFont;

        void Awake()
        {
            if (!titleFont)
                return;

            StyleTitle(titles[0], DefeatTitle);
            StyleTitle(titles[1], VictoryTitle);
        }

        public void Lose()
        {
            titles[0].SetActive(true);
        }

        public void Win()
        {
            titles[1].SetActive(true);
        }

        void StyleTitle(GameObject title, string caption)
        {
            title.GetComponent<Image>().enabled = false;
            var frame = (RectTransform)title.transform;
            frame.localScale = Vector3.one;
            frame.sizeDelta = new Vector2(119f, 25f);

            AddTitleLayer(frame, caption, new Color32(234, 174, 74, 255), -1.65f, 1.35f);
            AddTitleLayer(frame, caption, new Color32(255, 239, 181, 255), .25f, 1f);
        }

        void AddTitleLayer(RectTransform frame, string caption, Color color, float offsetY, float outlineWidth)
        {
            var layer = new GameObject("Result title lettering", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = (RectTransform)layer.transform;
            rect.SetParent(frame, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = new Vector2(0f, offsetY);

            var text = layer.GetComponent<Text>();
            text.font = titleFont;
            text.fontSize = 23;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.raycastTarget = false;
            text.text = caption;

            var outline = layer.AddComponent<Outline>();
            outline.effectColor = new Color32(11, 48, 50, 255);
            outline.effectDistance = new Vector2(outlineWidth, -outlineWidth);
        }
    }
}
