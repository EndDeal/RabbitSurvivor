using UnityEngine;
using UnityEngine.UI;

namespace RabbitSurvivor
{
    internal sealed class PlayStatusBorder : RawImage
    {
        const int MaskSize = 128;
        Texture2D edgeMask;

        protected override void Awake()
        {
            base.Awake();
            float width = MaskSize * 0.045f;
            var pixels = new Color32[MaskSize * MaskSize];
            for (int y = 0; y < MaskSize; y++) {
                for (int x = 0; x < MaskSize; x++) {
                    int horizontalEdge = Mathf.Min(x, MaskSize - 1 - x);
                    int verticalEdge = Mathf.Min(y, MaskSize - 1 - y);
                    float distance = Mathf.Min(horizontalEdge, verticalEdge);
                    byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(1f - distance / width));
                    pixels[y * MaskSize + x] = new Color32(255, 255, 255, alpha);
                }
            }

            edgeMask = new Texture2D(MaskSize, MaskSize, TextureFormat.RGBA32, false) {
                name = "Play Status Edge Mask",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            edgeMask.SetPixels32(pixels);
            edgeMask.Apply(false, true);
            texture = edgeMask;
        }

        protected override void OnDestroy()
        {
            if (edgeMask)
                Destroy(edgeMask);
            base.OnDestroy();
        }
    }
}
