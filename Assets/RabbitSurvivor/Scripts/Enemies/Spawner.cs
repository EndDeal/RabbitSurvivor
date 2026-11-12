using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public enum BossType { None, Mid, Final }
    public enum EnemyBehavior { Chase, Barrier, Shooter }

    public class Spawner : MonoBehaviour
    {
        const int WallCount = 4;
        const float WallSpacing = 0.72f;
        const float WallDistance = 3.4f;

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

        float timer;
        float nextWallAt;
        float nextShooterAt;

        public SpawnData CurrentWave => spawnData[Mathf.Clamp(
            Mathf.FloorToInt(GameManager.instance.gameTime / levelTime), 0, spawnData.Length - 1)];

        void Awake()
        {
            spawnPoint = GetComponentsInChildren<Transform>();
            levelTime = GameManager.instance.maxGameTime / spawnData.Length;
        }

        void Update()
        {
            GameManager game = GameManager.instance;
            if (!game.isLive)
                return;

            timer += Time.deltaTime;
            SpawnData wave = CurrentWave;
            if (timer >= wave.spawnTime) {
                timer -= wave.spawnTime;
                Spawn(wave, BossType.None, RandomSpawnPoint());
            }

            if (barrierWave != null && barrierWave.spawnTime > 0f && game.gameTime >= barrierStart && Time.time >= nextWallAt) {
                nextWallAt = Time.time + barrierWave.spawnTime;
                SpawnWall(game.player);
            }

            if (shooterWave != null && shooterWave.spawnTime > 0f && game.gameTime >= shooterStart && Time.time >= nextShooterAt) {
                nextShooterAt = Time.time + shooterWave.spawnTime;
                Spawn(shooterWave, BossType.None, RandomSpawnPoint());
            }
        }

        void SpawnWall(Player player)
        {
            Vector2 input = player.inputVec;
            if (input.sqrMagnitude < 0.01f)
                input = Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f) * Vector3.right;

            Vector2 forward = Mathf.Abs(input.x) >= Mathf.Abs(input.y) ?
                new Vector2(Mathf.Sign(input.x), 0f) : new Vector2(0f, Mathf.Sign(input.y));
            Vector2 center = (Vector2)player.transform.position + forward * WallDistance;
            Vector2 across = Vector2.Perpendicular(forward);
            for (int index = 0; index < WallCount; index++) {
                float offset = (index - (WallCount - 1) * 0.5f) * WallSpacing;
                Spawn(barrierWave, BossType.None, center + across * offset);
            }
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
