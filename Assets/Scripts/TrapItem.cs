using System;
using System.Collections;
using UnityEngine;
using HorrorItems;

namespace HauntedItems
{
    [DisallowMultipleComponent]
    public class TrapItem : MonoBehaviour, IInteractable
    {
        public enum State
        {
            Disarmed,
            Arming,
            Armed,
            Sprung
        }

        [Header("Trap Settings")]
        public float stunDuration = 60f;

        [Tooltip("Legacy value. No delay is used.")]
        public float armDelay = 0f;

        [Tooltip("ON for already placed traps. OFF for pickups.")]
        public bool armOnStart = false;

        public bool ignoreAlreadyStunned = true;
        public bool autoAddReceiver = true;
        public string enemyTag = "Enemy";

        public LayerMask scanMask = ~0;

        public Vector3 triggerCenter =
            new Vector3(0f, 0.12f, 0f);

        public Vector3 triggerSize =
            new Vector3(0.6f, 0.25f, 0.6f);

        [Header("Trap Parts")]
        public Transform jawLeft;
        public Transform jawRight;
        public Renderer statusLight;

        public float openAngle = 8f;
        public float closedAngle = 145f;

        public AudioClip snapSound;

        [Header("Debug")]
        public bool showDebugLogs = true;

        public State state { get; private set; }
            = State.Disarmed;

        public event Action<TrapItem, StunReceiver> Triggered;
        public event Action<TrapItem, GameObject> TriggeredEnemy;

        private readonly Collider[] scanBuffer =
            new Collider[64];

        private float nextScan;
        private Coroutine snapCoroutine;

        private bool initialized;

        private void Awake()
        {
            state = State.Disarmed;
        }

        private void Start()
        {
            if (initialized)
                return;

            initialized = true;

            if (armOnStart)
                Place();
            else
                SetAsPickup();
        }

        // ========================================
        // INTERACT (E pickup, same as bottle / note)
        // ========================================

        public bool CanBePickedUp =>
            state == State.Disarmed && !armOnStart;

        public string Prompt =>
            CanBePickedUp ? "Pick up bear trap" : "";

        public void Interact(PlayerController player)
        {
            Debug.Log("[Trap] Interact called", this);

            if (!CanBePickedUp)
            {
                Debug.Log(
                    "[Trap] Not pickable. State: " + state +
                    ", armOnStart: " + armOnStart,
                    this);
                return;
            }

            TrapHotbarController hotbar = null;

            if (player != null)
                hotbar = player.GetComponent<TrapHotbarController>();

            if (hotbar == null)
                hotbar = FindFirstObjectByType<TrapHotbarController>();

            if (hotbar == null)
            {
                Debug.LogError(
                    "[Trap] No TrapHotbarController in scene. " +
                    "Add it to the Player.",
                    this);
                return;
            }

            hotbar.PickUp(this);
        }

        // ========================================
        // WORLD PICKUP STATE
        // ========================================

        public void SetAsPickup()
        {
            if (snapCoroutine != null)
            {
                StopCoroutine(snapCoroutine);
                snapCoroutine = null;
            }

            initialized = true;

            state = State.Disarmed;
            armOnStart = false;
            armDelay = 0f;

            SetJawAngle(openAngle);

            if (statusLight != null)
            {
                ItemMaterials.SetEmission(
                    statusLight,
                    Color.black);
            }
        }

        // ========================================
        // INSTANT PLACEMENT
        // ========================================

        public void Place()
        {
            initialized = true;

            state = State.Armed;

            armOnStart = true;
            armDelay = 0f;

            nextScan = Time.time + 0.05f;

            SetJawAngle(openAngle);

            if (statusLight != null)
            {
                ItemMaterials.SetEmission(
                    statusLight,
                    new Color(0.6f, 0.07f, 0.025f));
            }

            if (showDebugLogs)
            {
                Debug.Log(
                    "[Trap] ARMED INSTANTLY: " + name,
                    this);
            }
        }

        public void SetJawAngle(float angle)
        {
            if (jawRight != null)
            {
                jawRight.localRotation =
                    Quaternion.Euler(0f, 0f, angle);
            }

            if (jawLeft != null)
            {
                jawLeft.localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        180f - angle);
            }
        }

        // ========================================
        // DETECT ENEMY
        // ========================================

        private void Update()
        {
            if (state != State.Armed)
                return;

            if (statusLight != null)
            {
                float pulse =
                    0.35f +
                    0.4f *
                    Mathf.Abs(Mathf.Sin(Time.time * 2f));

                ItemMaterials.SetEmission(
                    statusLight,
                    new Color(
                        1.3f, 0.07f, 0.03f) * pulse);
            }

            if (Time.time < nextScan)
                return;

            nextScan = Time.time + 0.08f;

            ScanForEnemy();
        }

        private void ScanForEnemy()
        {
            Vector3 scale = transform.lossyScale;

            Vector3 half = new Vector3(
                Mathf.Abs(triggerSize.x * scale.x) * 0.5f,
                Mathf.Abs(triggerSize.y * scale.y) * 0.5f,
                Mathf.Abs(triggerSize.z * scale.z) * 0.5f);

            int count = Physics.OverlapBoxNonAlloc(
                transform.TransformPoint(triggerCenter),
                half,
                scanBuffer,
                transform.rotation,
                scanMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider hit = scanBuffer[i];

                if (hit == null)
                    continue;

                if (hit.transform.IsChildOf(transform))
                    continue;

                EnemyAI ai =
                    hit.GetComponentInParent<EnemyAI>();

                EnemyController controller =
                    hit.GetComponentInParent<EnemyController>();

                StunnableEnemy stun =
                    hit.GetComponentInParent<StunnableEnemy>();

                StunReceiver receiver =
                    hit.GetComponentInParent<StunReceiver>();

                Transform taggedRoot = null;

                if (!string.IsNullOrEmpty(enemyTag))
                {
                    for (Transform t = hit.transform;
                         t != null;
                         t = t.parent)
                    {
                        if (t.gameObject.tag == enemyTag)
                            taggedRoot = t;
                    }
                }

                if (ai == null &&
                    controller == null &&
                    stun == null &&
                    receiver == null &&
                    taggedRoot == null)
                {
                    continue;
                }

                GameObject enemy;

                if (ai != null)
                    enemy = ai.gameObject;
                else if (controller != null)
                    enemy = controller.gameObject;
                else if (stun != null)
                    enemy = stun.gameObject;
                else if (receiver != null)
                    enemy = receiver.gameObject;
                else
                    enemy = taggedRoot.gameObject;

                if (ignoreAlreadyStunned)
                {
                    if (stun != null && stun.IsStunned)
                        continue;

                    if (receiver != null && receiver.IsStunned)
                        continue;
                }

                if (stun == null &&
                    receiver == null &&
                    autoAddReceiver)
                {
                    stun =
                        enemy.GetComponent<StunnableEnemy>();

                    if (stun == null)
                    {
                        stun =
                            enemy.AddComponent<StunnableEnemy>();
                    }
                }

                if (stun == null && receiver == null)
                    continue;

                Spring(enemy, stun, receiver);
                return;
            }
        }

        // ========================================
        // SNAP / STUN
        // ========================================

        private void Spring(
            GameObject enemy,
            StunnableEnemy stun,
            StunReceiver receiver)
        {
            state = State.Sprung;

            if (stun != null)
            {
                stun.Stun(stunDuration);
            }
            else if (receiver != null)
            {
                receiver.Stun(
                    stunDuration,
                    StunSource.Trap);
            }

            TriggeredEnemy?.Invoke(this, enemy);

            if (receiver != null)
                Triggered?.Invoke(this, receiver);

            if (snapSound != null)
            {
                AudioSource.PlayClipAtPoint(
                    snapSound,
                    transform.position);
            }

            if (snapCoroutine != null)
                StopCoroutine(snapCoroutine);

            snapCoroutine =
                StartCoroutine(SnapAnimation());

            if (showDebugLogs)
            {
                Debug.Log(
                    "[Trap] Enemy trapped: " + enemy.name,
                    this);
            }
        }

        private IEnumerator SnapAnimation()
        {
            GameObject flash =
                new GameObject("TrapSnapFlash");

            flash.transform.SetParent(transform, false);

            flash.transform.localPosition =
                new Vector3(0f, 0.3f, 0f);

            Light light =
                flash.AddComponent<Light>();

            light.color =
                new Color(1f, 0.12f, 0.08f);

            light.range = 4f;
            light.intensity = 3f;

            float duration = 0.12f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                SetJawAngle(
                    Mathf.Lerp(
                        openAngle,
                        closedAngle,
                        Mathf.Clamp01(elapsed / duration)));

                yield return null;
            }

            SetJawAngle(closedAngle);

            if (statusLight != null)
            {
                ItemMaterials.SetEmission(
                    statusLight,
                    new Color(2.5f, 0.08f, 0.04f));
            }

            elapsed = 0f;

            while (elapsed < 0.45f)
            {
                elapsed += Time.deltaTime;

                if (light != null)
                {
                    light.intensity =
                        Mathf.Lerp(
                            3f,
                            0f,
                            Mathf.Clamp01(elapsed / 0.45f));
                }

                yield return null;
            }

            Destroy(flash);
            snapCoroutine = null;
        }
    }
}