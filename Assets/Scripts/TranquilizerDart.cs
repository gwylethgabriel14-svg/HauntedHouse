
using System;
using UnityEngine;
using HorrorItems;

namespace HauntedItems
{
    [DisallowMultipleComponent]
    public class TranquilizerDart : MonoBehaviour
    {
        [Header("Projectile Settings")]
        public float lifetime = 8f;
        public float gravityScale = 0.2f;
        public float radius = 0.06f;
        public float stuckLifetime = 12f;
        public LayerMask hitMask = ~0;

        [Header("Enemy Detection")]
        public bool autoAddStunComponent = true;
        public string enemyTag = "Enemy";

        [Header("Debug")]
        public bool showDebugLogs = true;

        public event Action<StunnableEnemy> HitEnemy;

        private Vector3 velocity;
        private float stunSeconds;
        private Transform ownerRoot;

        private bool launched;
        private bool finished;
        private float dieAt;

        private void Awake()
        {
            // Manual projectile collision with SphereCast.
            Rigidbody rb = GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.detectCollisions = false;
            }

            // Prevent the dart's own colliders
            // from interfering with manual collision.
            foreach (Collider col in
                GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }
        }

        // ===================================================
        // LAUNCH
        // ===================================================

        public void Launch(
            Vector3 direction,
            float speed,
            float stunDuration,
            Transform owner)
        {
            if (direction.sqrMagnitude < 0.0001f)
                direction = transform.forward;

            direction.Normalize();

            velocity = direction * Mathf.Max(1f, speed);

            stunSeconds = Mathf.Max(0f, stunDuration);
            ownerRoot = owner;

            launched = true;
            finished = false;

            dieAt = Time.time +
                    Mathf.Max(0.1f, lifetime);

            transform.rotation =
                Quaternion.LookRotation(direction);

            if (showDebugLogs)
            {
                Debug.Log(
                    "[DART] Launched | Speed: " +
                    speed + " | Stun: " +
                    stunSeconds + "s",
                    this);
            }
        }

        // ===================================================
        // PROJECTILE MOVEMENT
        // ===================================================

        private void Update()
        {
            if (!launched || finished)
                return;

            if (Time.time >= dieAt)
            {
                if (showDebugLogs)
                {
                    Debug.Log(
                        "[DART] Expired without an impact.",
                        this);
                }

                Destroy(gameObject);
                return;
            }

            float dt = Time.deltaTime;

            velocity +=
                Physics.gravity * gravityScale * dt;

            Vector3 movement = velocity * dt;
            float distance = movement.magnitude;

            if (distance <= 0.00001f)
                return;

            Vector3 direction = movement / distance;

            // Sweep the entire distance traveled
            // this frame to avoid tunneling.
            RaycastHit[] hits = Physics.SphereCastAll(
                transform.position,
                Mathf.Max(0.01f, radius),
                direction,
                distance + 0.02f,
                hitMask,
                QueryTriggerInteraction.Collide);

            Array.Sort(
                hits,
                (a, b) =>
                    a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                Collider col = hit.collider;

                if (col == null)
                    continue;

                if (ShouldIgnore(col))
                    continue;

                Transform enemy = FindEnemyRoot(col);

                // Ignore unrelated triggers.
                if (col.isTrigger && enemy == null)
                    continue;

                Vector3 impactPosition =
                    hit.point != Vector3.zero
                        ? hit.point
                        : transform.position +
                          direction *
                          Mathf.Max(0f, hit.distance);

                transform.position =
                    impactPosition -
                    direction * 0.025f;

                Impact(col);
                return;
            }

            transform.position += movement;

            if (velocity.sqrMagnitude > 0.0001f)
            {
                transform.rotation =
                    Quaternion.LookRotation(
                        velocity.normalized);
            }
        }

        // ===================================================
        // COLLIDER FILTERS
        // ===================================================

        private bool ShouldIgnore(Collider col)
        {
            if (col == null)
                return true;

            if (col.transform == transform ||
                col.transform.IsChildOf(transform))
                return true;

            // Ignore Player only.
            // Do not ignore the CHARACTERS parent.
            if (ownerRoot != null &&
                (col.transform == ownerRoot ||
                 col.transform.IsChildOf(ownerRoot)))
            {
                return true;
            }

            if (col.GetComponentInParent<TranquilizerGun>()
                != null)
            {
                return true;
            }

            return false;
        }

        // ===================================================
        // DETECT ELEANOR / OTHER ENEMIES
        // ===================================================

        private Transform FindEnemyRoot(Collider col)
        {
            if (col == null)
                return null;

            EnemyAI ai =
                col.GetComponentInParent<EnemyAI>();

            if (ai != null)
                return ai.transform;

            EnemyController controller =
                col.GetComponentInParent<EnemyController>();

            if (controller != null)
                return controller.transform;

            StunnableEnemy stun =
                col.GetComponentInParent<StunnableEnemy>();

            if (stun != null)
                return stun.transform;

            StunReceiver receiver =
                col.GetComponentInParent<StunReceiver>();

            if (receiver != null)
                return receiver.transform;

            // Fallback: an ancestor tagged Enemy.
            if (!string.IsNullOrEmpty(enemyTag))
            {
                Transform result = null;

                for (Transform t = col.transform;
                     t != null;
                     t = t.parent)
                {
                    if (t.gameObject.tag == enemyTag)
                        result = t;
                }

                return result;
            }

            return null;
        }

        // ===================================================
        // APPLY STUN
        // ===================================================

        private void Impact(Collider col)
        {
            finished = true;
            launched = false;

            if (col == null)
            {
                Destroy(gameObject);
                return;
            }

            if (showDebugLogs)
            {
                Debug.Log(
                    "[DART] HIT COLLIDER: " +
                    col.name +
                    " | Tag: " + col.tag,
                    col);
            }

            Transform enemyRoot = FindEnemyRoot(col);

            if (enemyRoot == null)
            {
                if (showDebugLogs)
                {
                    Debug.LogWarning(
                        "[DART] Hit NON-ENEMY: " +
                        col.name,
                        col);
                }

                StickAndDestroy(col.transform, 5f);
                return;
            }

            StunnableEnemy stunnable =
                enemyRoot.GetComponent<StunnableEnemy>();

            if (stunnable == null)
            {
                stunnable =
                    enemyRoot.GetComponentInChildren<
                        StunnableEnemy>(true);
            }

            StunReceiver receiver =
                enemyRoot.GetComponent<StunReceiver>();

            if (receiver == null)
            {
                receiver =
                    enemyRoot.GetComponentInChildren<
                        StunReceiver>(true);
            }

            if (stunnable == null &&
                receiver == null &&
                autoAddStunComponent)
            {
                stunnable =
                    enemyRoot.gameObject
                        .AddComponent<StunnableEnemy>();

                if (showDebugLogs)
                {
                    Debug.Log(
                        "[DART] Automatically added " +
                        "StunnableEnemy to " +
                        enemyRoot.name,
                        enemyRoot);
                }
            }

            bool success = false;

            if (stunnable != null)
            {
                if (!stunnable.enabled)
                    stunnable.enabled = true;

                stunnable.Stun(stunSeconds);

                HitEnemy?.Invoke(stunnable);

                success = true;
            }
            else if (receiver != null)
            {
                if (!receiver.enabled)
                    receiver.enabled = true;

                receiver.Stun(
                    stunSeconds,
                    StunSource.Dart);

                success = true;
            }

            if (success)
            {
                Debug.Log(
                    "[DART] SUCCESS! " +
                    enemyRoot.name +
                    " stunned for " +
                    stunSeconds + " seconds!",
                    enemyRoot);
            }
            else
            {
                Debug.LogWarning(
                    "[DART] Enemy recognized but " +
                    "stun was not applied.",
                    enemyRoot);
            }

            StickAndDestroy(
                col.transform,
                Mathf.Max(0.1f, stuckLifetime));
        }

        private void StickAndDestroy(
            Transform target,
            float seconds)
        {
            if (target != null)
            {
                transform.SetParent(target, true);
            }

            Destroy(gameObject, seconds);
        }
    }
}
