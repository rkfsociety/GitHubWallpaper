using System.Text.Json;
using GitHubWallpaper.GitHub;

namespace GitHubWallpaper.Settings;

/// <summary>
/// Загрузка и сохранение <c>%APPDATA%\GitHubWallpaper\settings.json</c>.
/// PAT не хранится здесь — только Credential Manager.
/// </summary>
internal sealed class SettingsStore
{
    private const int DefaultGridColumns = 3;
    private const int DefaultGridRows = 2;
    private const int MinGridSize = 1;
    private const int MaxGridSize = 6;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Читает файл настроек или возвращает значения по умолчанию.</summary>
    public AppSettings Load()
    {
        var path = AppPaths.SettingsFile;
        if (!File.Exists(path))
        {
            return CreateDefault();
        }

        try
        {
            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return Normalize(settings ?? CreateDefault());
        }
        catch (JsonException)
        {
            return CreateDefault();
        }
        catch (IOException)
        {
            return CreateDefault();
        }
    }

    /// <summary>Сохраняет настройки на диск.</summary>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(AppPaths.AppData);
        var normalized = Normalize(settings);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        File.WriteAllText(AppPaths.SettingsFile, json);
    }

    /// <summary>Список репозиториев из ячеек сетки; при пустом списке — репо по умолчанию.</summary>
    public IReadOnlyList<RepoReference> LoadRepositories()
    {
        var settings = Load();
        var list = new List<RepoReference>();

        foreach (var slug in EnumerateRepositorySlugs(settings))
        {
            if (!RepoUrlParser.TryParse(slug, out var reference))
            {
                continue;
            }

            if (list.Any(existing =>
                    existing.Slug.Equals(reference.Slug, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            list.Add(reference);
        }

        if (list.Count == 0)
        {
            list.Add(RepoPoller.DefaultRepository);
        }

        return list;
    }

    /// <summary>Сохраняет упорядоченный список репозиториев (миграция в слоты сетки).</summary>
    public void SaveRepositories(IReadOnlyList<RepoReference> repositories)
    {
        ArgumentNullException.ThrowIfNull(repositories);

        var settings = Load();
        settings.Repositories = repositories.Select(repository => repository.Slug).ToList();
        settings.RepositorySlots = BuildSlotsFromRepositories(settings, settings.Repositories);
        Save(settings);
    }

    /// <summary>Сохраняет размер сетки и расположение репозиториев по ячейкам.</summary>
    public void SaveGridLayout(int columns, int rows, IReadOnlyList<string> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);

        var settings = Load();
        settings.GridColumns = ClampGridSize(columns, DefaultGridColumns);
        settings.GridRows = ClampGridSize(rows, DefaultGridRows);
        settings.RepositorySlots = NormalizeSlots(slots, SlotCapacity(settings.GridColumns, settings.GridRows));
        settings.Repositories = EnumerateRepositorySlugs(settings).ToList();
        Save(settings);
    }

    private static AppSettings CreateDefault() => new()
    {
        Repositories = [RepoPoller.DefaultRepository.Slug],
        GridColumns = DefaultGridColumns,
        GridRows = DefaultGridRows,
        RepositorySlots = [RepoPoller.DefaultRepository.Slug],
    };

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.GridColumns = ClampGridSize(settings.GridColumns, DefaultGridColumns);
        settings.GridRows = ClampGridSize(settings.GridRows, DefaultGridRows);

        if (settings.RepositorySlots.Count == 0 && settings.Repositories.Count > 0)
        {
            settings.RepositorySlots = settings.Repositories.ToList();
        }

        settings.RepositorySlots = NormalizeSlots(
            settings.RepositorySlots,
            SlotCapacity(settings.GridColumns, settings.GridRows));

        var slugs = EnumerateRepositorySlugs(settings).ToList();
        if (slugs.Count == 0)
        {
            slugs.Add(RepoPoller.DefaultRepository.Slug);
            settings.RepositorySlots = [RepoPoller.DefaultRepository.Slug];
        }

        settings.Repositories = slugs;
        return settings;
    }

    private static IEnumerable<string> EnumerateRepositorySlugs(AppSettings settings)
    {
        foreach (var slug in settings.RepositorySlots)
        {
            if (!string.IsNullOrWhiteSpace(slug))
            {
                yield return slug.Trim();
            }
        }
    }

    private static List<string> BuildSlotsFromRepositories(AppSettings settings, IReadOnlyList<string> repositories)
    {
        var capacity = SlotCapacity(settings.GridColumns, settings.GridRows);
        var slots = NormalizeSlots(settings.RepositorySlots, capacity);
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < slots.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(slots[index]))
            {
                known.Add(slots[index]);
            }
        }

        var queue = repositories
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Select(slug => slug.Trim())
            .Where(slug => known.Add(slug))
            .ToList();

        for (var index = 0; index < slots.Count && queue.Count > 0; index++)
        {
            if (!string.IsNullOrWhiteSpace(slots[index]))
            {
                continue;
            }

            slots[index] = queue[0];
            queue.RemoveAt(0);
        }

        return slots;
    }

    private static List<string> NormalizeSlots(IReadOnlyList<string> slots, int capacity)
    {
        var normalized = new List<string>(capacity);

        for (var index = 0; index < capacity; index++)
        {
            var slug = index < slots.Count ? slots[index]?.Trim() : null;
            normalized.Add(string.IsNullOrEmpty(slug) ? string.Empty : slug);
        }

        return normalized;
    }

    private static int SlotCapacity(int columns, int rows) =>
        ClampGridSize(columns, DefaultGridColumns) * ClampGridSize(rows, DefaultGridRows);

    private static int ClampGridSize(int value, int fallback) =>
        value is < MinGridSize or > MaxGridSize ? fallback : value;
}
