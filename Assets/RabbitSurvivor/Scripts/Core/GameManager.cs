using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Goldmetal.UndeadSurvivor
{
    public class GameManager : MonoBehaviour
    {
        const int ArenaBorderSegments = 96;
        const float ArenaPlayerInset = 0.15f;
        const float ArenaCleanupInterval = 0.25f;

        public static GameManager instance;

        [Header("# Game Control")]
        public bool isLive;
        public float gameTime;
        public float maxGameTime = 600f;
        [SerializeField, Min(0f)] float midBossTime = 300f;
        [SerializeField] Spawner encounterSpawner;
        [SerializeField, Min(4f)] float finalArenaRadius = 6.5f;
        [SerializeField] Material finalArenaBorderMaterial;
        [SerializeField] SeafloorRocks seafloorRocks;

        [Header("# Player Info")]
        public int playerId;
        public float health;
        public float maxHealth = 100;
        public int level;
        public int kill;
        public int exp;
        public int[] nextExp = {
            5, 8, 11, 15, 19, 23, 27, 31, 35, 39, 43, 47, 51, 55, 59,
            63, 67, 71, 75, 79, 83, 87, 91, 95, 99, 103, 107, 111, 115
        };

        [Header("# Game Object")]
        public PoolManager pool;
        public Player player;
        public LevelUp uiLevelUp;
        public Result uiResult;
        public Transform uiJoy;
        public GameObject enemyCleaner;
        public Sprite treasureChestSprite;
        [SerializeField] LootDrops lootDrops;
        [SerializeField] Material hitFlashMaterial;
        [SerializeField] BossPresentation bossPresentation;
        [SerializeField] GameOverPresentation gameOverPresentation;
        [SerializeField] RectTransform gameplayHud;
        [SerializeField] Font hudFont;
        [SerializeField, Min(0f)] float corpseLifetime = 2f;

        Enemy clockBoss;
        LineRenderer finalArenaBorder;
        Vector2 finalArenaCenter;
        float nextArenaCleanupAt;
        TreasureChest unopenedTreasure;
        bool midBossStarted;
        bool finalBossStarted;
        bool ending;
        float baseMaxHealth;

        public Material HitFlashMaterial => hitFlashMaterial;
        public BossPresentation BossPresentation => bossPresentation;
        public float CorpseLifetime => corpseLifetime;
        internal bool IsEnding => ending;
        internal bool HasFinalArena => finalArenaBorder;
        public bool IsBossClockHeld => clockBoss;
        internal bool HasWon { get; private set; }
        public int ExperienceToNextLevel => nextExp[Mathf.Min(level, nextExp.Length - 1)];

        void Awake()
        {
            instance = this;
            baseMaxHealth = maxHealth;
            Application.targetFrameRate = 60;
        }

        public void GameStart(int id)
        {
            playerId = id;
            health = maxHealth;
            player.gameObject.SetActive(true);
            uiLevelUp.Select(playerId % 2);
            if (gameplayHud && hudFont)
                gameplayHud.gameObject.AddComponent<RunHud>().Initialize(this, hudFont);
            Resume();

            AudioManager.instance.PlayBgm(true);
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Select);
        }

        public void GameOver()
        {
            if (!ending)
                StartCoroutine(GameOverRoutine());
        }

        IEnumerator GameOverRoutine()
        {
            BeginResult();
            bossPresentation?.Clear();
            Stop();

            Vector3 deathPosition = player.transform.position;
            yield return player.PlayDeathAnimation();

            player.gameObject.SetActive(false);
            gameOverPresentation?.Begin(deathPosition, uiResult);
            uiResult.gameObject.SetActive(true);
            uiResult.Lose();
            AudioManager.instance.PlayBgm(false);
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Lose);
        }

        IEnumerator VictoryRoutine(float delay)
        {
            HasWon = true;
            BeginResult();
            enemyCleaner.SetActive(true);

            yield return new WaitForSecondsRealtime(delay);
            bossPresentation?.Clear();
            uiResult.gameObject.SetActive(true);
            uiResult.Win();
            Stop();
            AudioManager.instance.PlayBgm(false);
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Win);
        }

        void BeginResult()
        {
            ending = true;
            isLive = false;
            clockBoss = null;
            ClearFinalArena();
            uiLevelUp.Dismiss();
            Time.timeScale = 1f;
        }

        public void GameRetry()
        {
            SceneManager.LoadScene("RabbitSurvivor");
        }

        public void GameQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Update()
        {
            if (!isLive)
                return;
            if (HasFinalArena) {
                if (Time.time >= nextArenaCleanupAt)
                    RecycleOutsideArena();
                return;
            }
            if (IsBossClockHeld)
                return;

            if (!encounterSpawner) {
                gameTime = Mathf.Min(gameTime + Time.deltaTime, maxGameTime);
                if (gameTime >= maxGameTime)
                    StartCoroutine(VictoryRoutine(0.5f));
                return;
            }

            float gate = midBossStarted ? maxGameTime : midBossTime;
            gameTime = Mathf.Min(gameTime + Time.deltaTime, gate);
            if (gameTime < gate || finalBossStarted)
                return;

            BossType type = midBossStarted ? BossType.Final : BossType.Mid;
            finalBossStarted = type == BossType.Final;
            midBossStarted = true;
            clockBoss = encounterSpawner.SpawnBoss(type);
            if (type == BossType.Final)
                OpenFinalArena(clockBoss.transform.position);
        }

        void OpenFinalArena(Vector3 center)
        {
            if (seafloorRocks)
                seafloorRocks.ClearArena(center, finalArenaRadius);
            finalArenaCenter = center;
            finalArenaBorder = new GameObject("Dragon Arena Border").AddComponent<LineRenderer>();
            finalArenaBorder.transform.SetParent(transform, false);
            finalArenaBorder.transform.position = center;
            finalArenaBorder.useWorldSpace = false;
            finalArenaBorder.loop = true;
            finalArenaBorder.sortingOrder = 1;
            finalArenaBorder.widthMultiplier = 0.16f;
            finalArenaBorder.startColor = new Color32(255, 226, 156, 220);
            finalArenaBorder.endColor = finalArenaBorder.startColor;
            if (finalArenaBorderMaterial)
                finalArenaBorder.sharedMaterial = finalArenaBorderMaterial;

            finalArenaBorder.positionCount = ArenaBorderSegments;
            for (int index = 0; index < ArenaBorderSegments; index++) {
                float angle = index * (2f * Mathf.PI / ArenaBorderSegments);
                finalArenaBorder.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * finalArenaRadius);
            }

            RecycleOutsideArena();
            if (lootDrops)
                lootDrops.RecycleOutsideFinalArena();

            if (unopenedTreasure && IsOutsideFinalArena(unopenedTreasure.transform.position)) {
                Transform chest = unopenedTreasure.transform;
                Vector2 inside = ConstrainPlayerPosition(chest.position);
                chest.position = new Vector3(inside.x, inside.y, chest.position.z);
            }
        }

        void RecycleOutsideArena()
        {
            pool.RecycleOutsideFinalArena(this);
            nextArenaCleanupAt = Time.time + ArenaCleanupInterval;
        }

        void ClearFinalArena()
        {
            if (!finalArenaBorder)
                return;

            finalArenaBorder.enabled = false;
            Destroy(finalArenaBorder.gameObject);
            finalArenaBorder = null;
        }

        internal Vector2 ConstrainPlayerPosition(Vector2 position)
        {
            if (!finalArenaBorder)
                return position;

            Vector2 offset = position - finalArenaCenter;
            float limit = finalArenaRadius - ArenaPlayerInset;
            return offset.sqrMagnitude <= limit * limit ? position :
                finalArenaCenter + offset.normalized * limit;
        }

        internal bool IsOutsideFinalArena(Vector2 position)
        {
            return HasFinalArena && (position - finalArenaCenter).sqrMagnitude > finalArenaRadius * finalArenaRadius;
        }

        public void RegisterKill(Enemy enemy)
        {
            if (ending || (!isLive && !uiLevelUp.IsOpen))
                return;

            kill++;
            if (enemy.IsBoss)
                bossPresentation?.ShowDefeat(enemy);

            if (enemy.Boss != BossType.Final) {
                Vector3 position = enemy.transform.position;
                if (lootDrops)
                    lootDrops.DropFromEnemy(position, enemy.IsBoss, enemy.ExperienceReward);
                else
                    GainExperience(enemy.ExperienceReward);

                if (enemy.Boss == BossType.Mid && treasureChestSprite)
                    unopenedTreasure = TreasureChest.Drop(position, treasureChestSprite);
            }

            if (clockBoss != enemy)
                return;

            clockBoss = null;
            if (enemy.Boss == BossType.Final)
                StartCoroutine(VictoryRoutine(bossPresentation ? bossPresentation.BannerSeconds : 0.5f));
        }

        public void GainExperience(int amount)
        {
            exp += amount;
            while (exp >= ExperienceToNextLevel) {
                exp -= ExperienceToNextLevel;
                level++;
                uiLevelUp.Show();
            }
        }

        public void SetMaxHealthBonus(float rate)
        {
            float newMax = baseMaxHealth * (1f + rate);
            health = Mathf.Min(health + newMax - maxHealth, newMax);
            maxHealth = newMax;
        }

        public void Stop()
        {
            isLive = false;
            Time.timeScale = 0f;
            uiJoy.localScale = Vector3.zero;
        }

        public void Resume()
        {
            if (ending)
                return;

            isLive = true;
            Time.timeScale = 1f;
            uiJoy.localScale = Vector3.one;
        }
    }
}
