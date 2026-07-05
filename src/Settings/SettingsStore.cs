using System.Text.Json;
using GitHubWallpaper.GitHub;

namespace GitHubWallpaper.Settings;

/// <summary>
/// Загрузка и сохранение <c>%APPDATA%\GitHubWallpaper\settings.json</c>.
/// PAT не хранится здесь — только Credential Manager.
/// </summary>
internal sealed class SettingsStore
{
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
            return settings ?? CreateDefault();
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
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(AppPaths.SettingsFile, json);
    }

    /// <summary>Список репозиториев из настроек; при пустом списке — репо по умолчанию.</summary>
    public IReadOnlyList<RepoReference> LoadRepositories()
    {
        var settings = Load();
        var list = new List<RepoReference>();

        foreach (var slug in settings.Repositories)
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

    /// <summary>Сохраняет упорядоченный список репозиториев.</summary>
    public void SaveRepositories(IReadOnlyList<RepoReference> repositories)
    {
        ArgumentNullException.ThrowIfNull(repositories);

        var settings = Load();
        settings.Repositories = repositories.Select(repository => repository.Slug).ToList();
        Save(settings);
    }

    private static AppSettings CreateDefault() => new()
    {
        Repositories = [RepoPoller.DefaultRepository.Slug],
    };
}
