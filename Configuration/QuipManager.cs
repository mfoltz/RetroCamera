using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RetroCamera.Utilities;
using Stunlock.Localization;

namespace RetroCamera.Configuration;

internal static class QuipManager
{
    public const string BACK_TO_CATEGORIES_LABEL = "Back to Categories";

    public static LocalizationKey BackToCategoriesLabelKey => _backToCategoriesLabelKey;
    public static IReadOnlyDictionary<byte, CommandQuip> CommandQuips => _readOnlyCommandQuips;

    static readonly LocalizationKey _backToCategoriesLabelKey = LocalizationManager.GetLocalizationKey(BACK_TO_CATEGORIES_LABEL);
    static readonly Dictionary<byte, CommandQuip> _commandQuips = [];
    static readonly ReadOnlyDictionary<byte, CommandQuip> _readOnlyCommandQuips = new(_commandQuips);

    static readonly Dictionary<byte, CommandCategory> _categoriesBySlot = [];
    static readonly ReadOnlyDictionary<byte, CommandCategory> _readOnlyCategories = new(_categoriesBySlot);
    static readonly Dictionary<byte, IReadOnlyList<byte>> _quipSlotsByCategory = [];

    static byte? _activeCategory;

    public static IReadOnlyDictionary<byte, CommandCategory> GetCategories() => _readOnlyCategories;
    public static byte? ActiveCategory => _activeCategory;

    public readonly struct CommandQuip(string name, string command)
    {
        public readonly LocalizationKey NameKey = LocalizationManager.GetLocalizationKey(name);
        public string Name { get; init; } = name;
        public string Command { get; init; } = command;
        public bool IsEmpty => string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Command);
    }

    public readonly struct Command
    {
        public string Name { get; init; }
        public string InputString { get; init; }
    }

    public readonly struct CommandCategory
    {
        public CommandCategory(string name, IEnumerable<byte> quipSlots, IEnumerable<KeyValuePair<byte, CommandQuip>> entries)
        {
            Name = name ?? string.Empty;
            NameKey = LocalizationManager.GetLocalizationKey(Name);

            var slotList = quipSlots != null ? new List<byte>(quipSlots) : new List<byte>();
            QuipSlots = new ReadOnlyCollection<byte>(slotList);

            var entryList = entries != null ? new List<KeyValuePair<byte, CommandQuip>>(entries) : new List<KeyValuePair<byte, CommandQuip>>();
            Entries = new ReadOnlyCollection<KeyValuePair<byte, CommandQuip>>(entryList);
        }

        public string Name { get; init; }
        public LocalizationKey NameKey { get; }
        public IReadOnlyList<byte> QuipSlots { get; }
        public IReadOnlyList<KeyValuePair<byte, CommandQuip>> Entries { get; }
        public bool HasEntries => Entries.Count > 0;
    }

    public static void TryLoadCommands()
    {
        Dictionary<byte, Command> loadedCommands = Persistence.LoadCommands();

        if (loadedCommands == null)
        {
            return;
        }

        Dictionary<byte, CommandCategoryDto> loadedCategories = Persistence.LoadCommandCategories() ?? new Dictionary<byte, CommandCategoryDto>();

        _commandQuips.Clear();

        foreach (KeyValuePair<byte, Command> commandPair in loadedCommands)
        {
            byte slot = commandPair.Key;
            Command command = commandPair.Value;
            _commandQuips[slot] = new CommandQuip(command.Name, command.InputString);
        }

        ClearCategories();

        if (loadedCategories.Count > 0)
        {
            foreach (KeyValuePair<byte, CommandCategoryDto> categoryPair in loadedCategories)
            {
                byte categorySlot = categoryPair.Key;
                CommandCategoryDto categoryDto = categoryPair.Value ?? new CommandCategoryDto();

                List<byte> quipSlots = categoryDto.QuipSlots != null && categoryDto.QuipSlots.Count > 0
                    ? new List<byte>(categoryDto.QuipSlots)
                    : new List<byte>();

                if (categoryDto.Entries != null && categoryDto.Entries.Count > 0)
                {
                    foreach (CommandCategoryEntryDto entry in categoryDto.Entries)
                    {
                        if (entry == null)
                        {
                            continue;
                        }

                        CommandQuipDto quipDto = entry.Quip ?? new CommandQuipDto();
                        byte entrySlot = entry.Slot;
                        _commandQuips[entrySlot] = new CommandQuip(quipDto.Name, quipDto.Command);

                        if (!quipSlots.Contains(entrySlot))
                        {
                            quipSlots.Add(entrySlot);
                        }
                    }
                }

                SetCategory(categorySlot, categoryDto.Name ?? string.Empty, quipSlots);
            }
        }

        RefreshCategories();
    }

    public static CommandCategory SetCategory(byte slot, string name, IEnumerable<byte> quipSlots)
    {
        byte[] orderedSlots = CreateOrderedSlotArray(quipSlots);
        var category = new CommandCategory(name, orderedSlots, BuildCategoryEntries(orderedSlots));
        _categoriesBySlot[slot] = category;
        _quipSlotsByCategory[slot] = category.QuipSlots;

        if (_activeCategory.HasValue && _activeCategory.Value == slot && !category.HasEntries)
        {
            _activeCategory = null;
        }

        return category;
    }

    public static bool RemoveCategory(byte slot)
    {
        var removed = _categoriesBySlot.Remove(slot);
        _quipSlotsByCategory.Remove(slot);

        if (_activeCategory.HasValue && _activeCategory.Value == slot)
        {
            _activeCategory = null;
        }

        return removed;
    }

    public static void ClearCategories()
    {
        _categoriesBySlot.Clear();
        _quipSlotsByCategory.Clear();
        _activeCategory = null;
    }

    public static bool TryGetCategory(byte slot, out CommandCategory category) => _categoriesBySlot.TryGetValue(slot, out category);

    public static IReadOnlyList<KeyValuePair<byte, CommandQuip>> GetQuipsForCategory(byte slot)
    {
        if (_categoriesBySlot.TryGetValue(slot, out var category))
        {
            return category.Entries;
        }

        return Array.Empty<KeyValuePair<byte, CommandQuip>>();
    }

    public static IReadOnlyList<byte> GetQuipSlotsForCategory(byte slot)
    {
        if (_quipSlotsByCategory.TryGetValue(slot, out var slots))
        {
            return slots;
        }

        return Array.Empty<byte>();
    }

    public static bool TryGetQuipSlotForCategory(byte categorySlot, byte quipIndex, out byte quipSlot)
    {
        if (_categoriesBySlot.TryGetValue(categorySlot, out var category)
            && quipIndex < category.QuipSlots.Count)
        {
            quipSlot = category.QuipSlots[quipIndex];
            return true;
        }

        quipSlot = default;
        return false;
    }

    public static bool TryGetQuip(byte slot, out CommandQuip commandQuip)
    {
        if (_activeCategory.HasValue)
        {
            byte activeSlot = _activeCategory.Value;

            if (TryGetQuipSlotForCategory(activeSlot, slot, out var resolvedSlot)
                && _commandQuips.TryGetValue(resolvedSlot, out commandQuip))
            {
                return true;
            }
        }

        return _commandQuips.TryGetValue(slot, out commandQuip);
    }

    public static bool SetActiveCategory(byte slot)
    {
        if (_categoriesBySlot.TryGetValue(slot, out var category) && category.HasEntries)
        {
            _activeCategory = slot;
            return true;
        }

        return false;
    }

    public static void ClearActiveCategory()
    {
        _activeCategory = null;
    }

    public static bool TryGetActiveCategory(out CommandCategory category)
    {
        if (_activeCategory.HasValue && _categoriesBySlot.TryGetValue(_activeCategory.Value, out category))
        {
            return true;
        }

        category = default;
        return false;
    }

    public static void RefreshCategories()
    {
        if (_categoriesBySlot.Count == 0)
        {
            return;
        }

        var snapshot = new List<KeyValuePair<byte, CommandCategory>>(_categoriesBySlot);

        foreach (var pair in snapshot)
        {
            byte slot = pair.Key;
            var existing = pair.Value;
            var refreshed = new CommandCategory(existing.Name, existing.QuipSlots, BuildCategoryEntries(existing.QuipSlots));
            _categoriesBySlot[slot] = refreshed;
            _quipSlotsByCategory[slot] = refreshed.QuipSlots;
        }

        if (_activeCategory.HasValue
            && (!_categoriesBySlot.TryGetValue(_activeCategory.Value, out var active) || !active.HasEntries))
        {
            _activeCategory = null;
        }
    }

    static byte[] CreateOrderedSlotArray(IEnumerable<byte> quipSlots)
    {
        if (quipSlots == null)
        {
            return Array.Empty<byte>();
        }

        var slots = new List<byte>();

        foreach (var quipSlot in quipSlots)
        {
            slots.Add(quipSlot);
        }

        return slots.Count == 0 ? Array.Empty<byte>() : slots.ToArray();
    }

    static IEnumerable<KeyValuePair<byte, CommandQuip>> BuildCategoryEntries(IEnumerable<byte> orderedSlots)
    {
        if (orderedSlots == null)
        {
            yield break;
        }

        foreach (var slot in orderedSlots)
        {
            if (_commandQuips.TryGetValue(slot, out var commandQuip) && !commandQuip.IsEmpty)
            {
                yield return new KeyValuePair<byte, CommandQuip>(slot, commandQuip);
            }
        }
    }
}
