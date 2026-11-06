
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace HauntedItems
{
    // THE LAST LULLABY
    // Unified stun component for Eleanor and other enemies.
    //
    // Compatible with:
    // - TranquilizerDart
    // - TranquilizerGun
    // - EnemyAI
    // - EnemyController
    // - NavMeshAgent
    // - Animator
    //
    // Attach to the enemy root (Woman).
    //
    // NOTE:
    // Only ONE class named HauntedItems.StunnableEnemy
    // must exist in the entire Unity project.

    [DisallowMultipleComponent]
    public class StunnableEnemy : MonoBehaviour
    {
        [Header("Stun Behaviour")]
        public bool disableOtherScripts = true;

        [Tooltip("Scripts that must remain enabled during stun.")]
        public List<Behaviour> alwaysKeepEnabled =
            new List<Behaviour>();

        [Tooltip("Additional enemy scripts to pause.")]
        public List<Behaviour> behavioursToPause =
            new List<Behaviour>();

        public bool freezeAnimator = true;
        public bool stopNavMeshAgent = true;

        [Header("Visual Feedback")]
        public bool tintWhileStunned = true;

        public Color stunTint =
            new Color(0.45f, 0.60f, 0.95f, 1f);

        [Header("Events")]
        public UnityEvent onStunStart;
        public UnityEvent onStunEnd;

        public event Action<StunnableEnemy, float> Stunned;
        public event Action<StunnableEnemy> Recovered;

        [Header("Debug")]
        public bool showDebugLogs = true;

        // -------------------------------------------------
        // PUBLIC STATE
        // -------------------------------------------------

        public bool IsStunned { get; private set; }

        public float Remaining
        {
            get
            {
                return IsStunned
                    ? Mathf.Max(0f, endTime - Time.time)
                    : 0f;
            }
        }

        // -------------------------------------------------
        // INTERNAL STATE
        // -------------------------------------------------

        private float endTime;

        private NavMeshAgent agent;
        private Animator animator;
        private Rigidbody rb;

        private bool agentWasStopped;
        private bool agentWasValid;

        private float savedAnimatorSpeed = 1f;

        private bool rbWasKinematic;
        private RigidbodyConstraints savedConstraints;
        private bool rbWasFrozen;

        private readonly List<Behaviour> disabledScripts =
            new List<Behaviour>();

        private Renderer[] tintedRenderers;

        private MaterialPropertyBlock[] savedPropertyBlocks;

        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");

        private static readonly int ColorId =
            Shader.PropertyToID("_Color");

        // -------------------------------------------------
        // UNITY
        // -------------------------------------------------

        private void Awake()
        {
            CacheReferences();
        }

        private void CacheReferences()
        {
            agent = GetComponent<NavMeshAgent>();

            if (agent == null)
            {
                agent =
                    GetComponentInChildren<NavMeshAgent>(true);
            }

            animator =
                GetComponentInChildren<Animator>(true);

            rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (!IsStunned)
                return;

            if (Time.time >= endTime)
            {
                Recover();
            }
        }

        private void LateUpdate()
        {
            if (!IsStunned)
                return;

            // Ensure that the enemy remains stopped
            // throughout the stun duration.
            if (stopNavMeshAgent &&
                agent != null &&
                agent.enabled &&
                agent.isOnNavMesh)
            {
                if (!agent.isStopped)
                    agent.isStopped = true;

                agent.velocity = Vector3.zero;
            }

            if (freezeAnimator &&
                animator != null &&
                animator.enabled)
            {
                animator.speed = 0f;
            }
        }

        // -------------------------------------------------
        // STUN
        // -------------------------------------------------

        public void Stun(float seconds)
        {
            if (seconds <= 0f)
                return;

            float newEndTime =
                Time.time + seconds;

            // Being hit again extends the stun.
            if (IsStunned)
            {
                endTime = Mathf.Max(
                    endTime,
                    newEndTime);

                if (showDebugLogs)
                {
                    Debug.Log(
                        "[Tranquilizer] Stun extended on " +
                        name +
                        ". Remaining: " +
                        Remaining.ToString("F1") +
                        " seconds.",
                        this);
                }

                return;
            }

            CacheReferences();

            IsStunned = true;
            endTime = newEndTime;

            FreezeEnemy();

            if (showDebugLogs)
            {
                Debug.Log(
                    "[Tranquilizer] " +
                    name +
                    " STUNNED for " +
                    seconds +
                    " seconds!",
                    this);
            }

            onStunStart?.Invoke();
            Stunned?.Invoke(this, seconds);
        }

        // -------------------------------------------------
        // FREEZE ENEMY
        // -------------------------------------------------

        private void FreezeEnemy()
        {
            disabledScripts.Clear();

            if (disableOtherScripts)
            {
                // Eleanor's actual AI.
                EnemyAI enemyAI =
                    GetComponentInChildren<EnemyAI>(true);

                PauseBehaviour(enemyAI);

                // Alternative movement system.
                EnemyController enemyController =
                    GetComponentInChildren<EnemyController>(true);

                PauseBehaviour(enemyController);

                // Optional extra AI scripts.
                foreach (Behaviour behaviour
                         in behavioursToPause)
                {
                    PauseBehaviour(behaviour);
                }
            }

            // Pause NavMesh movement.
            agentWasValid = false;

            if (stopNavMeshAgent &&
                agent != null &&
                agent.enabled &&
                agent.isOnNavMesh)
            {
                agentWasStopped = agent.isStopped;
                agentWasValid = true;

                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            // Freeze animation.
            if (freezeAnimator && animator != null)
            {
                savedAnimatorSpeed = animator.speed;
                animator.speed = 0f;
            }

            // Freeze physics if enemy uses Rigidbody.
            rbWasFrozen = false;

            if (rb != null)
            {
                rbWasKinematic = rb.isKinematic;
                savedConstraints = rb.constraints;

                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                rb.isKinematic = true;

                rb.constraints =
                    RigidbodyConstraints.FreezeAll;

                rbWasFrozen = true;
            }

            if (tintWhileStunned)
                ApplyTint();
        }

        private void PauseBehaviour(
            Behaviour behaviour)
        {
            if (behaviour == null)
                return;

            if (behaviour == this)
                return;

            if (!behaviour.enabled)
                return;

            if (alwaysKeepEnabled != null &&
                alwaysKeepEnabled.Contains(behaviour))
            {
                return;
            }

            if (disabledScripts.Contains(behaviour))
                return;

            behaviour.enabled = false;

            disabledScripts.Add(behaviour);
        }

        // -------------------------------------------------
        // VISUAL TINT
        // -------------------------------------------------

        private void ApplyTint()
        {
            tintedRenderers =
                GetComponentsInChildren<Renderer>(true);

            savedPropertyBlocks =
                new MaterialPropertyBlock[
                    tintedRenderers.Length];

            for (int i = 0;
                 i < tintedRenderers.Length;
                 i++)
            {
                Renderer renderer =
                    tintedRenderers[i];

                if (renderer == null ||
                    renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                MaterialPropertyBlock original =
                    new MaterialPropertyBlock();

                renderer.GetPropertyBlock(original);

                savedPropertyBlocks[i] = original;

                MaterialPropertyBlock tinted =
                    new MaterialPropertyBlock();

                renderer.GetPropertyBlock(tinted);

                Material material =
                    renderer.sharedMaterial;

                if (material == null)
                    continue;

                if (material.HasProperty(BaseColorId))
                {
                    tinted.SetColor(
                        BaseColorId,
                        material.GetColor(BaseColorId) *
                        stunTint);
                }
                else if (material.HasProperty(ColorId))
                {
                    tinted.SetColor(
                        ColorId,
                        material.GetColor(ColorId) *
                        stunTint);
                }

                renderer.SetPropertyBlock(tinted);
            }
        }

        private void RestoreTint()
        {
            if (tintedRenderers == null)
                return;

            for (int i = 0;
                 i < tintedRenderers.Length;
                 i++)
            {
                Renderer renderer =
                    tintedRenderers[i];

                if (renderer == null)
                    continue;

                if (savedPropertyBlocks != null &&
                    i < savedPropertyBlocks.Length &&
                    savedPropertyBlocks[i] != null)
                {
                    renderer.SetPropertyBlock(
                        savedPropertyBlocks[i]);
                }
                else
                {
                    renderer.SetPropertyBlock(null);
                }
            }

            tintedRenderers = null;
            savedPropertyBlocks = null;
        }

        // -------------------------------------------------
        // RECOVER
        // -------------------------------------------------

        public void ClearStun()
        {
            if (IsStunned)
                Recover();
        }

        private void Recover()
        {
            if (!IsStunned)
                return;

            IsStunned = false;
            endTime = 0f;

            // Restore NavMeshAgent.
            if (agentWasValid &&
                agent != null &&
                agent.enabled &&
                agent.isOnNavMesh)
            {
                agent.isStopped = agentWasStopped;
            }

            agentWasValid = false;

            // Restore Animator.
            if (freezeAnimator && animator != null)
            {
                animator.speed = savedAnimatorSpeed;
            }

            // Restore Rigidbody.
            if (rbWasFrozen && rb != null)
            {
                rb.constraints = savedConstraints;
                rb.isKinematic = rbWasKinematic;
            }

            rbWasFrozen = false;

            // Restore visuals.
            RestoreTint();

            // Restore only the scripts we disabled.
            foreach (Behaviour behaviour
                     in disabledScripts)
            {
                if (behaviour != null)
                    behaviour.enabled = true;
            }

            disabledScripts.Clear();

            if (showDebugLogs)
            {
                Debug.Log(
                    "[Tranquilizer] " +
                    name +
                    " recovered. Enemy AI resumed.",
                    this);
            }

            onStunEnd?.Invoke();
            Recovered?.Invoke(this);
        }

        // -------------------------------------------------
        // CLEANUP
        // -------------------------------------------------

        private void OnDisable()
        {
            if (IsStunned)
                Recover();
        }

        private void OnDestroy()
        {
            if (IsStunned)
                Recover();
        }
    }
}
