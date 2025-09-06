using RetroCamera.Utilities;
using Stunlock.Localization;

namespace RetroCamera.Configuration;
internal static class QuipManager
{
    public static IReadOnlyDictionary<int, CommandQuip> CommandQuips => _commandQuips;
    static readonly Dictionary<int, CommandQuip> _commandQuips = [];
    static readonly Dictionary<string, List<CommandQuip>> _quipsByCategory = [];
    const string DEFAULT_CATEGORY = "Default";
    public readonly struct CommandQuip(string name, string command)
    {
        public readonly LocalizationKey NameKey = LocalizationManager.GetLocalizationKey(name);
        public string Name { get; init; } = name;
        public string Command { get; init; } = command;
    }
    public readonly struct Command
    {
        public string Name { get; init; }
        public string InputString { get; init; }
    }
    public static IEnumerable<string> GetCategories() => _quipsByCategory.Keys;
    public static IReadOnlyList<CommandQuip> GetQuipsForCategory(string category) =>
        _quipsByCategory.TryGetValue(category, out var quips) ? quips : Array.Empty<CommandQuip>();
    public static void TryLoadCommands()
    {
        var loaded = Persistence.LoadCommands();

        if (loaded != null)
        {
            List<CommandQuip> defaultCategory = [];
            foreach (var keyValuePair in loaded)
            {
                Command command = keyValuePair.Value;
                var commandQuip = new CommandQuip(command.Name, command.InputString);
                _commandQuips.TryAdd(keyValuePair.Key, commandQuip);
                defaultCategory.Add(commandQuip);
            }

            if (defaultCategory.Count > 0) _quipsByCategory[DEFAULT_CATEGORY] = defaultCategory;
        }
    }
}
