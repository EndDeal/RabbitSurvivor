using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RabbitSurvivor
{
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] string gameScene = "RabbitSurvivor";
        [SerializeField] RectTransform titleArtwork;
        [SerializeField] RawImage chaseArtwork;
        [SerializeField] GameObject loadingPanel;
        [SerializeField] RectTransform loadingFill;
        [SerializeField] RectTransform loadingGlint;
        [SerializeField] Text loadingLabel;
        [SerializeField] Button startButton;

        AsyncOperation sceneLoad;
        RectTransform titleArea;
        RectTransform portraitLayout;
        RectTransform viewport;
        RectTransform loadingTrack;
        float titleAspect;
        bool ready;

        void Awake()
        {
            titleArea = (RectTransform)titleArtwork.parent;
            portraitLayout = (RectTransform)chaseArtwork.transform.parent;
            viewport = (RectTransform)portraitLayout.parent;
            loadingTrack = (RectTransform)loadingFill.parent;
            RawImage logo = titleArtwork.GetComponent<RawImage>();
            titleAspect = (logo.texture.width * logo.uvRect.width) / (logo.texture.height * logo.uvRect.height);
        }

        IEnumerator Start()
        {
            startButton.gameObject.SetActive(false);
            loadingPanel.SetActive(true);
            startButton.onClick.AddListener(EnterGame);
            StartCoroutine(AnimateTitle());
            yield return null;

            sceneLoad = SceneManager.LoadSceneAsync(gameScene);
            sceneLoad.allowSceneActivation = false;

            float elapsed = 0f;
            float displayedProgress = 0f;
            while (sceneLoad.progress < 0.9f || elapsed < 1.1f || displayedProgress < 1f)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(sceneLoad.progress / 0.9f);
                displayedProgress = Mathf.MoveTowards(displayedProgress, progress, Time.unscaledDeltaTime * 1.35f);
                loadingFill.anchorMax = new Vector2(displayedProgress, 1f);
                float trackWidth = loadingTrack.rect.width;
                float shine = Mathf.Repeat(elapsed * 250f, trackWidth + 80f) - 54f;
                float left = Mathf.Clamp(shine, 0f, trackWidth * displayedProgress);
                float right = Mathf.Clamp(shine + 54f, left, trackWidth * displayedProgress);
                loadingGlint.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, left, right - left);
                loadingLabel.text = "불러오는 중" + new string('.', 1 + (int)(elapsed * 3f) % 3);
                yield return null;
            }

            loadingGlint.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.14f);
            ready = true;
            loadingPanel.SetActive(false);
            startButton.gameObject.SetActive(true);
            startButton.Select();
        }

        void LateUpdate()
        {
            Vector2 viewSize = viewport.rect.size;
            float portraitWidth = Mathf.Min(viewSize.x, viewSize.y * 9f / 16f);
            Vector2 inset = new Vector2(portraitWidth - viewSize.x, 0f);
            if (portraitLayout.sizeDelta != inset)
                portraitLayout.sizeDelta = inset;

            Rect area = chaseArtwork.rectTransform.rect;
            if (area.width > 0f && area.height > 0f) {
                float screenAspect = area.width / area.height;
                float imageAspect = (float)chaseArtwork.texture.width / chaseArtwork.texture.height;
                float width = Mathf.Min(1f, screenAspect / imageAspect);
                float height = Mathf.Min(1f, imageAspect / screenAspect);
                chaseArtwork.uvRect = new Rect((1f - width) * 0.5f, (1f - height) * 0.5f, width, height);
            }
            Vector2 bounds = Vector2.Scale(titleArea.rect.size, titleArtwork.anchorMax - titleArtwork.anchorMin);
            Vector2 fitted = new Vector2(Mathf.Min(bounds.x, bounds.y * titleAspect),
                Mathf.Min(bounds.y, bounds.x / titleAspect));
            Vector2 adjustment = fitted - bounds;
            if (titleArtwork.sizeDelta != adjustment)
                titleArtwork.sizeDelta = adjustment;
        }

        IEnumerator AnimateTitle()
        {
            // Start off-screen even while Unity is resolving the first canvas layout.
            titleArtwork.localScale = Vector3.zero;
            yield return null;
            Vector2 rest = titleArtwork.anchoredPosition;
            float height = Mathf.Max(titleArea.rect.height, 180f);
            const float fall = 0.46f;
            const float rebound = 0.34f;
            for (float elapsed = 0f; elapsed < fall; elapsed += MotionDelta()) {
                float t = elapsed / fall;
                titleArtwork.anchoredPosition = rest + Vector2.up * (height * 2.05f * (1f - t * t));
                titleArtwork.localScale = new Vector3(1f - t * 0.035f, 1f + t * 0.065f, 1f);
                yield return null;
            }

            for (float elapsed = 0f; elapsed < rebound; elapsed += MotionDelta()) {
                float lift = Mathf.Sin(Mathf.PI * Mathf.Clamp01((elapsed - 0.07f) / 0.26f)) * height * 0.085f;
                float squash = 0.12f * Mathf.Max(0f, 1f - elapsed / 0.12f);
                titleArtwork.anchoredPosition = rest + Vector2.up * lift;
                titleArtwork.localScale = new Vector3(1f + squash, 1f - squash, 1f);
                yield return null;
            }

            titleArtwork.anchoredPosition = rest;
            const float pulsePeriod = 1.65f;
            float pulseStart = Time.unscaledTime;
            while (true) {
                float phase = (Time.unscaledTime - pulseStart) * (2f * Mathf.PI / pulsePeriod);
                float pulse = Mathf.Sin(phase);
                titleArtwork.localScale = new Vector3(1f + pulse * 0.034f, 1f + pulse * 0.049f, 1f);
                yield return null;
            }
        }

        static float MotionDelta() => Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        void EnterGame()
        {
            if (!ready)
                return;

            ready = false;
            startButton.interactable = false;
            EventSystem.current?.SetSelectedGameObject(null);
            sceneLoad.allowSceneActivation = true;
        }
    }
}
