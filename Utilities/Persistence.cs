using BepInEx;
using RetroCamera.Configuration;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using static RetroCamera.Configuration.OptionsManager;
using static RetroCamera.Configuration.KeybindsManager;
using static RetroCamera.Configuration.QuipManager;

namespace RetroCamera.Utilities;
internal static class Persistence
{
    static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        Converters =
        {
            new MenuOptionJsonConverter()
        }
    };

    static readonly string _directoryPath = Path.Join(Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);

    const string KEYBINDS_KEY = "Keybinds";
    const string OPTIONS_KEY = "Options";
    const string COMMANDS_KEY = "Commands";
    const string COMMAND_CATEGORIES_KEY = "CommandCategories";

    static readonly string _keybindsJson = Path.Combine(_directoryPath, $"{KEYBINDS_KEY}.json");
    static readonly string _settingsJson = Path.Combine(_directoryPath, $"{OPTIONS_KEY}.json");
    static readonly string _commandsJson = Path.Combine(_directoryPath, $"{COMMANDS_KEY}.json");
    static readonly string _commandCategoriesJson = Path.Combine(_directoryPath, $"{COMMAND_CATEGORIES_KEY}.json");

    static readonly Dictionary<string, string> _filePaths = new()
    {
        {KEYBINDS_KEY, _keybindsJson },
        {OPTIONS_KEY, _settingsJson },
        {COMMANDS_KEY, _commandsJson },
        {COMMAND_CATEGORIES_KEY, _commandCategoriesJson }
    };
    public static void SaveKeybinds() => SaveDictionary(Keybinds, KEYBINDS_KEY);
    public static void SaveOptions() => SaveDictionary(Options, OPTIONS_KEY);
    public static void SaveCommands() => SaveDictionary(CommandQuips, COMMANDS_KEY);
    public static void SaveCommandCategories()
    {
        var categories = QuipManager.GetCategories();
        var dtoDictionary = new Dictionary<byte, CommandCategoryDto>();

        foreach (var pair in categories)
        {
            var category = pair.Value;

            var entries = new List<CommandCategoryEntryDto>();
            foreach (var entry in category.Entries)
            {
                var commandQuip = entry.Value;
                var quipDto = new CommandQuipDto
                {
                    Name = commandQuip.Name ?? string.Empty,
                    Command = commandQuip.Command ?? string.Empty
                };

                entries.Add(new CommandCategoryEntryDto
                {
                    Slot = entry.Key,
                    Quip = quipDto
                });
            }

            var quipSlots = category.QuipSlots != null && category.QuipSlots.Count > 0
                ? new List<byte>(category.QuipSlots)
                : new List<byte>();

            dtoDictionary[pair.Key] = new CommandCategoryDto
            {
                Name = category.Name ?? string.Empty,
                QuipSlots = quipSlots,
                Entries = entries
            };
        }

        SaveDictionary(dtoDictionary, COMMAND_CATEGORIES_KEY);
    }
    public static Dictionary<string, Keybinding> LoadKeybinds() => LoadDictionary<string, Keybinding>(KEYBINDS_KEY);
    public static Dictionary<string, MenuOption> LoadOptions() => LoadDictionary<string, MenuOption>(OPTIONS_KEY);
    public static Dictionary<byte, Command> LoadCommands() => LoadDictionary<byte, Command>(COMMANDS_KEY);
    public static Dictionary<byte, CommandCategoryDto> LoadCommandCategories()
    {
        var loaded = LoadDictionary<byte, CommandCategoryDto>(COMMAND_CATEGORIES_KEY);
        return loaded ?? new Dictionary<byte, CommandCategoryDto>();
    }
    static Dictionary<T, U> LoadDictionary<T, U>(string fileKey)
    {
        if (!_filePaths.TryGetValue(fileKey, out string filePath)) return null;

        try
        {
            if (!Directory.Exists(_directoryPath))
                Directory.CreateDirectory(_directoryPath);

            if (!File.Exists(filePath))
            {
                File.Create(filePath).Dispose();

                if (fileKey == COMMANDS_KEY && typeof(T) == typeof(byte) && typeof(U) == typeof(Command))
                {
                    var defaultDict = new Dictionary<byte, Command>();
                    for (int i = 0; i < 8; i++)
                    {
                        defaultDict[(byte)i] = new Command { Name = "", InputString = "" };
                    }

                    File.WriteAllText(filePath, JsonSerializer.Serialize(defaultDict, _jsonOptions));
                    return defaultDict as Dictionary<T, U>;
                }

                return [];
            }

            string fileText = File.ReadAllText(filePath);
            return fileText.IsNullOrWhiteSpace()
                ? []
                : JsonSerializer.Deserialize<Dictionary<T, U>>(fileText, _jsonOptions);
        }
        catch (IOException ex)
        {
            Core.Log.LogWarning($"Failed to deserialize {fileKey} contents: {ex.Message}");
            return null;
        }
    }
    static void SaveDictionary<T, U>(IReadOnlyDictionary<T, U> fileData, string fileKey)
    {
        if (!_filePaths.TryGetValue(fileKey, out string filePath)) return;

        try
        {
            if (!Directory.Exists(_directoryPath))
                Directory.CreateDirectory(_directoryPath);

            string fileText = JsonSerializer.Serialize(fileData, _jsonOptions);
            File.WriteAllText(filePath, fileText);
        }
        catch (IOException ex)
        {
            Core.Log.LogWarning($"Failed to serialize {fileKey} contents: {ex.Message}");
        }
    }
}
internal sealed class CommandCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public List<byte> QuipSlots { get; set; } = new();
    public List<CommandCategoryEntryDto> Entries { get; set; } = new();
}

internal sealed class CommandCategoryEntryDto
{
    public byte Slot { get; set; }
    public CommandQuipDto Quip { get; set; } = new();
}

internal sealed class CommandQuipDto
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
}
internal class MenuOptionJsonConverter : JsonConverter<MenuOption>
{
    const string TYPE_PROPERTY = "OptionType";
    public override MenuOption Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var root = jsonDoc.RootElement;

        if (!root.TryGetProperty(TYPE_PROPERTY, out var typeProp))
            throw new JsonException($"Missing '{TYPE_PROPERTY}' in MenuOption JSON converter!");

        var typeName = typeProp.GetString();

        return typeName switch
        {
            nameof(Toggle) => JsonSerializer.Deserialize<Toggle>(root.GetRawText(), options),
            nameof(Slider) => JsonSerializer.Deserialize<Slider>(root.GetRawText(), options),
            nameof(Dropdown) => JsonSerializer.Deserialize<Dropdown>(root.GetRawText(), options),
            _ => throw new JsonException($"Unknown MenuOption type '{typeName}'")
        };
    }
    public override void Write(Utf8JsonWriter writer, MenuOption value, JsonSerializerOptions options)
    {
        using var jsonDoc = JsonSerializer.SerializeToDocument(value, value.GetType(), options);
        var jsonObj = JsonNode.Parse(jsonDoc.RootElement.GetRawText())!.AsObject();

        jsonObj[TYPE_PROPERTY] = value.GetType().Name;
        jsonObj.WriteTo(writer, options);
    }
}
