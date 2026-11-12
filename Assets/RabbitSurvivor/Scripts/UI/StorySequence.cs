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
            public string speaker;
            [TextArea] public string dialogue;
        }

        [SerializeField] GameManager gameManager;
        [SerializeField] GameObject characterSelection;
        [SerializeField] GameObject gameplayHud;
        [SerializeField] GameObject storyPanel;
        [SerializeField] Button skipButton;
        [SerializeField] RawImage illustration;
        [SerializeField] CanvasGroup frame;
        [SerializeField] Text speaker;
        [SerializeField] Text dialogue;
        [SerializeField] Cut[] cuts;
        [SerializeField, Min(0.5f)] float secondsPerCut = 2.4f;

        Coroutine playback;
        int selectedCharacter;
        bool startedGame;

        void Awake()
        {
            storyPanel.SetActive(false);
            skipButton.onClick.AddListener(Skip);
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
            RectTransform frameRect = (RectTransform)frame.transform;
            for (int index = 0; index < cuts.Length; index++)
            {
                Cut cut = cuts[index];
                illustration.texture = cut.artwork;
                speaker.text = cut.speaker;
                dialogue.text = cut.dialogue;

                float direction = index % 2 == 0 ? 1f : -1f;
                for (float elapsed = 0f; elapsed < 0.22f; elapsed += Time.unscaledDeltaTime)
                {
                    float fraction = elapsed / 0.22f;
                    float offset = Mathf.Lerp(24f * direction, 0f, 1f - (1f - fraction) * (1f - fraction));
                    frame.alpha = Mathf.Clamp01(fraction * 1.6f);
                    frameRect.anchoredPosition = new Vector2(offset, 0f);
                    yield return null;
                }

                frame.alpha = 1f;
                frameRect.anchoredPosition = Vector2.zero;
                yield return new WaitForSecondsRealtime(secondsPerCut - 0.22f);
            }

            playback = null;
            StartGameplay();
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
            storyPanel.SetActive(false);
            gameplayHud.SetActive(true);
            gameManager.GameStart(selectedCharacter);
        }
    }
}
