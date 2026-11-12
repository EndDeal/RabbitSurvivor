using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Goldmetal.UndeadSurvivor
{
    public class GameOverPresentation : MonoBehaviour
    {
        const float LoopSeconds = 1.25f;
        const int MaxCaptions = 3;

        [SerializeField] Sprite tombstoneSprite;
        [SerializeField] Material presentationMaterial;
        [SerializeField] Sprite[] regularLaughSprites;
        [SerializeField] Sprite captainLaughSprite;
        [SerializeField] Sprite finalLaughSprite;
        [SerializeField] Font font;

        readonly List<LaughingEnemy> visibleEnemies = new List<LaughingEnemy>();
        SpriteRenderer tombstone;
        Vector3 gravePosition;
        Camera sceneCamera;
        Result result;
        Canvas resultCanvas;
        AudioSource audioSource;
        AudioClip loopClip;
        float started;

        struct LaughingEnemy
        {
            public SpriteRenderer renderer;
            public Animator animator;
            public Sprite originalSprite;
            public Material originalMaterial;
            public Vector3 originalScale;
            public bool originalFlip;
            public bool animatorWasEnabled;
            public float height;
            public float phase;
            public Text caption;
        }

        void Awake()
        {
            enabled = false;
        }

        public void Begin(Vector3 deathPosition, Result resultScreen)
        {
            if (enabled)
                return;

            result = resultScreen;
            resultCanvas = result.GetComponentInParent<Canvas>(true);
            sceneCamera = Camera.main;
            started = Time.unscaledTime;

            if (tombstoneSprite) {
                tombstone = new GameObject("Bouncing Tombstone").AddComponent<SpriteRenderer>();
                tombstone.transform.SetParent(transform, false);
                tombstone.sprite = tombstoneSprite;
                if (presentationMaterial)
                    tombstone.sharedMaterial = presentationMaterial;
                tombstone.sortingOrder = 7;
                gravePosition = deathPosition;
                tombstone.transform.position = gravePosition;
            }

            if (sceneCamera)
                CollectVisibleEnemies(deathPosition);
            PlayLoop();
            enabled = true;
        }

        void LateUpdate()
        {
            if (!result || !result.gameObject.activeInHierarchy) {
                StopPresentation();
                return;
            }

            float elapsed = Time.unscaledTime - started;
            float phase = Mathf.Repeat(elapsed, LoopSeconds);
            if (tombstone) {
                float lift = Hop(phase, 0f, 0.23f) * 0.32f + Hop(phase, 0.23f, 0.24f) * 0.21f;
                tombstone.transform.position = gravePosition + Vector3.up * lift;
                tombstone.transform.localRotation = Quaternion.Euler(0f, 0f, lift * Mathf.Sin(phase * 14f) * 20f);
                tombstone.transform.localScale = new Vector3(1f - lift * 0.23f, 1f + lift * 0.38f, 1f);
            }

            foreach (LaughingEnemy enemy in visibleEnemies) {
                if (!enemy.renderer || !enemy.renderer.gameObject.activeInHierarchy)
                    continue;
                float laugh = Mathf.Repeat(elapsed + enemy.phase, LoopSeconds);
                bool flipped = laugh >= 0.31f && laugh < 0.87f && Mathf.FloorToInt((laugh - 0.31f) / 0.14f) % 2 == 0;
                enemy.renderer.flipX = enemy.originalFlip ^ flipped;
                if (enemy.caption) {
                    float pulse = Hop(laugh, 0.31f, 0.14f) + Hop(laugh, 0.46f, 0.14f) + Hop(laugh, 0.61f, 0.17f);
                    UpdateCaption(enemy, laugh, pulse);
                }
            }
        }

        static float Hop(float time, float start, float duration)
        {
            return Mathf.Sin(Mathf.PI * Mathf.Clamp01((time - start) / duration));
        }

        void CollectVisibleEnemies(Vector3 deathPosition)
        {
            Plane[] view = GeometryUtility.CalculateFrustumPlanes(sceneCamera);
            Enemy[] enemies = FindObjectsOfType<Enemy>();
            System.Array.Sort(enemies, (first, second) =>
                (first.transform.position - deathPosition).sqrMagnitude.CompareTo(
                    (second.transform.position - deathPosition).sqrMagnitude));

            foreach (Enemy enemy in enemies) {
                SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();
                if (!enemy.IsAlive || !renderer.enabled || !GeometryUtility.TestPlanesAABB(view, renderer.bounds))
                    continue;

                Sprite laugh = LaughSprite(enemy);
                if (!laugh || !renderer.sprite)
                    continue;

                Animator animator = enemy.GetComponent<Animator>();
                visibleEnemies.Add(new LaughingEnemy {
                    renderer = renderer,
                    animator = animator,
                    originalSprite = renderer.sprite,
                    originalMaterial = renderer.sharedMaterial,
                    originalScale = enemy.transform.localScale,
                    originalFlip = renderer.flipX,
                    animatorWasEnabled = animator.enabled,
                    height = renderer.bounds.size.y,
                    phase = (visibleEnemies.Count % 5) * 0.045f,
                    caption = visibleEnemies.Count < MaxCaptions && font && resultCanvas ? CreateCaption() : null
                });
                animator.enabled = false;
                enemy.transform.localScale *= renderer.sprite.bounds.size.y / laugh.bounds.size.y;
                renderer.sprite = laugh;
                if (presentationMaterial)
                    renderer.sharedMaterial = presentationMaterial;
            }
        }

        Sprite LaughSprite(Enemy enemy)
        {
            if (enemy.Boss == BossType.Mid)
                return captainLaughSprite;
            if (enemy.Boss == BossType.Final)
                return finalLaughSprite;
            if (enemy.Boss == BossType.None && enemy.SpriteType < regularLaughSprites.Length)
                return regularLaughSprites[enemy.SpriteType];
            return null;
        }

        Text CreateCaption()
        {
            var caption = new GameObject("Enemy Laugh", typeof(RectTransform), typeof(Text), typeof(Outline));
            var rect = (RectTransform)caption.transform;
            rect.SetParent(resultCanvas.transform, false);
            rect.SetSiblingIndex(result.transform.GetSiblingIndex());
            rect.sizeDelta = new Vector2(38f, 10f);

            Text text = caption.GetComponent<Text>();
            text.font = font;
            text.fontSize = 7;
            text.text = "꺄르르!";
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            caption.GetComponent<Outline>().effectColor = new Color32(38, 29, 49, 255);
            return text;
        }

        void UpdateCaption(LaughingEnemy enemy, float laugh, float pulse)
        {
            Color color = new Color32(255, 231, 148, 255);
            color.a = Mathf.Clamp01(pulse * 1.5f);
            enemy.caption.color = color;
            Vector3 above = enemy.renderer.transform.position + Vector3.up * (enemy.height * 0.65f + 0.3f + laugh * 0.12f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)resultCanvas.transform,
                sceneCamera.WorldToScreenPoint(above), null, out Vector2 position);
            enemy.caption.rectTransform.anchoredPosition = position;
        }

        void PlayLoop()
        {
            const int sampleRate = 22050;
            int length = Mathf.RoundToInt(sampleRate * LoopSeconds);
            float[] samples = new float[length];
            for (int index = 0; index < length; index++) {
                float time = index / (float)sampleRate;
                samples[index] = Tone(time, 0f, 0.21f, 265f, -80f, 0.65f) +
                    Tone(time, 0.23f, 0.17f, 325f, -100f, 0.46f) +
                    Tone(time, 0.34f, 0.10f, 525f, 110f, 0.23f) +
                    Tone(time, 0.46f, 0.10f, 610f, 85f, 0.19f) +
                    Tone(time, 0.58f, 0.12f, 530f, -70f, 0.17f);
            }

            loopClip = AudioClip.Create("Soft Boing and Chuckle", length, 1, sampleRate, false);
            loopClip.SetData(samples, 0);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.16f;
            audioSource.loop = true;
            audioSource.clip = loopClip;
            audioSource.Play();
        }

        static float Tone(float time, float start, float duration, float pitch, float bend, float volume)
        {
            float elapsed = time - start;
            if (elapsed < 0f || elapsed >= duration)
                return 0f;

            float fade = Mathf.Min(1f, elapsed * 120f) * Mathf.Pow(1f - elapsed / duration, 2f);
            float angle = 2f * Mathf.PI * (pitch * elapsed + bend * elapsed * elapsed / (2f * duration));
            return Mathf.Sin(angle) * fade * volume;
        }

        void StopPresentation()
        {
            if (audioSource)
                audioSource.Stop();
            if (tombstone)
                Destroy(tombstone.gameObject);

            foreach (LaughingEnemy enemy in visibleEnemies) {
                if (enemy.renderer) {
                    enemy.renderer.sprite = enemy.originalSprite;
                    enemy.renderer.sharedMaterial = enemy.originalMaterial;
                    enemy.renderer.flipX = enemy.originalFlip;
                    enemy.renderer.transform.localScale = enemy.originalScale;
                }
                if (enemy.animator)
                    enemy.animator.enabled = enemy.animatorWasEnabled;
                if (enemy.caption)
                    Destroy(enemy.caption.gameObject);
            }
            visibleEnemies.Clear();
            enabled = false;
        }

        void OnDestroy()
        {
            if (loopClip)
                Destroy(loopClip);
        }
    }
}
