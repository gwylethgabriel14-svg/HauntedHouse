using System.Collections;
using UnityEngine;

namespace HauntedItems
{
    public class TranquilizerGun : MonoBehaviour
    {
        [Header("Ammo")]
        public int magazineSize = 3;
        public int ammoInMagazine = 3;

        [Header("Stats")]
        public float stunSeconds = 120f;
        public float dartSpeed = 45f;
        public float fireCooldown = 0.6f;
        public float reloadSeconds = 1.8f;
        public float maxAimRange = 100f;

        [Header("Controls")]
        public int fireMouseButton = 0;
        public KeyCode reloadKey = KeyCode.R;

        [Header("Parts (auto-filled)")]
        public Transform muzzle, magazine, bolt, chamberDart;
        public Renderer[] ammoLights;
        public GameObject dartPrefab;

        [Header("Held pose (relative sa camera)")]
        public Vector3 heldPosition = new Vector3(0.24f, -0.22f, 0.5f);
        public Vector3 heldEuler = new Vector3(0, 0, 0);

        public bool IsReloading { get; private set; }

        PlayerItemInteractor owner; Camera cam; float nextFire;
        Vector3 magRest, boltRest;
        static readonly Color LightOn = new Color(2.2f, 0.12f, 0.08f);

        void Awake()
        {
            if (magazine) magRest = magazine.localPosition;
            if (bolt) boltRest = bolt.localPosition;
            RefreshVisuals();
        }

        public void Equip(PlayerItemInteractor who, Camera camera)
        {
            owner = who; cam = camera;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            var pickup = GetComponent<PickupItem>(); if (pickup) pickup.enabled = false;
            transform.SetParent(cam.transform, false);
            transform.localPosition = heldPosition;
            transform.localRotation = Quaternion.Euler(heldEuler);
            transform.localScale = Vector3.one;
            enabled = true;
            RefreshVisuals();
        }

        void Update()
        {
            if (cam == null || IsReloading) return;
            if (Input.GetMouseButtonDown(fireMouseButton)) TryFire();
            if (Input.GetKeyDown(reloadKey)) TryReload();
        }

        void TryFire()
        {
            if (Time.time < nextFire) return;
            if (ammoInMagazine <= 0)
            {
                nextFire = Time.time + 0.3f;
                owner?.ShowMessage("Empty - press R to reload");
                return;
            }
            ammoInMagazine--;
            nextFire = Time.time + fireCooldown;

            Vector3 origin = muzzle ? muzzle.position : cam.transform.position;
            Vector3 target = cam.transform.position + cam.transform.forward * maxAimRange;
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, maxAimRange, ~0, QueryTriggerInteraction.Ignore))
                target = hit.point;
            Vector3 dir = (target - origin).normalized;

            GameObject go = dartPrefab != null ? Instantiate(dartPrefab, origin, Quaternion.LookRotation(dir))
                                               : ItemModelFactory.BuildDart();
            go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(dir));
            var dart = go.GetComponent<TranquilizerDart>();
            if (dart == null) dart = go.AddComponent<TranquilizerDart>();
            dart.Launch(dir, dartSpeed, stunSeconds, owner != null ? owner.transform.root : null);

            RefreshVisuals();
            StartCoroutine(Recoil());
        }

        void TryReload()
        {
            if (IsReloading) return;
            if (ammoInMagazine >= magazineSize) { owner?.ShowMessage("Magazine full"); return; }
            if (owner == null || owner.darts <= 0) { owner?.ShowMessage("No darts left - find more"); return; }
            StartCoroutine(ReloadRoutine());
        }

        IEnumerator ReloadRoutine()
        {
            IsReloading = true;
            Vector3 basePos = heldPosition;
            Quaternion baseRot = Quaternion.Euler(heldEuler);
            Vector3 lowPos = basePos + new Vector3(0.02f, -0.13f, -0.05f);
            Quaternion lowRot = Quaternion.Euler(heldEuler + new Vector3(28, 12, -22));
            float t1 = reloadSeconds * 0.25f, t2 = reloadSeconds * 0.5f, t3 = reloadSeconds * 0.25f;

            owner?.ShowMessage("Reloading...");
            yield return LerpLocal(transform, lowPos, lowRot, t1);

            // ilabas ang magazine
            if (magazine) yield return LerpLocal(magazine, magRest + Vector3.down * 0.09f, magazine.localRotation, t2 * 0.25f);
            int needed = Mathf.Min(magazineSize - ammoInMagazine, owner.darts);
            for (int i = 0; i < needed; i++)
            {
                ammoInMagazine++; owner.darts--;
                RefreshVisuals();
                yield return new WaitForSeconds(t2 * 0.5f / Mathf.Max(1, needed));
            }
            if (magazine) yield return LerpLocal(magazine, magRest, magazine.localRotation, t2 * 0.25f);
            // cycle ng bolt
            if (bolt)
            {
                yield return LerpLocal(bolt, boltRest + Vector3.back * 0.05f, bolt.localRotation, 0.08f);
                yield return LerpLocal(bolt, boltRest, bolt.localRotation, 0.08f);
            }
            yield return LerpLocal(transform, basePos, baseRot, t3);
            IsReloading = false;
            owner?.ShowMessage("Reloaded");
        }

        IEnumerator Recoil()
        {
            Vector3 p = heldPosition;
            Quaternion r = Quaternion.Euler(heldEuler);
            yield return LerpLocal(transform, p + Vector3.back * 0.06f, r * Quaternion.Euler(-6, 0, 0), 0.05f);
            yield return LerpLocal(transform, p, r, 0.15f);
        }

        IEnumerator LerpLocal(Transform tr, Vector3 toPos, Quaternion toRot, float dur)
        {
            Vector3 p0 = tr.localPosition; Quaternion r0 = tr.localRotation;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / dur);
                tr.localPosition = Vector3.Lerp(p0, toPos, k);
                tr.localRotation = Quaternion.Slerp(r0, toRot, k);
                yield return null;
            }
            tr.localPosition = toPos; tr.localRotation = toRot;
        }

        public void RefreshVisuals()
        {
            if (ammoLights != null)
                for (int i = 0; i < ammoLights.Length; i++)
                    ItemMaterials.SetEmission(ammoLights[i], i < ammoInMagazine ? LightOn : Color.black);
            if (chamberDart) chamberDart.gameObject.SetActive(ammoInMagazine > 0);
        }
    }
}