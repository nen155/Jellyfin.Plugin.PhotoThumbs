using Jellyfin.Plugin.PhotoThumbs.Services;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.PhotoThumbs.Tasks;

/// <summary>
/// Tarea post-escaneo: tras cada escaneo de biblioteca, genera las portadas
/// que falten en carpetas con fotos (sin pre-generar miniaturas, para no
/// alargar el escaneo).
/// </summary>
public class FolderCoverPostScanTask : ILibraryPostScanTask
{
    private readonly FolderCoverService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderCoverPostScanTask"/> class.
    /// </summary>
    /// <param name="service">Servicio de portadas.</param>
    public FolderCoverPostScanTask(FolderCoverService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.Enabled || !config.OnlyWhenMissingCover)
        {
            progress.Report(100);
            return;
        }

        await _service.GenerateFolderCoversAsync(progress, cancellationToken)
            .ConfigureAwait(false);
    }
}
