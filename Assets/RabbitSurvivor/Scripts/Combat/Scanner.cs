using System.Collections.Generic;
using UnityEngine;

namespace RabbitSurvivor
{
    public class Scanner : MonoBehaviour
    {
        public float scanRange;
        public LayerMask targetLayer;
        readonly List<Collider2D> targets = new List<Collider2D>(128);
        ContactFilter2D filter;
        Collider2D nearestCollider;
        double lastScanTime = double.NegativeInfinity;

        void Awake()
        {
            filter.SetLayerMask(targetLayer);
            filter.useTriggers = false;
        }

        internal Transform GetNearestTarget()
        {
            // Share a query until physics advances or the cached collider is disabled.
            double physicsTime = Time.fixedTimeAsDouble;
            if (lastScanTime == physicsTime &&
                (ReferenceEquals(nearestCollider, null) || (nearestCollider && nearestCollider.isActiveAndEnabled)))
                return nearestCollider ? nearestCollider.transform : null;

            lastScanTime = physicsTime;
            Vector2 position = transform.position;
            Physics2D.OverlapCircle(position, scanRange, filter, targets);
            nearestCollider = null;
            float bestDistance = float.PositiveInfinity;
            foreach (Collider2D target in targets) {
                Transform candidate = target.transform;
                float distance = ((Vector2)candidate.position - position).sqrMagnitude;
                if (distance < bestDistance) {
                    bestDistance = distance;
                    nearestCollider = target;
                }
            }
            return nearestCollider ? nearestCollider.transform : null;
        }
    }
}
