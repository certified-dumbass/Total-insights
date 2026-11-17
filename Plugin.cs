using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace LibraryRuntime;

public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths paths, IXmlSerializer serializer) : base(paths, serializer)
    {
        Instance = this;
    }

    internal static Plugin? Instance { get; private set; }

    public override string Name => "Total Insights";
    public override Guid Id => new("114f78ef-4940-495f-a7fb-7a47ab24592a");

    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo { Name = Name, EmbeddedResourcePath = "LibraryRuntime.config.html" };
    }
}

public sealed class PluginConfiguration : BasePluginConfiguration
{
    public string Webhook { get; set; } = "";
    public string Template { get; set; } = "";
}
