using Jellyfin.Data.Enums;
using Jellyfin.Plugin.PhotoThumbs.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PhotoThumbs.Services;

/// <summary>
/// Lógica compartida: asigna portadas a carpetas/álbumes que contienen fotos
/// (collage con el <see cref="IImageProcessor"/> de Jellyfin o primera foto)
/// y pre-genera miniaturas en caché.
/// </summary>
public class FolderCoverService
{
    private const int CollagePhotoCount = 4;
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly ILibraryManager _libraryManager;
    private readonly IImageProcessor _imageProcessor;
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<FolderCoverService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderCoverService"/> class.
    /// </summary>
    /// <param name="libraryManager">Gestor de bibliotecas.</param>
    /// <param name="imageProcessor">Procesador de imágenes.</param>
    /// <param name="applicationPaths">Rutas de la aplicación.</param>
    /// <param name="logger">Logger.</param>
    public FolderCoverService(
        ILibraryManager libraryManager,
        IImageProcessor imageProcessor,
        IApplicationPaths applicationPaths,
        ILogger<FolderCoverService> logger)
    {
        _libraryManager = libraryManager;
        _imageProcessor = imageProcessor;
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    /// <summary>
    /// Recorre las carpetas con fotos de las bibliotecas seleccionadas y les
    /// asigna portada si procede. Devuelve cuántas carpetas se actualizaron.
    /// </summary>
    public async Task<int> GenerateFolderCoversAsync(
        IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        var roots = ResolveTargetRoots(config);
        if (config is null || roots.Length == 0)
        {
            progress.Report(100);
            return 0;
        }

        var folders = _libraryManager
            .GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Folder, BaseItemKind.PhotoAlbum },
                Recursive = true
            })
            .Where(item => item is Folder
                && !item.IsVirtualItem
                && !string.IsNullOrEmpty(item.Path)
                && roots.Any(root => IsPathInside(item.Path, root)))
            .Cast<Folder>()
            .ToList();

        var updated = 0;
        var collageDir = Path.Combine(_applicationPaths.DataPath, "photocovers");
        Directory.CreateDirectory(collageDir);

        for (var i = 0; i < folders.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = folders[i];
            try
            {
                if (await ProcessFolderAsync(folder, config, collageDir, cancellationToken)
                        .ConfigureAwait(false))
                {
                    updated++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "No se pudo generar portada para {Path}", folder.Path);
            }

            progress.Report((i + 1) * 100.0 / folders.Count);
        }

        _logger.LogInformation(
            "PhotoThumbs: {Updated} portadas generadas sobre {Total} carpetas.",
            updated, folders.Count);
        return updated;
    }

    /// <summary>
    /// Pre-genera en caché la miniatura Primary de cada foto de las
    /// bibliotecas seleccionadas, al tamaño configurado.
    /// </summary>
    public async Task<int> WarmThumbnailsAsync(
        IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        var roots = ResolveTargetRoots(config);
        if (config is null || roots.Length == 0)
        {
            progress.Report(100);
            return 0;
        }

        var photos = _libraryManager
            .GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Photo },
                Recursive = true
            })
            .Where(item => !item.IsVirtualItem
                && !string.IsNullOrEmpty(item.Path)
                && roots.Any(root => IsPathInside(item.Path, root)))
            .ToList();

        var size = Math.Max(64, config.ThumbSize);
        var formats = _imageProcessor.GetSupportedImageOutputFormats();
        var done = 0;

        for (var i = 0; i < photos.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var photo = photos[i];
            try
            {
                var imageInfo = photo.GetImageInfo(ImageType.Primary, 0);
                if (imageInfo is not null)
                {
                    await _imageProcessor.ProcessImage(new ImageProcessingOptions
                    {
                        Item = photo,
                        ItemId = photo.Id,
                        Image = imageInfo,
                        ImageIndex = 0,
                        MaxWidth = size,
                        MaxHeight = size,
                        Quality = 85,
                        SupportedOutputFormats = formats,
                        RequiresAutoOrientation = true
                    }).ConfigureAwait(false);
                    done++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "No se pudo pre-generar miniatura de {Path}", photo.Path);
            }

            progress.Report((i + 1) * 100.0 / photos.Count);
        }

        _logger.LogInformation(
            "PhotoThumbs: {Done} miniaturas pre-generadas de {Total} fotos.",
            done, photos.Count);
        return done;
    }

    private async Task<bool> ProcessFolderAsync(
        Folder folder, PluginConfiguration config, string collageDir,
        CancellationToken cancellationToken)
    {
        if (config.OnlyWhenMissingCover
            && folder.GetImageInfo(ImageType.Primary, 0) is { Path: not null and not "" })
        {
            return false;
        }

        var photoPaths = folder
            .GetRecursiveChildren(child => child is Photo)
            .OrderBy(p => p.DateCreated)
            .Select(p => p.Path)
            .Where(File.Exists)
            .Take(CollagePhotoCount)
            .ToArray();

        if (photoPaths.Length == 0)
        {
            return false;
        }

        string? coverPath = null;
        if (photoPaths.Length >= Math.Max(2, config.CollageMinPhotos)
            && _imageProcessor.SupportsImageCollageCreation)
        {
            var output = Path.Combine(collageDir, $"{folder.Id:N}.jpg");
            // CreateImageCollage es síncrono en Jellyfin 12: devuelve void.
            _imageProcessor.CreateImageCollage(
                new ImageCollageOptions
                {
                    InputPaths = photoPaths,
                    OutputPath = output,
                    Width = 600,
                    Height = 600
                },
                folder.GetTopParent()?.Name);
            coverPath = File.Exists(output) ? output : null;
        }
        else
        {
            coverPath = photoPaths[0];
        }

        if (string.IsNullOrEmpty(coverPath))
        {
            return false;
        }

        folder.SetImagePath(ImageType.Primary, coverPath);
        await _libraryManager.UpdateItemAsync(
            folder, folder.GetParent(), ItemUpdateType.ImageUpdate, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private string[] ResolveTargetRoots(PluginConfiguration? config)
    {
        if (config is null || config.TargetLibraries.Count == 0)
        {
            return Array.Empty<string>();
        }

        return _libraryManager.GetVirtualFolders()
            .Where(vf => config.TargetLibraries.Contains(vf.Name, StringComparer.OrdinalIgnoreCase))
            .SelectMany(vf => vf.Locations)
            .Where(loc => !string.IsNullOrWhiteSpace(loc))
            .ToArray();
    }

    private static bool IsPathInside(string path, string root)
    {
        if (!path.StartsWith(root, PathComparison))
        {
            return false;
        }

        return path.Length == root.Length || path[root.Length] is '/' or '\\';
    }
}
