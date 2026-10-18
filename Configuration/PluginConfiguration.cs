using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.PhotoThumbs.Configuration;

/// <summary>
/// Configuración del plugin Photo Thumbnails.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        Enabled = true;
        OnlyWhenMissingCover = true;
        CollageMinPhotos = 2;
        WarmThumbnails = true;
        ThumbSize = 480;
        TargetLibraries = new List<string>();
    }

    /// <summary>
    /// Gets or sets a value indicating whether el plugin está activo.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether solo se generan portadas en
    /// carpetas que no tienen imagen principal.
    /// </summary>
    public bool OnlyWhenMissingCover { get; set; }

    /// <summary>
    /// Gets or sets el mínimo de fotos para generar collage; por debajo se usa
    /// la primera foto como portada directamente.
    /// </summary>
    public int CollageMinPhotos { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether la tarea también pre-genera
    /// miniaturas de cada foto en caché.
    /// </summary>
    public bool WarmThumbnails { get; set; }

    /// <summary>
    /// Gets or sets el tamaño (lado mayor) de las miniaturas pre-generadas.
    /// </summary>
    public int ThumbSize { get; set; }

    /// <summary>
    /// Gets or sets the names of the libraries afectadas. Vacío = ninguna.
    /// </summary>
    public List<string> TargetLibraries { get; set; }
}
