namespace RetroCamera.Configuration;
internal static class OptionsManager
{
    public enum OptionItemType
    {
        Toggle,
        Slider,
        Dropdown,
        Divider
    }
    public class OptionEntry(OptionItemType type, string key)
    {
        public OptionItemType Type { get; } = type;
        public string Key { get; } = key;
    }

    static readonly Dictionary<string, MenuOption> _options = [];
    public static IReadOnlyDictionary<string, MenuOption> Options => _options;

    static readonly List<OptionEntry> _orderedEntries = [];
    public static IReadOnlyList<OptionEntry> OrderedEntries => _orderedEntries;
    public static Toggle AddToggle(string name, string description, bool defaultValue)
    {
        var toggle = new Toggle(name, description, defaultValue);
        _options[name] = toggle;
        _orderedEntries.Add(new OptionEntry(OptionItemType.Toggle, name));
        return toggle;
    }
    public static Slider AddSlider(string name, string description, float min, float max, float defaultVal, int decimals = 0, float step = 0)
    {
        var slider = new Slider(name, description, min, max, defaultVal, decimals, step);
        _options[name] = slider;
        _orderedEntries.Add(new OptionEntry(OptionItemType.Slider, name));
        return slider;
    }
    public static Dropdown AddDropdown(string name, string description, int defaultIndex, string[] values)
    {
        var dropdown = new Dropdown(name, description, defaultIndex, values);
        _options[name] = dropdown;
        _orderedEntries.Add(new OptionEntry(OptionItemType.Dropdown, name));
        return dropdown;
    }
    public static void AddDivider(string label)
    {
        _orderedEntries.Add(new(OptionItemType.Divider, label));
    }
    public static bool TryGetOption(OptionEntry entry, out MenuOption option)
    {
        option = null;

        if (!_options.TryGetValue(entry.Key, out var raw))
        {
            Core.Log.LogWarning($"[OptionsManager] Key not found: {entry.Key}");
            return false;
        }

        var expectedType = GetValueType(entry.Type);
        if (expectedType == null)
        {
            Core.Log.LogWarning($"[OptionsManager] Unsupported type for: {entry.Key} ({entry.Type})");
            return false;
        }

        var menuOptionType = typeof(MenuOption<>).MakeGenericType(expectedType);
        if (!menuOptionType.IsInstanceOfType(raw))
        {
            Core.Log.LogWarning($"[OptionsManager] Type mismatch: {entry.Key} (expected: {menuOptionType.Name}, actual: {raw.GetType().Name})");
            return false;
        }

        option = (MenuOption)raw;
        return true;
    }
    static Type GetValueType(OptionItemType type) => type switch
    {
        OptionItemType.Toggle => typeof(bool),
        OptionItemType.Slider => typeof(float),
        OptionItemType.Dropdown => typeof(int),
        _ => null
    };
}