using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace HauntedItems
{
    /// Idagdag sa root ng bawat enemy (slime, dolls, etc.)
    [DisallowMultipleComponent]
    public class StunnableEnemy : MonoBehaviour
    {
        [Header("Stun behaviour")]
        public bool disableOtherScripts = true;
        public List<Behaviour> alwaysKeepEnabled = new List<Behaviour>();
        public bool freezeAnimator = true;
        public bool tintWhileStunned = true;
        public Color stunTint = new Color(0.45f, 0.6f, 0.95f, 1f);

        [Header("Hooks (para i-stop ang custom coroutines/AI mo)")]
        public UnityEvent onStunStart;
        public UnityEvent onStunEnd;

        public event System.Action<StunnableEnemy, float> Stunned;
        public event System.Action<StunnableEnemy> Recovered;

        public bool IsStunned { get; private set; }
        public float Remaining => IsStunned ? Mathf.Max(0f, endTime - Time.time) : 0f;

        float endTime;
        readonly List<Behaviour> disabled = new List<Behaviour>();
        NavMeshAgent agent; bool agentWasStopped;
        Animator anim; float animSpeed;
        Rigidbody rb; bool rbWasKinematic;
        Renderer[] renderers; MaterialPropertyBlock mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public void Stun(float seconds)
        {
            if (seconds <= 0f) return;
            float newEnd = Time.time + seconds;
            if (IsStunned) { if (newEnd > endTime) endTime = newEnd; return; }
            IsStunned = true;
            endTime = newEnd;
            Freeze();
            onStunStart?.Invoke();
            Stunned?.Invoke(this, seconds);
        }

        void Update()
        {
            if (IsStunned && Time.time >= endTime) Recover();
        }

        void Freeze()
        {
            disabled.Clear();
            if (disableOtherScripts)
            {
                foreach (var b in GetComponentsInChildren<MonoBehaviour>())
                {
                    if (b == null || b == this || !b.enabled || b is StunnableEnemy) continue;
                    if (alwaysKeepEnabled.Contains(b)) continue;
                    b.enabled = false;
                    disabled.Add(b);
                }
            }

            agent = GetComponentInChildren<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agentWasStopped = agent.isStopped;
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            else agent = null;

            if (freezeAnimator)
            {
                anim = GetComponentInChildren<Animator>();
                if (anim != null) { animSpeed = anim.speed; anim.speed = 0f; }
            }

            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rbWasKinematic = rb.isKinematic;
                if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                rb.isKinematic = true;
            }

            if (tintWhileStunned)
            {
                renderers = GetComponentsInChildren<Renderer>();
                mpb = new MaterialPropertyBlock();
                foreach (var r in renderers)
                {
                    if (r is ParticleSystemRenderer) continue;
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor(BaseColorId, stunTint);
                    mpb.SetColor(ColorId, stunTint);
                    r.SetPropertyBlock(mpb);
                }
            }
        }

        void Recover()
        {
            IsStunned = false;
            foreach (var b in disabled) if (b != null) b.enabled = true;
            disabled.Clear();
            if (agent != null) agent.isStopped = agentWasStopped;
            if (anim != null) anim.speed = animSpeed;
            if (rb != null) rb.isKinematic = rbWasKinematic;
            if (renderers != null)
                foreach (var r in renderers) if (r != null) r.SetPropertyBlock(null);
            onStunEnd?.Invoke();
            Recovered?.Invoke(this);
        }
    }
}