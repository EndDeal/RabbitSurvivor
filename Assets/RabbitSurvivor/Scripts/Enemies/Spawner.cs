using System.Collections.Generic;
using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public enum BossType { None, Mid, Final }
    public enum EnemyBehavior { Chase, Barrier, Shooter }

    public class Spawner : MonoBehaviour
    {
        const int BarrierCount = 10;
        const float BarrierRadius = 2.7f;
        const float CleanupInterval = 0.5f;

        public Transform[] spawnPoint;
        public SpawnData[] spawnData;
        public float levelTime;

        [SerializeField] RuntimeAnimatorController[] enemyAnimations = System.Array.Empty<RuntimeAnimatorController>();
        [SerializeField] RuntimeAnimatorController midBossAnimation;
        [SerializeField] RuntimeAnimatorController finalBossAnimation;
        [SerializeField] SpawnData midBoss = new SpawnData { spriteType = 1, health = 145, speed = 1.3f, experience = 24 };
        [SerializeField] SpawnData finalBoss = new SpawnData { spriteType = 1, health = 525 };
        [SerializeField] Material dashGuideMaterial;
        [SerializeField, Min(0f)] float barrierStart = 90f;
        [SerializeField] SpawnData barrierWave;
        [SerializeField, Min(0f)] float shooterStart = 180f;
        [SerializeField] SpawnData shooterWave;
        [SerializeField, Min(12f)] float recycleDistance = 24f;
        [SerializeField, Min(BarrierCount + 2)] int maxWaveEnemies = 96;

        readonly List<Enemy> waveEnemies = new List<Enemy>();
        Camera sceneCamera;
        float nextCleanupAt;
        float timer;
        float nextWallAt;
        float nextShooterAt;

        public SpawnData CurrentWave => spawnData[Mathf.Clamp(
            Mathf.FloorToInt(GameManager.instance.gameTime / levelTime), 0, spawnData.Length - 1)];

        void Awake()
        {
            spawnPoint = GetComponentsInChildren<Transform>();
            levelTime = GameManager.instance.maxGameTime / spawnData.Length;
            sceneCamera = Camera.main;
        }

        void Update()
        {
            GameManager game = GameManager.instance;
            if (!game.isLive || game.HasFinalArena)
                return;

            if (Time.time >= nextCleanupAt)
                RecycleDistantEnemies(game.player.transform.position);

            timer += Time.deltaTime;
            SpawnData wave = CurrentWave;
            if (timer >= wave.spawnTime) {
                timer -= wave.spawnTime;
                if (waveEnemies.Count < maxWaveEnemies - BarrierCount - 1)
                    SpawnWaveEnemy(wave, RandomSpawnPoint());
            }

            if (barrierWave != null && barrierWave.spawnTime > 0f && game.gameTime >= barrierStart && Time.time >= nextWallAt) {
                nextWallAt = Time.time + barrierWave.spawnTime;
                if (waveEnemies.Count <= maxWaveEnemies - BarrierCount)
                    SpawnBarrierRing(game.player.transform.position);
            }

            if (shooterWave != null && shooterWave.spawnTime > 0f && game.gameTime >= shooterStart && Time.time >= nextShooterAt) {
                nextShooterAt = Time.time + shooterWave.spawnTime;
                if (waveEnemies.Count < maxWaveEnemies - BarrierCount)
                    SpawnWaveEnemy(shooterWave, RandomSpawnPoint());
            }
        }

        void SpawnBarrierRing(Vector2 center)
        {
            float startAngle = Random.Range(0f, 2f * Mathf.PI / BarrierCount);
            for (int index = 0; index < BarrierCount; index++) {
                float angle = startAngle + index * (2f * Mathf.PI / BarrierCount);
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * BarrierRadius;
                SpawnWaveEnemy(barrierWave, center + offset);
            }
        }

        void SpawnWaveEnemy(SpawnData data, Vector3 position)
        {
            Enemy enemy = Spawn(data, BossType.None, position);
            if (!waveEnemies.Contains(enemy))
                waveEnemies.Add(enemy);
        }

        void RecycleDistantEnemies(Vector2 playerPosition)
        {
            nextCleanupAt = Time.time + CleanupInterval;
            float limitSquared = recycleDistance * recycleDistance;
            for (int index = waveEnemies.Count - 1; index >= 0; index--) {
                Enemy enemy = waveEnemies[index];
                if (enemy && enemy.gameObject.activeSelf && !enemy.IsBoss) {
                    Vector3 position = enemy.transform.position;
                    if (((Vector2)position - playerPosition).sqrMagnitude <= limitSquared || IsNearCameraView(position))
                        continue;
                    enemy.gameObject.SetActive(false);
                }
                waveEnemies[index] = waveEnemies[waveEnemies.Count - 1];
                waveEnemies.RemoveAt(waveEnemies.Count - 1);
            }
        }

        bool IsNearCameraView(Vector3 position)
        {
            if (!sceneCamera)
                return false;

            Vector3 viewport = sceneCamera.WorldToViewportPoint(position);
            return viewport.z > 0f && viewport.x >= -0.03f && viewport.x <= 1.03f &&
                viewport.y >= -0.05f && viewport.y <= 1.05f;
        }

        public Enemy SpawnBoss(BossType type)
        {
            GameManager game = GameManager.instance;
            bool isFinal = type == BossType.Final;
            Vector3 position = isFinal ? DragonPosition(game.player) : RandomSpawnPoint();
            Enemy boss = Spawn(isFinal ? finalBoss : midBoss, type, position);
            boss.ConfigureBossAttacks(this, dashGuideMaterial);
            if (game.BossPresentation)
                game.BossPresentation.ShowArrival(boss);
            else
                game.uiLevelUp.Notify("보스 등장!");
            return boss;
        }

        internal Enemy SpawnDragonGuard(Vector2 position, Enemy dragon)
        {
            Enemy guard = Spawn(spawnData[spawnData.Length - 1], BossType.None, position);
            guard.Summoner = dragon;
            return guard;
        }

        static Vector3 DragonPosition(Player player)
        {
            Camera camera = Camera.main;
            float distance = camera && camera.orthographic ? Mathf.Min(camera.orthographicSize * 0.56f, 3.4f) : 3.4f;
            return player.transform.position + Vector3.up * distance;
        }

        Vector3 RandomSpawnPoint()
        {
            return spawnPoint[Random.Range(1, spawnPoint.Length)].position;
        }

        Enemy Spawn(SpawnData data, BossType bossType, Vector3 position)
        {
            GameObject enemy = GameManager.instance.pool.Get(0);
            enemy.transform.position = position;
            enemy.GetComponent<Rigidbody2D>().position = position;
            RuntimeAnimatorController controller = data.spriteType < enemyAnimations.Length ?
                enemyAnimations[data.spriteType] : null;
            if (bossType == BossType.Mid)
                controller = midBossAnimation;
            else if (bossType == BossType.Final)
                controller = finalBossAnimation ? finalBossAnimation : midBossAnimation;

            Enemy spawned = enemy.GetComponent<Enemy>();
            spawned.Init(data, bossType, controller);
            return spawned;
        }
    }

    [System.Serializable]
    public class SpawnData
    {
        public float spawnTime;
        public int spriteType;
        public int health;
        public float speed;
        public EnemyBehavior behavior;
        public Sprite attackPose;
        public GameObject projectile;
        [Min(1)] public int killsPerMagnet = 100;
        [Min(1)] public int experience = 1;
    }
}
