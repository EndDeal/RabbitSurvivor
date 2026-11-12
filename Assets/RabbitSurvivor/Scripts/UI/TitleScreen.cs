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
        [SerializeField] GameObject loadingPanel;
        [SerializeField] RectTransform loadingFill;
        [SerializeField] RectTransform loadingGlint;
        [SerializeField] Text loadingLabel;
        [SerializeField] Button startButton;

        AsyncOperation sceneLoad;
        bool ready;

        IEnumerator Start()
        {
            startButton.gameObject.SetActive(false);
            loadingPanel.SetActive(true);
            startButton.onClick.AddListener(EnterGame);
            yield return null;

            sceneLoad = SceneManager.LoadSceneAsync(gameScene);
            sceneLoad.allowSceneActivation = false;

            float elapsed = 0f;
            float displayedProgress = 0f;
            while (sceneLoad.progress < 0.9f || elapsed < 1.1f || displayedProgress < 0.999f)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(sceneLoad.progress / 0.9f);
                displayedProgress = Mathf.MoveTowards(displayedProgress, progress, Time.unscaledDeltaTime * 1.35f);
                loadingFill.anchorMax = new Vector2(displayedProgress, 1f);
                loadingGlint.anchoredPosition = new Vector2(Mathf.Repeat(elapsed * 250f, 400f) - 70f, 0f);
                loadingLabel.text = "불러오는 중" + new string('.', 1 + (int)(elapsed * 3f) % 3);
                yield return null;
            }

            ready = true;
            loadingPanel.SetActive(false);
            startButton.gameObject.SetActive(true);
            startButton.Select();
        }

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
