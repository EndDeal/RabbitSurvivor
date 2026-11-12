using UnityEngine;
using UnityEngine.Tilemaps;

namespace Goldmetal.UndeadSurvivor
{
    public class ArenaBackdrop : MonoBehaviour
    {
        [SerializeField] Sprite seafloor;
        [SerializeField] Material unlitMaterial;

        void Awake()
        {
            if (!seafloor)
                return;

            foreach (TilemapRenderer tiles in GetComponentsInChildren<TilemapRenderer>()) {
                Bounds bounds = tiles.GetComponent<Tilemap>().localBounds;
                if (bounds.size.x <= 0f || bounds.size.y <= 0f)
                    continue;

                SpriteRenderer background = new GameObject("Quiet Seafloor").AddComponent<SpriteRenderer>();
                background.transform.SetParent(tiles.transform, false);
                background.sprite = seafloor;
                if (unlitMaterial)
                    background.sharedMaterial = unlitMaterial;
                background.sortingLayerID = tiles.sortingLayerID;
                background.sortingOrder = tiles.sortingOrder;
                background.transform.localPosition = bounds.center;
                background.transform.localScale = new Vector3(
                    bounds.size.x / seafloor.bounds.size.x, bounds.size.y / seafloor.bounds.size.y, 1f);

                // Ground sections recycle two section widths at a time, preserving the mirrored seams.
                background.flipX = Mirrored(tiles.transform.localPosition.x, bounds.size.x);
                background.flipY = Mirrored(tiles.transform.localPosition.y, bounds.size.y);
                tiles.enabled = false;
            }
        }

        static bool Mirrored(float position, float size)
        {
            return (Mathf.RoundToInt(position / size - 0.5f) & 1) != 0;
        }
    }
}
