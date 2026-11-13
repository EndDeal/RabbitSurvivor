using System;
using System.Collections;
using Goldmetal.UndeadSurvivor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RabbitSurvivor
{
    public sealed class StorySequence : MonoBehaviour
    {
        [Serializable]
        struct Cut
        {
            public Texture2D artwork;
            [TextArea] public string dialogue;
            [Range(0.1f, 0.9f)] public float bubblePointer;
            public Vector4 bubbleBounds;
            public bool tailOnTop;
            [TextArea] public string reply;
            public Vector4 replyBounds;
        }

        [SerializeField] GameManager gameManager;
        [SerializeField] GameObject characterSelection;
        [SerializeField] GameObject gameplayHud;
        [SerializeField] Font selectionFont;
        [SerializeField] Texture2D selectionArtwork;
        [SerializeField] Sprite[] stagePreviews;
        [SerializeField] Sprite[] portraits;
        [SerializeField] GameObject storyPanel;
        [SerializeField] Button skipButton;
        [SerializeField] RawImage illustration;
        [SerializeField] CanvasGroup frame;
        [SerializeField] CanvasGroup bubble;
        [SerializeField] RectTransform bubbleTail;
        [SerializeField] Text dialogue;
        [SerializeField] Cut[] cuts;
        [SerializeField, Min(0.65f)] float secondsPerCut = 2.7f;

        Coroutine playback;
        RawImage previousPage;
        Image paperEdge;
        CanvasGroup replyBubble;
        Text replyText;
        RectTransform replyTail;
        Rect pageUv;
        int selectedCharacter;
        bool startedGame;

        void Awake()
        {
            storyPanel.SetActive(false);
            skipButton.onClick.AddListener(Skip);
            var skipOutline = skipButton.gameObject.AddComponent<Outline>();
            skipOutline.effectColor = new Color32(244, 201, 115, 230);
            skipOutline.effectDistance = new Vector2(1.5f, 1.5f);
            replyBubble = Instantiate(bubble, bubble.transform.parent);
            replyBubble.name = "Reply Bubble";
            replyText = replyBubble.transform.GetChild(dialogue.transform.GetSiblingIndex()).GetComponent<Text>();
            replyTail = (RectTransform)replyBubble.transform.GetChild(bubbleTail.GetSiblingIndex());
            replyBubble.gameObject.SetActive(false);
            CreatePageEdge();
            BuildSelection();
        }

        void BuildSelection()
        {
            foreach (Transform oldCard in characterSelection.transform)
                oldCard.gameObject.SetActive(false);

            var root = (RectTransform)characterSelection.transform;
            root.offsetMin = root.offsetMax = Vector2.zero;
            characterSelection.AddComponent<RunSelection>().Initialize(portraits, selectionArtwork, stagePreviews, selectionFont, Play);
        }

        public void Play(int characterId)
        {
            if (playback != null || startedGame)
                return;

            selectedCharacter = characterId;
            characterSelection.SetActive(false);
            storyPanel.SetActive(true);
            skipButton.Select();
            playback = StartCoroutine(PlayCuts());
        }

        IEnumerator PlayCuts()
        {
            const float turnDuration = 0.35f;
            const float bubbleDelay = 0.30f;
            const float replyDelay = 0.24f;
            const float bounceDuration = 0.36f;
            for (int index = 0; index < cuts.Length; index++)
            {
                Cut cut = cuts[index];
                if (index > 0) {
                    previousPage.texture = illustration.texture;
                    pageUv = illustration.uvRect;
                }
                illustration.texture = cut.artwork;
                FitArtwork();
                SetBubble(bubble, dialogue, bubbleTail, cut.dialogue, cut.bubbleBounds, cut.bubblePointer, cut.tailOnTop);
                bool hasReply = !string.IsNullOrEmpty(cut.reply);
                replyBubble.gameObject.SetActive(hasReply);
                if (hasReply)
                    SetBubble(replyBubble, replyText, replyTail, cut.reply, cut.replyBounds, 0.76f, true);

                previousPage.gameObject.SetActive(index > 0);
                SetPageWidth(1f);

                float animationDuration = bubbleDelay + bounceDuration + (hasReply ? replyDelay : 0f);
                float elapsed = 0f;
                for (; elapsed < animationDuration; elapsed += Time.unscaledDeltaTime) {
                    float turn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / turnDuration));
                    frame.alpha = index == 0 ? Mathf.Clamp01(elapsed / 0.20f) : 1f;
                    if (previousPage.gameObject.activeSelf) {
                        if (turn < 1f)
                            SetPageWidth(1f - turn);
                        else {
                            previousPage.gameObject.SetActive(false);
                            previousPage.texture = null;
                        }
                    }
                    AnimateBubble(bubble, elapsed - bubbleDelay);
                    if (hasReply)
                        AnimateBubble(replyBubble, elapsed - bubbleDelay - replyDelay);
                    yield return null;
                }
                frame.alpha = 1f;
                previousPage.gameObject.SetActive(false);
                previousPage.texture = null;
                AnimateBubble(bubble, bounceDuration);
                if (hasReply)
                    AnimateBubble(replyBubble, bounceDuration);
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, secondsPerCut - elapsed));
            }

            playback = null;
            StartGameplay();
        }

        void FitArtwork()
        {
            Rect area = illustration.rectTransform.rect;
            if (!illustration.texture || area.height <= 0f || area.width <= 0f)
                return;

            float imageAspect = (float)illustration.texture.width / illustration.texture.height;
            float screenAspect = area.width / area.height;
            float width = Mathf.Min(1f, screenAspect / imageAspect);
            float height = Mathf.Min(1f, imageAspect / screenAspect);
            illustration.uvRect = new Rect((1f - width) * 0.5f, (1f - height) * 0.5f, width, height);
        }

        void LateUpdate()
        {
            if (storyPanel.activeSelf)
                FitArtwork();
        }

        static void SetBubble(CanvasGroup group, Text text, RectTransform tail, string line, Vector4 bounds,
            float pointer, bool tailOnTop)
        {
            RectTransform placement = (RectTransform)group.transform;
            placement.anchorMin = new Vector2(bounds.x, bounds.y);
            placement.anchorMax = placement.anchorMin + new Vector2(bounds.z, bounds.w);
            placement.offsetMin = placement.offsetMax = Vector2.zero;
            tail.anchorMin = tail.anchorMax = new Vector2(pointer, tailOnTop ? 1f : 0f);
            tail.localScale = new Vector3(1f, tailOnTop ? -1f : 1f, 1f);
            tail.anchoredPosition = new Vector2(0f, tailOnTop ? -3f : 3f);
            text.text = line;
            AnimateBubble(group, -1f);
        }

        void CreatePageEdge()
        {
            previousPage = new GameObject("Turning Page", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            previousPage.rectTransform.SetParent(illustration.rectTransform, false);
            previousPage.rectTransform.anchorMin = Vector2.zero;
            previousPage.rectTransform.offsetMin = previousPage.rectTransform.offsetMax = Vector2.zero;
            previousPage.raycastTarget = false;

            paperEdge = new GameObject("Paper Edge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            RectTransform edge = paperEdge.rectTransform;
            edge.SetParent(previousPage.rectTransform, false);
            edge.anchorMin = Vector2.right;
            edge.anchorMax = Vector2.one;
            edge.pivot = new Vector2(1f, 0.5f);
            edge.anchoredPosition = Vector2.zero;
            paperEdge.raycastTarget = false;
            previousPage.gameObject.SetActive(false);
        }

        void SetPageWidth(float remaining)
        {
            previousPage.rectTransform.anchorMax = new Vector2(remaining, 1f);
            previousPage.uvRect = new Rect(pageUv.x, pageUv.y, pageUv.width * remaining, pageUv.height);
            float crease = Mathf.Sin(Mathf.PI * remaining);
            paperEdge.rectTransform.sizeDelta = new Vector2(12f * crease, 0f);
            paperEdge.color = new Color(1f, 0.94f, 0.74f, 0.83f * crease);
        }

        static void AnimateBubble(CanvasGroup group, float age)
        {
            float pop = Mathf.Clamp01(age / 0.19f);
            float stretch = Mathf.Sin(pop * Mathf.PI) * 0.29f;
            float bounce = Mathf.Sin(Mathf.PI * Mathf.Clamp01((age - 0.20f) / 0.16f)) * 0.055f;
            RectTransform placement = (RectTransform)group.transform;
            group.alpha = Mathf.Clamp01(age / 0.09f);
            float size = Mathf.Lerp(0.76f, 1f, pop) + stretch + bounce;
            placement.localScale = new Vector3(size, size, 1f);
            placement.anchoredPosition = Vector2.up * (Mathf.Lerp(-16f, 0f, pop) + stretch * 27f + bounce * 36f);
        }

        void Skip()
        {
            if (playback == null)
                return;

            StopCoroutine(playback);
            playback = null;
            StartGameplay();
        }

        void StartGameplay()
        {
            startedGame = true;
            EventSystem.current?.SetSelectedGameObject(null);
            previousPage.gameObject.SetActive(false);
            previousPage.texture = null;
            storyPanel.SetActive(false);
            gameplayHud.SetActive(true);
            gameManager.GameStart(selectedCharacter);
        }
    }
}
