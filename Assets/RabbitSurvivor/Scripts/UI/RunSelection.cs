using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RabbitSurvivor
{
    public sealed class RunSelection : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        static readonly string[] RabbitNames = { "쌀 토끼", "밀 토끼", "감자 토끼", "당근 토끼" };
        static readonly string[] RabbitDetails = { "이동 속도 +10%", "공격 간격 -10%", "무기 피해 +20%", "발사체 +1" };
        // Opaque bounds in the 627-pixel portrait source images, measured from the bottom left.
        static readonly RectInt[] RabbitPortraitBounds = {
            new RectInt(129, 20, 403, 590),
            new RectInt(109, 24, 386, 586),
            new RectInt(146, 16, 369, 598),
            new RectInt(119, 16, 365, 596),
        };
        static readonly Color32 Ink = new Color32(13, 37, 52, 240);
        static readonly Color32 Cream = new Color32(255, 244, 219, 255);
        static readonly Color32 Gold = new Color32(255, 207, 112, 255);

        Sprite[] portraits;
        Sprite[] stages;
        Action<int> play;
        Func<int, ItemData> startingWeapon;
        Font font;
        Text heading;
        Text entryName;
        Text detail;
        Text indexLabel;
        Text confirmLabel;
        Button confirm;
        CanvasGroup selectionControls;
        GameObject quitDialog;
        Button quitCancel;
        GameObject previousSelection;
        Image portrait;
        RectTransform portraitArea;
        Image stagePreview;
        RawImage background;
        Texture characterArtwork;
        Outline cardFrame;
        bool selectingStage;
        int characterIndex;
        int stageIndex = StageCatalog.IndexOf(StageCatalog.ShallowSeaId);

        internal void Initialize(Sprite[] sprites, Texture artwork, Sprite[] stagePreviews, Font uiFont,
            Func<int, ItemData> getStartingWeapon, Action<int> launch)
        {
            portraits = sprites;
            stages = stagePreviews;
            characterArtwork = artwork;
            font = uiFont;
            startingWeapon = getStartingWeapon;
            play = launch;
            Image input = gameObject.AddComponent<Image>();
            input.color = Ink;
            var root = Area(transform, "Selection Content", 0f, 0f, 1f, 1f);
            selectionControls = root.gameObject.AddComponent<CanvasGroup>();

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
            portraitArea = Area(card, "Portrait Area", .09f, .35f, .91f, .95f);
            portrait = Area(portraitArea, "Rabbit Portrait", .5f, 0f, .5f, 0f).gameObject.AddComponent<Image>();
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
            AddButton(Area(root, "Selection Back", .04f, .92f, .24f, .975f), "뒤로", Back);
            BuildQuitDialog();
            Refresh();
        }

        void LateUpdate()
        {
            bool backPressed = Keyboard.current?.escapeKey.wasPressedThisFrame == true;
#if UNITY_ANDROID && ENABLE_LEGACY_INPUT_MANAGER
            backPressed |= Input.GetKeyDown(KeyCode.Escape);
#endif
            if (backPressed)
                Back();

            if (!selectingStage)
                AlignPortrait();

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

        void AlignPortrait()
        {
            RectInt bounds = RabbitPortraitBounds[characterIndex];
            Rect source = portrait.sprite.rect;
            Vector2 available = portraitArea.rect.size;
            float scale = Mathf.Min(available.x / bounds.width, available.y / bounds.height);
            portrait.rectTransform.sizeDelta = source.size * scale;
            portrait.rectTransform.anchoredPosition = new Vector2(
                source.width * .5f - bounds.center.x, source.height * .5f - bounds.yMin) * scale;
        }

        void Refresh()
        {
            heading.text = selectingStage ? "스테이지 선택" : "캐릭터 선택";
            int index = selectingStage ? stageIndex : characterIndex;
            indexLabel.text = $"{index + 1} / {(selectingStage ? StageCatalog.Count : RabbitNames.Length)}";
            bool unlocked = selectingStage ? StageCatalog.At(stageIndex).IsPlayable : AchiveManager.IsCharacterUnlocked(characterIndex);
            if (selectingStage) {
                StageDefinition stage = StageCatalog.At(index);
                entryName.text = stage.Name;
                detail.text = unlocked ? "햇빛이 비치는 해저" : "Coming Soon";
                portrait.enabled = false;
                stagePreview.enabled = true;
                stagePreview.sprite = stages[stage.PreviewIndex];
                background.texture = stages[stage.PreviewIndex].texture;
                background.color = new Color(.53f, .67f, .71f);
                cardFrame.effectColor = unlocked ? Gold : new Color32(123, 189, 190, 210);
            }
            else {
                entryName.text = RabbitNames[index];
                string description = unlocked ? RabbitDetails[index] : AchiveManager.CharacterRequirement(index);
                detail.text = $"시작 무기 · {startingWeapon(index).itemName}\n{description}";
                portrait.sprite = portraits[index];
                AlignPortrait();
                portrait.enabled = true;
                stagePreview.enabled = false;
                portrait.color = unlocked ? Color.white : new Color(.51f, .63f, .65f, .9f);
                background.texture = characterArtwork;
                background.color = Color.white;
                cardFrame.effectColor = Gold;
            }
            confirm.interactable = unlocked;
            confirmLabel.text = unlocked ? (selectingStage ? "입장" : "선택") : "잠김";
        }

        void Move(int step)
        {
            if (selectingStage)
                stageIndex = (stageIndex + step + StageCatalog.Count) % StageCatalog.Count;
            else
                characterIndex = (characterIndex + step + RabbitNames.Length) % RabbitNames.Length;
            Refresh();
        }

        void Confirm()
        {
            if (selectingStage) {
                if (GameManager.instance.TrySelectStage(StageCatalog.At(stageIndex).Id))
                    play(characterIndex);
                return;
            }
            if (!AchiveManager.IsCharacterUnlocked(characterIndex))
                return;
            selectingStage = true;
            stageIndex = StageCatalog.IndexOf(StageCatalog.ShallowSeaId);
            Refresh();
        }

        void Back()
        {
            if (quitDialog.activeSelf) {
                SetQuitDialog(false);
            }
            else if (selectingStage) {
                selectingStage = false;
                Refresh();
            }
            else {
                SetQuitDialog(true);
            }
        }

        void BuildQuitDialog()
        {
            var overlay = Area(transform, "Quit Confirmation", 0f, 0f, 1f, 1f);
            overlay.gameObject.AddComponent<Image>().color = new Color32(5, 17, 25, 205);
            quitDialog = overlay.gameObject;

            var card = Area(overlay, "Quit Card", .12f, .40f, .88f, .60f);
            card.gameObject.AddComponent<Image>().color = new Color32(19, 46, 60, 255);
            var frame = card.gameObject.AddComponent<Outline>();
            frame.effectColor = Gold;
            frame.effectDistance = new Vector2(1f, -1f);
            Label(Area(card, "Prompt", .06f, .52f, .94f, .89f), "게임을 종료하시겠습니까?", 12, Cream);
            quitCancel = AddButton(Area(card, "Cancel Quit", .08f, .12f, .46f, .43f), "취소", () => SetQuitDialog(false));
            Button quit = AddButton(Area(card, "Confirm Quit", .54f, .12f, .92f, .43f), "종료", () => GameManager.instance.GameQuit());
            quitCancel.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = quit };
            quit.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = quitCancel };
            quitDialog.SetActive(false);
        }

        void SetQuitDialog(bool show)
        {
            if (show)
                previousSelection = EventSystem.current?.currentSelectedGameObject;
            selectionControls.interactable = !show;
            selectionControls.blocksRaycasts = !show;
            quitDialog.SetActive(show);
            if (show)
                quitCancel.Select();
            else
                EventSystem.current?.SetSelectedGameObject(previousSelection);
        }

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (quitDialog.activeSelf)
                return;
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
