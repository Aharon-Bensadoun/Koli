namespace Koli.Platform;

/// <summary>
/// Detects a lone Alt Gr (or Right Alt) press without an accompanying character key.
/// Alt Gr on European keyboards sends Left Ctrl + Right Alt; typing AltGr+key must not toggle.
/// </summary>
public sealed class AltGrToggleTracker
{
    private const uint VkLControl = 0xA2;
    private const uint VkRControl = 0xA3;
    private const uint VkLMenu = 0xA4;
    private const uint VkRMenu = 0xA5;
    private const uint VkControl = 0x11;
    private const uint VkShift = 0x10;
    private const uint VkLShift = 0xA0;
    private const uint VkRShift = 0xA1;

    private bool _rightAltDown;
    private bool _otherKeyDuringRightAlt;

    public bool? ProcessKey(uint vkCode, bool isKeyDown)
    {
        if (vkCode == VkRMenu)
        {
            if (isKeyDown)
            {
                _rightAltDown = true;
                _otherKeyDuringRightAlt = false;
                return null;
            }

            var shouldToggle = _rightAltDown && !_otherKeyDuringRightAlt;
            _rightAltDown = false;
            return shouldToggle ? true : null;
        }

        if (_rightAltDown && isKeyDown && !IsIgnorableModifier(vkCode))
            _otherKeyDuringRightAlt = true;

        return null;
    }

    internal static bool IsIgnorableModifier(uint vkCode) =>
        vkCode is VkLControl or VkRControl or VkLMenu or VkRMenu or VkControl or VkShift or VkLShift or VkRShift;
}
