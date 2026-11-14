using UnityEngine;
using UnityEngine.Tilemaps;

namespace RabbitSurvivor
{
    public class ArenaBackdrop : MonoBehaviour
    {
        const int Columns = 9;
        const int Rows = 5;

        [SerializeField] Sprite seafloor;
        [SerializeField] Material unlitMaterial;
        readonly SpriteRenderer[] backgrounds = new SpriteRenderer[Columns * Rows];
        Camera sceneCamera;
        Vector2 tileSize;
        Vector2Int lastTile = new Vector2Int(int.MinValue, int.MinValue);

        void Awake()
        {
            if (!seafloor)
                return;

            TilemapRenderer[] tilemaps = GetComponentsInChildren<TilemapRenderer>();
            if (tilemaps.Length == 0)
                return;

            tileSize = tilemaps[0].GetComponent<Tilemap>().localBounds.size;
            if (tileSize.x <= 0f || tileSize.y <= 0f)
                return;

            foreach (TilemapRenderer tiles in tilemaps)
                tiles.enabled = false;

            for (int index = 0; index < backgrounds.Length; index++) {
                SpriteRenderer background = new GameObject("Quiet Seafloor").AddComponent<SpriteRenderer>();
                background.transform.SetParent(transform, false);
                background.sprite = seafloor;
                if (unlitMaterial)
                    background.sharedMaterial = unlitMaterial;
                background.sortingLayerID = tilemaps[0].sortingLayerID;
                background.sortingOrder = tilemaps[0].sortingOrder;
                background.transform.localScale = new Vector3(
                    tileSize.x / seafloor.bounds.size.x, tileSize.y / seafloor.bounds.size.y, 1f);
                backgrounds[index] = background;
            }
            sceneCamera = Camera.main;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (!sceneCamera)
                return;

            Vector3 view = transform.InverseTransformPoint(sceneCamera.transform.position);
            var center = new Vector2Int(Mathf.FloorToInt(view.x / tileSize.x), Mathf.FloorToInt(view.y / tileSize.y));
            if (center == lastTile)
                return;
            lastTile = center;

            for (int index = 0; index < backgrounds.Length; index++) {
                int x = center.x + index % Columns - Columns / 2;
                int y = center.y + index / Columns - Rows / 2;
                SpriteRenderer background = backgrounds[index];
                background.transform.localPosition = new Vector3((x + 0.5f) * tileSize.x, (y + 0.5f) * tileSize.y);
                background.flipX = (x & 1) != 0;
                background.flipY = (y & 1) != 0;
            }
        }
    }
}
