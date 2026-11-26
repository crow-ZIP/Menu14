using UnityEngine.InputSystem;

public static class InputHint
{
    public static string KeyName(InputAction action, string fallback = "E")
    {
        if (action == null) return fallback;
        string group = Gamepad.current != null ? "Gamepad" : "Keyboard&Mouse";
        string key = action.GetBindingDisplayString(InputBinding.MaskByGroup(group),
                                                   InputBinding.DisplayStringOptions.DontIncludeInteractions);
        return string.IsNullOrEmpty(key) ? fallback : key;
    }
}
