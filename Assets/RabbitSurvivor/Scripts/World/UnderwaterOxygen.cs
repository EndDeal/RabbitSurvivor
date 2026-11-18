using UnityEngine;

namespace RabbitSurvivor
{
    public sealed class UnderwaterOxygen : MonoBehaviour
    {
        const float StationRadius = 1f;
        const float StationSpacing = 11f;
        const float StationHeightOffset = 4f;
        const float AlternateRowOffset = 2f;
        const float RockClearance = StationRadius + 1f;
        const int HorizontalPadding = 2;
        const int VerticalPadding = 1;
        const int GridColumns = HorizontalPadding * 2 + 1;
        const int GridRows = VerticalPadding * 2 + 1;

        [SerializeField, Min(1f)] float breathSeconds = 50f;
        [SerializeField, Min(0.1f)] float refillSeconds = 5f;
        [SerializeField, Min(0f)] float suffocationDamage = 8f;
        [SerializeField] Sprite anemone;
        [SerializeField] Material effectMaterial;

        readonly OxygenZoneVisual[] stations = new OxygenZoneVisual[GridColumns * GridRows];
        OxygenZoneVisual arena;
        Vector2 arenaCenter;
        float arenaRadius;
        Vector2Int? currentCell;
        Sprite stationSand;
        Sprite arenaSand;
        Sprite bubble;
        float animationTime;
        float suffocationTime;

        internal float Fraction { get; private set; }
        internal bool HasAir { get; private set; }

        internal void Begin(Vector2 playerPosition)
        {
            stationSand ??= OxygenZoneVisual.CreateSand(32);
            bubble ??= OxygenZoneVisual.CreateBubble();
            Fraction = 1f;
            suffocationTime = 0f;
            animationTime = 0f;
            HasAir = false;
            currentCell = null;
            UpdateStations(StationCell(playerPosition));
        }

        internal void Advance(Player player, float deltaTime)
        {
            animationTime += deltaTime;
            Vector2 position = player.transform.position;
            if (arenaRadius > 0f) {
                HasAir = (position - arenaCenter).sqrMagnitude <= arenaRadius * arenaRadius;
                arena.Animate(animationTime);
            } else {
                Vector2Int cell = StationCell(position);
                UpdateStations(cell);
                HasAir = (position - StationPosition(cell)).sqrMagnitude <= StationRadius * StationRadius;
                foreach (OxygenZoneVisual station in stations)
                    station.Animate(animationTime);
            }

            if (HasAir) {
                Fraction = Mathf.Min(1f, Fraction + deltaTime / refillSeconds);
                suffocationTime = 0f;
                return;
            }

            float remainingBreath = Fraction * breathSeconds;
            Fraction = Mathf.Max(0f, remainingBreath - deltaTime) / breathSeconds;
            suffocationTime += Mathf.Max(0f, deltaTime - remainingBreath);
            if (suffocationTime >= 1f) {
                suffocationTime -= 1f;
                player.TakeDamage(suffocationDamage, showHitBorder: false);
            }
        }

        internal void OpenArena(Vector2 center, float radius)
        {
            foreach (OxygenZoneVisual station in stations)
                station.SetActive(false);

            arenaSand ??= OxygenZoneVisual.CreateSand(128);
            arena ??= new OxygenZoneVisual(transform, "Dragon Arena Air", arenaSand, bubble, effectMaterial, null, true);
            arenaCenter = center;
            arenaRadius = radius;
            HasAir = true;
            arena.Place(center, radius);
            arena.Animate(animationTime);
        }

        internal void End()
        {
            HasAir = false;
            arenaRadius = 0f;
            arena?.SetActive(false);
            foreach (OxygenZoneVisual station in stations)
                station?.SetActive(false);
        }

        void OnDestroy()
        {
            DestroyRuntimeSprite(stationSand);
            DestroyRuntimeSprite(arenaSand);
            DestroyRuntimeSprite(bubble);
        }

        static void DestroyRuntimeSprite(Sprite sprite)
        {
            if (!sprite)
                return;

            Destroy(sprite.texture);
            Destroy(sprite);
        }

        void UpdateStations(Vector2Int center)
        {
            if (currentCell == center)
                return;

            currentCell = center;
            for (int y = center.y - VerticalPadding; y <= center.y + VerticalPadding; y++) {
                for (int x = center.x - HorizontalPadding; x <= center.x + HorizontalPadding; x++) {
                    int index = WrapGridIndex(y, GridRows) * GridColumns + WrapGridIndex(x, GridColumns);
                    var cell = new Vector2Int(x, y);
                    stations[index] ??= new OxygenZoneVisual(transform, "Oxygen Anemone", stationSand, bubble,
                        effectMaterial, anemone);
                    stations[index].Place(StationPosition(cell), StationRadius);
                }
            }
        }

        static int WrapGridIndex(int index, int size) => (index % size + size) % size;

        static float RowOffset(int row) => (row & 1) * AlternateRowOffset;

        static Vector2Int StationCell(Vector2 position)
        {
            int row = Mathf.RoundToInt((position.y - StationHeightOffset) / StationSpacing);
            return new Vector2Int(Mathf.RoundToInt((position.x - RowOffset(row)) / StationSpacing), row);
        }

        static Vector2 StationPosition(Vector2Int cell) =>
            new Vector2(cell.x * StationSpacing + RowOffset(cell.y), cell.y * StationSpacing + StationHeightOffset);

        internal static bool NeedsRockClearance(Vector2 position) =>
            (position - StationPosition(StationCell(position))).sqrMagnitude < RockClearance * RockClearance;
    }
}
