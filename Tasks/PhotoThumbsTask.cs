using Jellyfin.Plugin.PhotoThumbs.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.PhotoThumbs.Tasks;

/// <summary>
/// Tarea programada "Generar portadas y miniaturas de fotos":
/// 1) portadas/collages para carpetas con fotos;
/// 2) miniaturas pre-generadas en caché.
/// </summary>
public class PhotoThumbsTask : IScheduledTask
{
    private readonly FolderCoverService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoThumbsTask"/> class.
    /// </summary>
    /// <param name="service">Servicio de portadas/miniaturas.</param>
    public PhotoThumbsTask(FolderCoverService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public string Name => "Generar portadas y miniaturas de fotos";

    /// <inheritdoc />
    public string Key => "PhotoThumbsGenerate";

    /// <inheritdoc />
    public string Description =>
        "Asigna portadas (collage o primera foto) a las carpetas con fotos de "
        + "las bibliotecas seleccionadas y pre-genera sus miniaturas en caché.";

    /// <inheritdoc />
    public string Category => "Photo Thumbnails";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromHours(24).Ticks
            }
        };
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.Enabled)
        {
            progress.Report(100);
            return;
        }

        var phase = config.WarmThumbnails ? 50.0 : 100.0;
        var coverProgress = new Progress<double>(p => progress.Report(p * phase / 100));
        await _service.GenerateFolderCoversAsync(coverProgress, cancellationToken)
            .ConfigureAwait(false);

        if (config.WarmThumbnails)
        {
            var thumbProgress = new Progress<double>(p => progress.Report(50 + p * 0.5));
            await _service.WarmThumbnailsAsync(thumbProgress, cancellationToken)
                .ConfigureAwait(false);
        }

        progress.Report(100);
    }
}
