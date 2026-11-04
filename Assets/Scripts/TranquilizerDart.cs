using UnityEngine;

namespace HauntedItems
{
    public class TranquilizerDart : MonoBehaviour
    {
        public float lifetime = 8f;
        public float gravityScale = 0.2f;
        public float radius = 0.02f;
        public float stuckLifetime = 30f;
        public LayerMask hitMask = ~0;
        public event System.Action<StunnableEnemy> HitEnemy;

        Vector3 velocity; float stunSeconds; Transform ignoreRoot;
        bool launched, finished; float dieAt;

        public void Launch(Vector3 dir, float speed, float stun, Transform owner)
        {
            velocity = dir.normalized * speed;
            stunSeconds = stun;
            ignoreRoot = owner;
            launched = true;
            dieAt = Time.time + lifetime;
            transform.rotation = Quaternion.LookRotation(dir);
        }

        void Update()
        {
            if (!launched || finished) return;
            float dt = Time.deltaTime;
            velocity += Physics.gravity * gravityScale * dt;
            Vector3 step = velocity * dt;
            float dist = step.magnitude;
            if (dist <= 0f) return;
            Vector3 dir = step / dist;

            var hits = Physics.SphereCastAll(transform.position, radius, dir, dist, hitMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; RaycastHit bestHit = default; bool found = false;
            foreach (var h in hits)
            {
                if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot)) continue;
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.distance < best) { best = h.distance; bestHit = h; found = true; }
            }
            if (found)
            {
                transform.position += dir * Mathf.Max(0f, bestHit.distance);
                Impact(bestHit.collider);
                return;
            }
            transform.position += step;
            transform.rotation = Quaternion.LookRotation(velocity);
            if (Time.time > dieAt) Destroy(gameObject);
        }

        void Impact(Collider c)
        {
            finished = true;
            var enemy = c.GetComponentInParent<StunnableEnemy>();
            if (enemy != null)
            {
                enemy.Stun(stunSeconds);
                HitEnemy?.Invoke(enemy);
            }
            transform.SetParent(c.transform, true);
            Destroy(gameObject, enemy != null ? stuckLifetime : 15f);
        }
    }
}