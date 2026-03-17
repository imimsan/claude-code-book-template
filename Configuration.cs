using Dalamud.Configuration;

namespace SamplePlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool IsVisible { get; set; } = true;
}
