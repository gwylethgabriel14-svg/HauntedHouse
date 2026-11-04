using UnityEngine;
using UnityEngine.AI;

namespace HauntedItems
{
    public enum ItemKind { TranquilizerGun, DartPack, Trap }

    public class PickupItem : MonoBehaviour
    {
        public ItemKind kind;
        public int amount = 1;
        public string displayName = "Item";
        public string PromptText => "Pick up " + displayName;
    }

    public static class ItemUtil
    {
        public static ItemSpawner FindSpawner()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<ItemSpawner>();
#else
            return Object.FindObjectOfType<ItemSpawner>();
#endif
        }
    }
}