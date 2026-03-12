using System.Text.Json;
using AuroraDesk.Core.Interfaces;
using AuroraDesk.Shared.Helpers;

namespace AuroraDesk.Infrastructure.Config;

public class JsonConfigService : IConfigService
{
    private readonly string _filePath;
    private Dictionary<string, JsonElement> _data = new();

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    public JsonConfigService()
        : this(PathHelper.GetConfigPath()) { }

    public JsonConfigService(string filePath)
    {
        _filePath = filePath;
    }

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            _data = new Dictionary<string, JsonElement>();
            Save();
            return;
        }

        var json = File.ReadAllText(_filePath);
        _data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                ?? new Dictionary<string, JsonElement>();
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(_data, WriteOptions);
        File.WriteAllText(_filePath, json);
    }

    public T? Get<T>(string key)
    {
        if (!_data.TryGetValue(key, out var element))
            return default;

        return element.Deserialize<T>();
    }

    public void Set<T>(string key, T value)
    {
        var serialized = JsonSerializer.Serialize(value);
        _data[key] = JsonDocument.Parse(serialized).RootElement.Clone();
        Save();
    }
}
