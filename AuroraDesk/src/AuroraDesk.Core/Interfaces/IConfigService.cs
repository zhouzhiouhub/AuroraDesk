namespace AuroraDesk.Core.Interfaces;

public interface IConfigService
{
    T? Get<T>(string key);
    void Set<T>(string key, T value);
    void Save();
    void Load();
}
