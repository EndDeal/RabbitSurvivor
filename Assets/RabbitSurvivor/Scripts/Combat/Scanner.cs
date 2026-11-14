using System.Collections.Generic;
using UnityEngine;

namespace RabbitSurvivor
{
    public class Scanner : MonoBehaviour
    {
        public float scanRange;
        public LayerMask targetLayer;
        public Transform nearestTarget;

        readonly List<Collider2D> targets = new List<Collider2D>(128);
        ContactFilter2D filter;

        void Awake()
        {
            filter.SetLayerMask(targetLayer);
            filter.useTriggers = false;
        }

        void FixedUpdate()
        {
            Vector2 position = transform.position;
            Physics2D.OverlapCircle(position, scanRange, filter, targets);
            nearestTarget = null;
            float bestDistance = float.PositiveInfinity;
            foreach (Collider2D target in targets) {
                float distance = ((Vector2)target.transform.position - position).sqrMagnitude;
                if (distance < bestDistance) {
                    bestDistance = distance;
                    nearestTarget = target.transform;
                }
            }
        }
    }
}
