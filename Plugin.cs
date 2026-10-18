using Jellyfin.Plugin.PhotoThumbs.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.PhotoThumbs;

/// <summary>
/// Plugin principal: genera portadas/collages para carpetas que contienen
/// fotos y pre-genera miniaturas de las fotos para una vista previa rápida.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Rutas de la aplicación.</param>
    /// <param name="xmlSerializer">Serializador XML de configuración.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Photo Thumbnails";

    /// <inheritdoc />
    public override string Description =>
        "Genera portadas (collage o primera foto) para carpetas con fotos y "
        + "pre-genera miniaturas para una vista previa más rápida.";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("c62e1a4b-5f7d-4e9a-8b3c-6d2f9a1e7b05");

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "PhotoThumbs",
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html",
                EnableInMainMenu = true,
                MenuSection = "server",
                MenuIcon = "collections"
            }
        };
    }
}
