using UnityEngine;

namespace HorrorItems
{
    /// <summary>
    /// All gameplay numbers in one asset (created by Tools > Horror Items > Item System Window).
    /// Lives at Assets/Resources/HorrorItems/ItemSystemSettings.asset
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Items/Item System Settings", fileName = "ItemSystemSettings")]
    public class ItemSystemSettings : ScriptableObject
    {
        public const string ResourcePath = "HorrorItems/ItemSystemSettings";

        [Header("Tranquilizer Dart Gun")]
        [Tooltip("How long an enemy stays frozen after a dart hit (seconds). 120 = 2 minutes.")]
        public float dartStunSeconds = 120f;
        [Min(1)] public int magazineSize = 3;
        [Tooltip("Darts already loaded when the gun is first picked up.")]
        [Min(0)] public int startLoadedDarts = 3;
        [Tooltip("Max spare darts the player can carry (found as dart pickups). Used by reload.")]
        [Min(0)] public int maxReserveDarts = 3;
        public float reloadSeconds = 2.4f;
        public float fireCooldown = 0.6f;
        public float dartSpeed = 60f;
        [Range(0f, 1f)] public float dartGravityMultiplier = 0.3f;
        public float dartMaxLifetime = 6f;
        public float dartRadius = 0.03f;
        [Tooltip("How long a dart that hit a wall/floor stays in the world.")]
        public float dartStickSeconds = 20f;
        [Tooltip("Layers the dart can hit (include your Enemy layer + environment).")]
        public LayerMask hitMask = ~0;

        [Header("Trap")]
        [Tooltip("How long an enemy stays frozen when it triggers a trap (seconds). 60 = 1 minute.")]
        public float trapStunSeconds = 60f;
        public float trapArmDelaySeconds = 1f;
        [Tooltip("If true the trap can be picked up again after it releases the enemy.")]
        public bool trapReusable = false;
        public float trapOpenAngle = 75f;
        public float trapCloseSpeed = 900f;
        public float trapOpenSpeed = 60f;
        public float trapDropDistance = 2f;
        [Range(0, 80)] public float trapMaxSlope = 40f;
        [Tooltip("Seconds before a spent (non-reusable) trap is removed. 0 = stays forever.")]
        public float spentTrapLifetime = 0f;

        [Header("Enemy detection")]
        [Tooltip("Enemies with this tag get a StunReceiver automatically (no edits to your enemy prefab needed).")]
        public string enemyTag = "Enemy";
        public bool autoAddStunReceiver = true;

        [Header("Player interaction")]
        public float interactRange = 3f;
        public LayerMask interactMask = ~0;

        static ItemSystemSettings _active;
        public static ItemSystemSettings Active
        {
            get
            {
                if (_active == null)
                {
                    _active = Resources.Load<ItemSystemSettings>(ResourcePath);
                    if (_active == null)
                    {
                        _active = CreateInstance<ItemSystemSettings>();
                        _active.hideFlags = HideFlags.DontSave;
                        Debug.LogWarning("[HorrorItems] No ItemSystemSettings asset found in Resources/HorrorItems - using defaults. " +
                                         "Open Tools > Horror Items > Item System Window to create one.");
                    }
                }
                return _active;
            }
        }
    }
}
