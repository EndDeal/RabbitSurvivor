using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RabbitSurvivor
{
    public sealed class FloatingStick : MonoBehaviour, IPointerDownHandler, IInitializePotentialDragHandler, IDragHandler, IPointerUpHandler
    {
        RectTransform surface;
        RectTransform ring;
        RectTransform knob;
        Player player;
        Vector2 origin;
        int pointer = int.MinValue;

        internal void Initialize(Player target, RectTransform visual)
        {
            player = target;
            surface = (RectTransform)transform;
            ring = visual;
            ring.SetParent(surface, false);
            ring.anchorMin = ring.anchorMax = ring.pivot = new Vector2(0.5f, 0.5f);
            ring.sizeDelta = Vector2.one * 29f;
            ring.localScale = Vector3.one;
            knob = (RectTransform)ring.GetChild(0);
            knob.anchorMin = knob.anchorMax = knob.pivot = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = Vector2.one * 11f;
            Image rim = ring.GetComponent<Image>();
            rim.color = new Color(1f, 1f, 1f, 0.25f);
            rim.raycastTarget = false;
            Image handle = knob.GetComponent<Image>();
            handle.color = new Color(1f, 1f, 1f, 0.63f);
            handle.raycastTarget = false;
            ring.gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointer != int.MinValue || !GameManager.instance.isLive || !LocalPoint(eventData, out origin))
                return;

            pointer = eventData.pointerId;
            ring.anchoredPosition = origin - surface.rect.center;
            knob.anchoredPosition = Vector2.zero;
            ring.gameObject.SetActive(true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (pointer != eventData.pointerId || !LocalPoint(eventData, out Vector2 position))
                return;

            const float radius = 10f;
            Vector2 offset = Vector2.ClampMagnitude(position - origin, radius);
            knob.anchoredPosition = offset;
            player.inputVec = offset / radius;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pointer == eventData.pointerId)
                Release();
        }

        void OnDisable() => Release();

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
                Release();
        }

        bool LocalPoint(PointerEventData eventData, out Vector2 point)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, eventData.position,
                eventData.pressEventCamera, out point);
        }

        void Release()
        {
            pointer = int.MinValue;
            if (ring)
                ring.gameObject.SetActive(false);
            if (player)
                player.inputVec = Vector2.zero;
        }
    }
}
