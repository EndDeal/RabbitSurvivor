using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Goldmetal.UndeadSurvivor
{
    public sealed class RunSelection : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        static readonly string[] RabbitNames = { "쌀 토끼", "밀 토끼", "감자 토끼", "당근 토끼" };
        static readonly string[] RabbitDetails = { "이동 속도 +10%", "무기 공격 간격 -10%", "무기 피해 +20%", "투사체 +1" };
        static readonly string[] StageNames = { "얕은 바다", "바닷가", "농장", "심해" };
        const int PlayableStage = 0;
        static readonly Color32 Ink = new Color32(13, 37, 52, 240);
        static readonly Color32 Cream = new Color32(255, 244, 219, 255);
        static readonly Color32 Gold = new Color32(255, 207, 112, 255);

        Sprite[] portraits;
        Sprite[] stages;
        Action<int> play;
        Font font;
        Text heading;
        Text entryName;
        Text detail;
        Text indexLabel;
        Text confirmLabel;
        Button confirm;
        Button back;
        Image portrait;
        Image stagePreview;
        RawImage background;
        Texture characterArtwork;
        Outline cardFrame;
        bool selectingStage;
        int characterIndex;
        int stageIndex = PlayableStage;

        internal void Initialize(Sprite[] sprites, Texture artwork, Sprite[] stagePreviews, Font uiFont, Action<int> launch)
        {
            portraits = sprites;
            stages = stagePreviews;
            characterArtwork = artwork;
            font = uiFont;
            play = launch;
            var root = (RectTransform)transform;
            Image input = gameObject.AddComponent<Image>();
            input.color = Ink;

            background = Area(root, "Selection Artwork", 0f, 0f, 1f, 1f).gameObject.AddComponent<RawImage>();
            background.texture = artwork;
            background.raycastTarget = false;
            Image shade = Area(root, "Selection Shade", 0f, 0f, 1f, 1f).gameObject.AddComponent<Image>();
            shade.color = new Color32(12, 40, 57, 100);
            shade.raycastTarget = false;
            var card = Area(root, "Selection Card", .10f, .25f, .90f, .80f);
            Image cardImage = card.gameObject.AddComponent<Image>();
            cardImage.color = Ink;
            cardImage.raycastTarget = false;
            cardFrame = card.gameObject.AddComponent<Outline>();
            cardFrame.effectColor = new Color32(255, 207, 112, 185);
            cardFrame.effectDistance = new Vector2(2f, -2f);
            heading = Label(Area(root, "Selection Title", .07f, .83f, .93f, .92f), "", 12, Gold);
            portrait = Area(card, "Rabbit Portrait", .09f, .29f, .91f, .98f).gameObject.AddComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            stagePreview = Area(card, "Stage Preview", .045f, .355f, .955f, .962f).gameObject.AddComponent<Image>();
            stagePreview.preserveAspect = true;
            stagePreview.raycastTarget = false;
            entryName = Label(Area(card, "Entry Name", .06f, .19f, .94f, .35f), "", 11, Cream);
            detail = Label(Area(card, "Entry Detail", .07f, .045f, .93f, .21f), "", 7, Cream);
            indexLabel = Label(Area(root, "Page Index", .28f, .195f, .72f, .245f), "", 7, Cream);
            AddButton(Area(root, "Previous", .11f, .192f, .24f, .25f), "<", () => Move(-1));
            AddButton(Area(root, "Next", .76f, .192f, .89f, .25f), ">", () => Move(1));
            confirm = AddButton(Area(root, "Confirm Selection", .23f, .099f, .77f, .174f), "", Confirm);
            confirmLabel = confirm.GetComponentInChildren<Text>();
            back = AddButton(Area(root, "Back to Characters", .04f, .92f, .24f, .975f), "뒤로", Back);
            Refresh();
        }

        void LateUpdate()
        {
            if (!background || !background.texture)
                return;
            Rect rect = background.rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            float imageAspect = (float)background.texture.width / background.texture.height;
            float aspect = rect.width / rect.height;
            float width = Mathf.Min(1f, aspect / imageAspect);
            float height = Mathf.Min(1f, imageAspect / aspect);
            background.uvRect = new Rect((1f - width) * .5f, (1f - height) * .5f, width, height);
        }

        void Refresh()
        {
            heading.text = selectingStage ? "스테이지 선택" : "캐릭터 선택";
            int index = selectingStage ? stageIndex : characterIndex;
            indexLabel.text = $"{index + 1} / 4";
            bool unlocked = selectingStage ? stageIndex == PlayableStage : AchiveManager.IsCharacterUnlocked(characterIndex);
            if (selectingStage) {
                entryName.text = StageNames[index];
                detail.text = unlocked ? "햇빛이 비치는 해저" : "Coming Soon";
                portrait.enabled = false;
                stagePreview.enabled = true;
                stagePreview.sprite = stages[index];
                background.texture = stages[index].texture;
                background.color = new Color(.53f, .67f, .71f);
                cardFrame.effectColor = unlocked ? Gold : new Color32(123, 189, 190, 210);
            }
            else {
                entryName.text = RabbitNames[index];
                detail.text = unlocked ? RabbitDetails[index] : AchiveManager.CharacterRequirement(index);
                portrait.sprite = portraits[index];
                portrait.enabled = true;
                stagePreview.enabled = false;
                portrait.color = unlocked ? Color.white : new Color(.51f, .63f, .65f, .9f);
                background.texture = characterArtwork;
                background.color = Color.white;
                cardFrame.effectColor = Gold;
            }
            back.gameObject.SetActive(selectingStage);
            confirm.interactable = unlocked;
            confirmLabel.text = unlocked ? (selectingStage ? "입장" : "선택") : "잠김";
        }

        void Move(int step)
        {
            if (selectingStage)
                stageIndex = (stageIndex + step + StageNames.Length) % StageNames.Length;
            else
                characterIndex = (characterIndex + step + RabbitNames.Length) % RabbitNames.Length;
            Refresh();
        }

        void Confirm()
        {
            if (selectingStage) {
                if (stageIndex == PlayableStage)
                    play(characterIndex);
                return;
            }
            if (!AchiveManager.IsCharacterUnlocked(characterIndex))
                return;
            selectingStage = true;
            stageIndex = PlayableStage;
            Refresh();
        }

        void Back()
        {
            selectingStage = false;
            Refresh();
        }

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData)
        {
            Vector2 swipe = eventData.position - eventData.pressPosition;
            if (Mathf.Abs(swipe.x) >= Mathf.Max(24f, Screen.width * .08f) && Mathf.Abs(swipe.x) > Mathf.Abs(swipe.y))
                Move(swipe.x < 0f ? 1 : -1);
        }

        static RectTransform Area(Transform parent, string name, float left, float bottom, float right, float top)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        Text Label(RectTransform rect, string value, int size, Color color)
        {
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = value;
            label.raycastTarget = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 4;
            label.resizeTextMaxSize = size;
            return label;
        }

        Button AddButton(RectTransform rect, string text, Action action)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Gold;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            Label(Area(rect, "Label", .04f, 0f, .96f, 1f), text, 9, Ink);
            return button;
        }
    }
}
