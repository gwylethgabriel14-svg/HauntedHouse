using System.Collections;
using UnityEngine;
using HorrorItems;

namespace HauntedItems
{
    public class TrapItem : MonoBehaviour
    {
        public enum State { Disarmed, Arming, Armed, Sprung }

        [Header("Settings")]
        public float stunDuration = 60f;
        public float armDelay = 1.5f;
        public bool armOnStart = true;
        public bool ignoreAlreadyStunned = true;
        public string enemyTag = "Enemy";
        public bool autoAddReceiver = true;
        public LayerMask scanMask = ~0;
        public Vector3 triggerCenter = new Vector3(0, 0.12f, 0);
        public Vector3 triggerSize = new Vector3(0.6f, 0.25f, 0.6f);

        [Header("Parts (auto-filled)")]
        public Transform jawLeft, jawRight;
        public Renderer statusLight;
        public float openAngle = 8f, closedAngle = 145f;
        public AudioClip snapSound;

        public State state { get; private set; } = State.Disarmed;
        public event System.Action<TrapItem, StunReceiver> Triggered;

        float armTime, nextScan;
        readonly Collider[] buffer = new Collider[32];

        void Start()
        {
            if (armOnStart) Place();
            else SetJawAngle(openAngle);
        }

        /// Tawagin kapag nailagay na sa sahig
        public void Place()
        {
            if (state == State.Sprung) return;
            SetJawAngle(openAngle);
            state = State.Arming;
            armTime = Time.time + armDelay;
        }

        public void SetJawAngle(float a)
        {
            if (jawRight) jawRight.localRotation = Quaternion.Euler(0, 0, a);
            if (jawLeft) jawLeft.localRotation = Quaternion.Euler(0, 0, 180f - a);
        }

        void Update()
        {
            if (state == State.Arming && Time.time >= armTime) state = State.Armed;
            if (state != State.Armed) return;

            float pulse = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Time.time * 2f));
            if (statusLight) ItemMaterials.SetEmission(statusLight, new Color(1.6f, 0.08f, 0.05f) * pulse * 0.6f);

            if (Time.time < nextScan) return;
            nextScan = Time.time + 0.08f;

            Vector3 half = Vector3.Scale(triggerSize, transform.lossyScale) * 0.5f;
            int n = Physics.OverlapBoxNonAlloc(transform.TransformPoint(triggerCenter), half, buffer,
                transform.rotation, scanMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < n; i++)
            {
                if (buffer[i].transform.IsChildOf(transform)) continue; // huwag tamaan ang sarili
                var e = StunReceiver.Find(buffer[i], autoAddReceiver, enemyTag);
                if (e == null) continue;
                if (ignoreAlreadyStunned && e.IsStunned) continue;
                Spring(e);
                return;
            }
        }

        void Spring(StunReceiver enemy)
        {
            state = State.Sprung;
            enemy.Stun(stunDuration, StunSource.Trap);
            Triggered?.Invoke(this, enemy);
            if (snapSound) AudioSource.PlayClipAtPoint(snapSound, transform.position);
            StartCoroutine(SnapRoutine());
        }

        IEnumerator SnapRoutine()
        {
            var go = new GameObject("TrapFlash");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 0.3f, 0);
            var l = go.AddComponent<Light>();
            l.color = new Color(1f, 0.15f, 0.1f); l.range = 4f; l.intensity = 4f;

            const float dur = 0.12f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                SetJawAngle(Mathf.Lerp(openAngle, closedAngle, t / dur));
                yield return null;
            }
            SetJawAngle(closedAngle);
            if (statusLight) ItemMaterials.SetEmission(statusLight, new Color(3f, 0.1f, 0.05f));

            for (float t = 0; t < 0.6f; t += Time.deltaTime)
            {
                l.intensity = Mathf.Lerp(4f, 0f, t / 0.6f);
                yield return null;
            }
            Destroy(go);
        }
    }
}