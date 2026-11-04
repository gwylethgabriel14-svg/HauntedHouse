using System;
using System.Collections.Generic;
using UnityEngine;

namespace HauntedItems
{
    public class PlayerItemInteractor : MonoBehaviour
    {
        [Header("Setup")]
        public Camera playerCamera;
        public float reach = 2.6f;
        public KeyCode pickupKey = KeyCode.E;
        public KeyCode dropTrapKey = KeyCode.G;
        public GameObject trapPrefab;           // optional, kukunin sa ItemSpawner kung walang laman
        public ItemSpawner settings;
        public bool showPrompts = true;

        [Header("Inventory")]
        public int darts = 0; public int maxDarts = 9;
        public int traps = 0; public int maxTraps = 3;
        public TranquilizerGun gun;

        PickupItem focused; string message; float messageUntil;
        GUIStyle center, right;

        void Start()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null) playerCamera = Camera.main;
            if (settings == null) settings = ItemUtil.FindSpawner();
            if (trapPrefab == null && settings != null) trapPrefab = settings.trapPrefab;
        }

        public void ShowMessage(string m, float seconds = 2f) { message = m; messageUntil = Time.time + seconds; }

        void Update()
        {
            if (playerCamera == null) return;
            focused = FindFocus();
            if (focused != null && Input.GetKeyDown(pickupKey)) TryPickup(focused);
            if (Input.GetKeyDown(dropTrapKey)) TryPlaceTrap();
        }

        PickupItem FindFocus()
        {
            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            var hits = Physics.RaycastAll(ray, reach, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                var p = h.collider.GetComponentInParent<PickupItem>();
                if (p != null && p.enabled) return p;
                if (!h.collider.isTrigger) break; // pader o solid na humaharang
            }
            return null;
        }

        void TryPickup(PickupItem p)
        {
            switch (p.kind)
            {
                case ItemKind.TranquilizerGun:
                    if (gun != null) { ShowMessage("You already have a dart gun"); return; }
                    var g = p.GetComponent<TranquilizerGun>();
                    if (g == null) return;
                    gun = g; g.Equip(this, playerCamera);
                    ShowMessage("Picked up Tranquilizer Gun  (LMB fire, R reload)", 3f);
                    break;
                case ItemKind.DartPack:
                    int space = maxDarts - darts;
                    if (space <= 0) { ShowMessage("Can't carry more darts"); return; }
                    int add = Mathf.Min(space, p.amount);
                    darts += add; p.amount -= add;
                    if (p.amount <= 0) Destroy(p.gameObject);
                    ShowMessage("+" + add + " tranquilizer darts");
                    break;
                case ItemKind.Trap:
                    if (traps >= maxTraps) { ShowMessage("Can't carry more traps"); return; }
                    traps++;
                    Destroy(p.gameObject);
                    ShowMessage("Picked up Bear Trap  (" + dropTrapKey + " to place)");
                    break;
            }
        }

        void TryPlaceTrap()
        {
            if (traps <= 0) { ShowMessage("No traps to place"); return; }
            Transform cam = playerCamera.transform;
            Vector3 spot = cam.position + cam.forward * 1.6f;
            if (Physics.Raycast(cam.position, cam.forward, out var wall, 1.6f, ~0, QueryTriggerInteraction.Ignore))
                spot = wall.point - cam.forward * 0.5f;
            if (!Physics.Raycast(spot + Vector3.up, Vector3.down, out var floor, 3f, ~0, QueryTriggerInteraction.Ignore)
                || floor.normal.y < 0.7f)
            { ShowMessage("Can't place a trap here"); return; }

            GameObject go = trapPrefab != null ? Instantiate(trapPrefab) : ItemModelFactory.BuildTrap();
            float yaw = cam.eulerAngles.y;
            go.transform.SetPositionAndRotation(floor.point, Quaternion.Euler(0, yaw, 0));
            var b = ItemModelFactory.GetBounds(go);
            go.transform.position += Vector3.up * (floor.point.y + 0.004f - b.min.y);

            var trap = go.GetComponent<TrapItem>();
            if (trap != null)
            {
                if (settings != null) { trap.stunDuration = settings.trapStunSeconds; trap.armDelay = settings.trapArmDelay; }
                trap.armOnStart = false;
                trap.Place();
            }
            traps--;
            ShowMessage("Trap placed - arming...");
        }

        void OnGUI()
        {
            if (!showPrompts) return;
            if (center == null)
            {
                center = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                center.normal.textColor = Color.white;
                right = new GUIStyle(center) { fontSize = 16, alignment = TextAnchor.MiddleRight };
            }
            if (focused != null)
                GUI.Label(new Rect(0, Screen.height * 0.62f, Screen.width, 30), "[" + pickupKey + "] " + focused.PromptText, center);
            if (Time.time < messageUntil)
                GUI.Label(new Rect(0, Screen.height * 0.68f, Screen.width, 30), message, center);
            string ammo = gun != null ? gun.ammoInMagazine + "/" + gun.magazineSize : "-";
            GUI.Label(new Rect(Screen.width - 360, Screen.height - 60, 340, 30),
                "Darts " + ammo + "   Reserve " + darts + "   Traps " + traps, right);
        }
    }
}