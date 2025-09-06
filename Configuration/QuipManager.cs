using RetroCamera.Utilities;
using Stunlock.Localization;
using System.Linq;

namespace RetroCamera.Configuration;
internal static class QuipManager
{
    public static IReadOnlyDictionary<int, CommandQuip> CommandQuips => _commandQuips;
    public static IReadOnlyDictionary<string, List<Command>> CommandCategories =>
        _quipsByCategory.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(q => new Command { Name = q.Name, InputString = q.Command }).ToList());

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
        var categoryData = Persistence.LoadCommandCategories();
        if (categoryData != null)
        {
            foreach (var pair in categoryData)
            {
                _quipsByCategory[pair.Key] = pair.Value
                    .Select(cmd => new CommandQuip(cmd.Name, cmd.InputString))
                    .ToList();
            }
        }

        var loaded = Persistence.LoadCommands();
        if (loaded != null)
        {
            foreach (var keyValuePair in loaded)
            {
                Command command = keyValuePair.Value;
                var commandQuip = new CommandQuip(command.Name, command.InputString);
                _commandQuips[keyValuePair.Key] = commandQuip;

                if (!_quipsByCategory.TryGetValue(DEFAULT_CATEGORY, out var list))
                {
                    list = [];
                    _quipsByCategory[DEFAULT_CATEGORY] = list;
                }

                list.Add(commandQuip);
            }
        }
    }
}
