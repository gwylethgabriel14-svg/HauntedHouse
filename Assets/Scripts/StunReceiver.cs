using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace HorrorItems
{
    public enum StunSource { Dart, Trap, Other }

    public interface IStunnable
    {
        bool IsStunned { get; }
        float StunRemaining { get; }
        void Stun(float seconds, StunSource source);
        void ClearStun();
    }

    /// <summary>
    /// Drop-in "freeze" component for enemies. It does NOT replace your enemy AI: it pauses the NavMeshAgent,
    /// the Animator and your AI scripts while stunned, then restores them. Added automatically to objects tagged
    /// with the enemy tag when hit by a dart / trap, or add it manually on the enemy root.
    /// </summary>
    [DisallowMultipleComponent]
    public class StunReceiver : MonoBehaviour, IStunnable
    {
        [Header("Auto-detected if left empty")]
        public NavMeshAgent agent;
        public Animator animator;
        public Rigidbody body;

        [Header("AI scripts to pause while stunned")]
        [Tooltip("Drag your enemy AI scripts here (most reliable).")]
        public List<Behaviour> behavioursToPause = new List<Behaviour>();
        [Tooltip("Also pause any script whose class name contains one of the keywords below.")]
        public bool autoDetectAIScripts = true;
        public string[] aiNameKeywords = { "AI", "Chase", "Patrol", "Monster", "Enemy", "Brain", "Hunt", "Wander", "Behaviour", "Behavior", "Movement", "Attack", "Vision", "Hearing" };
        public string[] excludeKeywords = { "Health", "Damage", "Stun", "Audio", "Sound", "Footstep", "Ragdoll" };

        [Header("Feedback")]
        public bool freezeAnimator = true;
        public bool tintWhileStunned = true;
        public Color dartTint = new Color(0.55f, 0.7f, 1f);
        public Color trapTint = new Color(1f, 0.7f, 0.55f);
        public bool showTimerLabel = true;

        public UnityEvent onStunned;
        public UnityEvent onRecovered;
        public static event Action<StunReceiver, StunSource> Stunned;
        public static event Action<StunReceiver> Recovered;

        bool stunned;
        float endTime;
        StunSource lastSource;
        readonly List<Behaviour> paused = new List<Behaviour>();
        float savedAnimSpeed = 1f;
        RigidbodyConstraints savedConstraints;
        bool bodyFrozen;
        Renderer[] tinted;
        static GUIStyle labelStyle;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public bool IsStunned => stunned;
        public float StunRemaining => stunned ? Mathf.Max(0f, endTime - Time.time) : 0f;
        public StunSource LastSource => lastSource;

        void Awake()
        {
            if (agent == null) agent = GetComponentInChildren<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (body == null) body = GetComponent<Rigidbody>();
        }

        /// <summary>Finds (or auto-adds) the StunReceiver for a collider that was hit.</summary>
        public static StunReceiver Find(Collider c, bool autoAdd, string enemyTag)
        {
            if (c == null) return null;
            var r = c.GetComponentInParent<StunReceiver>();
            if (r != null) return r;
            if (!autoAdd || string.IsNullOrEmpty(enemyTag)) return null;
            Transform topTagged = null;
            for (Transform t = c.transform; t != null; t = t.parent)
                if (t.gameObject.tag == enemyTag) topTagged = t;
            return topTagged != null ? topTagged.gameObject.AddComponent<StunReceiver>() : null;
        }

        public void Stun(float seconds, StunSource source)
        {
            if (seconds <= 0f) return;
            float newEnd = Time.time + seconds;
            lastSource = source;
            if (stunned) { if (newEnd > endTime) endTime = newEnd; return; } // re-hit: keep the longer timer
            stunned = true;
            endTime = newEnd;
            ApplyStun(source);
            Stunned?.Invoke(this, source);
            onStunned?.Invoke();
        }

        public void ClearStun() { if (stunned) Recover(); }

        void Update() { if (stunned && Time.time >= endTime) Recover(); }

        void LateUpdate()
        {
            // keep the agent frozen even if some other script touches it
            if (stunned && agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }

        void ApplyStun(StunSource src)
        {
            CollectBehaviours();
            foreach (var b in paused) b.enabled = false;

            if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
            if (animator != null && freezeAnimator) { savedAnimSpeed = animator.speed; animator.speed = 0f; }
            if (body != null && !body.isKinematic) { savedConstraints = body.constraints; body.constraints = RigidbodyConstraints.FreezeAll; bodyFrozen = true; }
            if (tintWhileStunned) ApplyTint(src == StunSource.Trap ? trapTint : dartTint);
        }

        void Recover()
        {
            stunned = false;
            foreach (var b in paused) if (b != null) b.enabled = true;
            paused.Clear();
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
            if (animator != null && freezeAnimator) animator.speed = savedAnimSpeed;
            if (bodyFrozen && body != null) { body.constraints = savedConstraints; bodyFrozen = false; }
            ClearTint();
            Recovered?.Invoke(this);
            onRecovered?.Invoke();
        }

        void CollectBehaviours()
        {
            paused.Clear();
            foreach (var b in behavioursToPause)
                if (b != null && b.enabled && b != this && !paused.Contains(b)) paused.Add(b);
            if (!autoDetectAIScripts) return;

            foreach (var mb in GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || mb == this || !mb.enabled || paused.Contains(mb)) continue;
                Type t = mb.GetType();
                string ns = t.Namespace ?? "";
                if (ns.StartsWith("UnityEngine") || ns.StartsWith("Unity") || ns.StartsWith("TMPro") || ns.StartsWith("Cinemachine") || ns.StartsWith("HorrorItems")) continue;
                if (Matches(t.Name, excludeKeywords)) continue;
                if (Matches(t.Name, aiNameKeywords)) paused.Add(mb);
            }
        }

        static bool Matches(string n, string[] keys)
        {
            if (keys == null) return false;
            foreach (var k in keys)
                if (!string.IsNullOrEmpty(k) && n.IndexOf(k, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        void ApplyTint(Color tint)
        {
            tinted = GetComponentsInChildren<Renderer>();
            foreach (var r in tinted)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                var m = r.sharedMaterial; if (m == null) continue;
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                if (m.HasProperty(BaseColorId)) mpb.SetColor(BaseColorId, m.GetColor(BaseColorId) * tint);
                if (m.HasProperty(ColorId)) mpb.SetColor(ColorId, m.GetColor(ColorId) * tint);
                r.SetPropertyBlock(mpb);
            }
        }

        void ClearTint()
        {
            if (tinted == null) return;
            foreach (var r in tinted) if (r != null) r.SetPropertyBlock(null);
            tinted = null;
        }

        void OnGUI()
        {
            if (!stunned || !showTimerLabel) return;
            var cam = Camera.main; if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.2f);
            if (sp.z <= 0f) return;
            if (labelStyle == null) { labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold }; }
            labelStyle.normal.textColor = lastSource == StunSource.Trap ? new Color(1f, .6f, .3f) : new Color(.6f, .8f, 1f);
            GUI.Label(new Rect(sp.x - 60, Screen.height - sp.y - 12, 120, 24), "Zzz " + Mathf.CeilToInt(StunRemaining) + "s", labelStyle);
        }
    }
}
