#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
#define HI_NEW_INPUT
#endif
using UnityEngine;
#if HI_NEW_INPUT
using UnityEngine.InputSystem;
#endif

namespace HorrorItems
{
    /// <summary>
    /// Single place for all item-system key bindings. Works with the old Input Manager and the new Input System.
    /// If your project already uses these keys (E, R, G, 1, 2), change them here.
    /// </summary>
    public static class ItemInput
    {
#if HI_NEW_INPUT
        static bool Pressed(Key k) { var kb = Keyboard.current; return kb != null && kb[k].wasPressedThisFrame; }
        public static bool Fire => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool Reload => Pressed(Key.R);
        public static bool Interact => Pressed(Key.E);
        public static bool Drop => Pressed(Key.G);
        public static bool Slot1 => Pressed(Key.Digit1);
        public static bool Slot2 => Pressed(Key.Digit2);
#else
        public static bool Fire => Input.GetMouseButtonDown(0);
        public static bool Reload => Input.GetKeyDown(KeyCode.R);
        public static bool Interact => Input.GetKeyDown(KeyCode.E);
        public static bool Drop => Input.GetKeyDown(KeyCode.G);
        public static bool Slot1 => Input.GetKeyDown(KeyCode.Alpha1);
        public static bool Slot2 => Input.GetKeyDown(KeyCode.Alpha2);
#endif
    }
}
