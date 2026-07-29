using System.Text.Json;

namespace RemoteDesktop.Agent;

public class AgentConfig
{
    public string ServerUrl { get; set; } = "ws://192.168.3.44:5080/ws";
    public string PairCode { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private const string ConfigPath = "config.json";

    public static AgentConfig LoadOrCreate()
    {
        if (File.Exists(ConfigPath))
        {
            var json = File.ReadAllText(ConfigPath);
            var loaded = JsonSerializer.Deserialize<AgentConfig>(json);
            if (loaded != null) return loaded;
        }

        var config = new AgentConfig();
        Save(config);
        return config;
    }

    public static void Save(AgentConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
