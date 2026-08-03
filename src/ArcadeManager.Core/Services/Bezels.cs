using ArcadeManager.Core.Actions.Bezels;
using ArcadeManager.Core.Exceptions;
using ArcadeManager.Core.Infrastructure;
using ArcadeManager.Core.Infrastructure.Interfaces;
using ArcadeManager.Core.Models.Bezels;
using ArcadeManager.Core.Models.Zip;
using ArcadeManager.Core.Services.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace ArcadeManager.Core.Services;

public class Bezels(IFileSystem fs, IBezelImageProcessor bezelImageProcessor) : IBezels
{
    // TODO: create short-lived memory cache when reading image file so as to not read it multiple times per loop

    // ######################################## CHECKER

    private readonly ConcurrentBag<Config> configs = [];
    private int errorsNb = 0;
    private int fixesNb = 0;
    private int createdNb = 0;

    /// <summary>
    /// Checks the Retroarch configuration files.
    /// </summary>
    /// <param name="options">The options.</param>
    public async Task CheckRetroArchConfigFiles(CheckAction options, IMessageHandler messageHandler)
    {
        // check roms configs
        var romConfigs = fs.FilesGetList(options.RomsConfigFolder, "*.cfg");

        messageHandler.ProgressInit("Check RetroArch config files");

        await Parallel.ForEachAsync(romConfigs, async (f, cancellationToken) =>
        {
            var fileName = fs.FileName(f);
            var game = fileName.Replace(".zip.cfg", "").Replace(".cfg", "");
            var romConfEntry = AddConfigEntry(fileName, null, null);

            var cfgContent = await fs.FileReadAsync(f);
            var overlayPath = GetCfgData(cfgContent, "input_overlay");

            messageHandler.Progress($"Processing config {f}");

            if (string.IsNullOrWhiteSpace(overlayPath))
            {
                LogError(options.ErrorFile, game, $"rom has no input_overlay parameter");
            }
            else
            {
                // make sure a Windows path is converted to Unix under *nix, and vice versa
                var overlayFileName = fs.FileName(NormalizePath(overlayPath));

                // check that there is an matching overlay file at the expected localtion
                if (!fs.FileExists(fs.PathJoin(options.OverlaysConfigFolder, overlayFileName)))
                {
                    LogError(options.ErrorFile, game, $"rom points to a non-existing overlay: {overlayFileName}");
                }
                else
                {
                    romConfEntry.Overlay = overlayFileName;
                }

                // check that the path in the rom config is valid
                if (!string.IsNullOrEmpty(options.InputOverlayConfigPathInRomConfig))
                {
                    var separator = options.InputOverlayConfigPathInRomConfig.EndsWith('/') ? "" : "/";
                    var overlayShouldBe = $"{options.InputOverlayConfigPathInRomConfig}{separator}{overlayFileName}";
                    if (!overlayPath.Equals(overlayShouldBe, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (options.AutoFix)
                        {
                            LogFix(options.ErrorFile, game, "fixing overlay path in rom config");
                            cfgContent = SetCfgData(cfgContent, "input_overlay", overlayShouldBe);

                            await fs.FileWriteAsync(f, cfgContent);
                        }
                        else
                        {
                            LogError(options.ErrorFile, game, $"rom has a wrong overlay path: {overlayPath}");
                        }
                    }
                }
            }
        });

        // check overlay config files
        var configFiles = fs.FilesGetList(options.OverlaysConfigFolder, "*.cfg");
        await Parallel.ForEachAsync(configFiles, async (f, cancellationToken) =>
        {
            var fileName = fs.FileName(f);
            var game = fileName.Replace(".cfg", "");

            var cfgContent = await fs.FileReadAsync(f);
            var overlayFileName = GetCfgData(cfgContent, "overlay0_overlay");

            messageHandler.Progress($"Processing config {f}");

            // check that the overlay is used
            var cfgEntry = GetConfigEntry(c => c.Overlay != null && c.Overlay.Equals(fileName, StringComparison.InvariantCultureIgnoreCase));
            if (cfgEntry == null)
            {
                if (options.AutoFix)
                {
                    // create a rom
                    var romFile = fileName.Replace(".cfg", ".zip.cfg");
                    var dest = fs.PathJoin(options.RomsConfigFolder, romFile);
                    if (fs.FileExists(dest))
                    {
                        LogError(options.ErrorFile, game, $"overlay matches rom file but is not used by it: {romFile}");
                    }
                    else
                    {
                        LogFix(options.ErrorFile, game, $"creating rom config file for unused overlay at {dest}");

                        var imgFileName = fs.PathJoin(options.OverlaysConfigFolder, overlayFileName);
                        if (fs.FileExists(imgFileName))
                        {
                            // get bounds
                            var img = await fs.FileReadBinaryAsync(imgFileName);
                            var bounds = bezelImageProcessor.FindScreen(img, options.Margin);

                            await CreateConfig(options.TemplateRom, game, dest, bounds, options.TargetResolutionBounds);

                            cfgEntry = AddConfigEntry(romFile, fileName, overlayFileName);
                        }
                        else
                        {
                            LogError(options.ErrorFile, game, $"overlay points to a non-existing image: {imgFileName}");
                        }
                    }
                }
                else
                {
                    LogError(options.ErrorFile, game, $"overlay is not used by any game");
                }
            }

            // check that the image exists
            if (!fs.FileExists(fs.PathJoin(options.OverlaysConfigFolder, overlayFileName)))
            {
                LogError(options.ErrorFile, game, $"overlay points to a non-existing image: {overlayFileName}");
            }
            else
            {
                if (cfgEntry == null)
                {
                    AddConfigEntry(null, fileName, overlayFileName);
                }
            }
        });

        // check that all images have an associated overlay config
        var images = fs.FilesGetList(options.OverlaysConfigFolder, "*.png");
        await Parallel.ForEachAsync(images, async (f, cancellationToken) =>
        {
            var fileName = fs.FileName(f);
            var game = fileName.Replace(".png", "");

            messageHandler.Progress($"Processing image {f}");

            // check that the image is used by an overlay
            var cfgEntry = GetConfigEntry(c => c.Image != null && c.Image.Equals(fileName, StringComparison.InvariantCultureIgnoreCase));
            if (cfgEntry == null)
            {
                if (options.AutoFix)
                {
                    var cfgFilesName = $"{game}.cfg";
                    var dest = fs.PathJoin(options.OverlaysConfigFolder, cfgFilesName);
                    if (fs.FileExists(dest))
                    {
                        LogError(options.ErrorFile, game, $"trying to create overlay {dest} but file already exists");
                    }
                    else
                    {
                        LogFix(options.ErrorFile, game, $"Creating overlay config for orphan image at {dest}");
                        await CreateConfig(options.TemplateOverlay, game, dest, null, options.TargetResolutionBounds);

                        var romDest = fs.PathJoin(options.RomsConfigFolder, cfgFilesName);
                        LogFix(options.ErrorFile, game, $"Creating rom config for orphan image at {romDest}");

                        // create the config
                        var bounds = bezelImageProcessor.FindScreen(await fs.FileReadBinaryAsync(f), options.Margin);
                        await CreateConfig(options.TemplateRom, game, romDest, bounds, options.TargetResolutionBounds);

                        AddConfigEntry(cfgFilesName, cfgFilesName, fileName);
                    }
                }
                else
                {
                    LogError(options.ErrorFile, game, $"image is not used by any overlay: {fileName}");
                }
            }

            // check that image is not too large
            var imgSize = bezelImageProcessor.GetSize(f);
            if (imgSize.Width > options.TargetResolutionBounds.Width || imgSize.Height > options.TargetResolutionBounds.Height)
            {
                if (options.AutoFix)
                {
                    LogFix(options.ErrorFile, game, $"resizing image (previous size: {imgSize.Width}x{imgSize.Height})");
                    bezelImageProcessor.Resize(f, (int)options.TargetResolutionBounds.Width, (int)options.TargetResolutionBounds.Height);
                }
                else
                {
                    LogError(options.ErrorFile, game, $"image has wrong size: {imgSize.Width}x{imgSize.Height}");
                }
            }
        });

        // get list of roms configs, again (in case some have been created)
        romConfigs = fs.FilesGetList(options.RomsConfigFolder, "*.cfg");
        await Parallel.ForEachAsync(romConfigs, async (f, cancellationToken) =>
        {
            var fileName = fs.FileName(f);
            var game = fileName.Replace(".cfg", "").Replace(".zip", "");

            messageHandler.Progress($"Processing config {f}");

            // get overlay file name
            var romContent = await fs.FileReadAsync(f);
            var overlayFileName = GetCfgData(romContent, "input_overlay");
            if (string.IsNullOrWhiteSpace(overlayFileName))
            {
                LogError(options.ErrorFile, game, $"fixing screen: rom config doesn't have an input_overlay");
                return;
            }

            var overlayFile = fs.FileName(NormalizePath(overlayFileName));
            var overlayPath = fs.PathJoin(options.OverlaysConfigFolder, overlayFile);

            if (!fs.FileExists(overlayPath))
            {
                LogError(options.ErrorFile, game, $"fixing screen: overlay file does not exist: {overlayPath}");
                return;
            }

            var overlayContent = await fs.FileReadAsync(overlayPath);
            var imageFile = GetCfgData(overlayContent, "overlay0_overlay");
            var imagePath = fs.PathJoin(options.OverlaysConfigFolder, imageFile);

            if (!fs.FileExists(imagePath))
            {
                LogError(options.ErrorFile, game, $"fixing screen: image file does not exist: {imagePath}");
                return;
            }

            var imageContent = await fs.FileReadBinaryAsync(imagePath);

            // get bounds
            var boundsInImage = bezelImageProcessor.FindScreen(imageContent, 0);
            var boundsInConf = GetBoundsFromConfig(romContent);

            // make sure the bounds match
            if (!CheckCoordinate(boundsInImage.X, boundsInConf.X, options.ErrorMargin)
                || !CheckCoordinate(boundsInImage.Y, boundsInConf.Y, options.ErrorMargin)
                || !CheckCoordinate(boundsInImage.Width, boundsInConf.Width, options.ErrorMargin * 2)
                || !CheckCoordinate(boundsInImage.Height, boundsInConf.Height, options.ErrorMargin * 2))
            {
                boundsInImage = boundsInImage.ApplyMargin(options.Margin);

                if (!string.IsNullOrWhiteSpace(options.OutputDebug))
                {
                    // output debug whether fixing or not
                    if (boundsInConf.Width > 0 && boundsInConf.Height > 0)
                    {
                        bezelImageProcessor.DebugDraw($"{game}_conf", options.OutputDebug, imagePath, boundsInConf, options.TargetResolutionBounds);
                    }

                    bezelImageProcessor.DebugDraw($"{game}_image", options.OutputDebug, imagePath, boundsInImage, null);
                }

                // fix the image
                if (options.AutoFix)
                {
                    LogFix(options.ErrorFile, game, "Fixing screen position in config");
                    await SetBounds(f, game, boundsInImage, options.TargetResolutionBounds);
                }
                else
                {
                    LogError(options.ErrorFile, game, $"image has wrong coordinates in config");
                }
            }
        });

        if (errorsNb > 0 || fixesNb > 0)
        {
            // TODO: use errorsNb, fixesNb, options.ErrorFile
        }
    }

    private Config AddConfigEntry(string rom, string overlay, string image)
    {
        var entry = new Config { Rom = rom, Overlay = overlay, Image = image };
        configs.Add(entry);

        return entry;
    }

    private static bool CheckCoordinate(double a, double b, int margin)
    {
        return Math.Abs(a - b) <= margin;
    }

    private Config GetConfigEntry(Func<Config, bool> predicate)
    {
        return configs.FirstOrDefault(predicate);
    }

    /// <summary>
    /// A rom/overlay/image configuration
    /// </summary>
    private sealed class Config
    {
        public string Image;
        public string Overlay;
        public string Rom;
    }

    // ######################################## CONVERTER

    /// <summary>
    /// Starts the import from MAME to Retroarch
    /// </summary>
    public async Task ConvertMameToRetroarch(MameToRaAction options)
    {
        var fsEntries = fs.FilesGetList(options.Source, "*.*");

        await Parallel.ForEachAsync(fsEntries, async (f, cancellationToken) =>
        {
            await ProcessMameFile(f, options);
        });
    }

    /// <summary>
    /// Starts the import from Retroarch to MAME
    /// </summary>
    /// <param name="options"></param>
    public async Task ConvertRetroarchToMame(RaToMameAction options)
    {
        // get files to process
        var romFiles = fs.FilesGetList(options.SourceRoms, "*.zip.cfg");

        await Parallel.ForEachAsync(romFiles, async (f, cancellationToken) =>
        {
            await ProcessRetroarchFile(f, options);
        });
    }

    /// <summary>
    /// Extracts and deserializes the LAY and CFG files
    /// </summary>
    /// <param name="game">The game name.</param>
    /// <param name="zipFile">The zip file path.</param>
    /// <param name="cfgFile">The CFG file path.</param>
    /// <param name="tmpFolder">The temporary folder path.</param>
    /// <returns>The deserialized LAY and CFG files</returns>
    /// <exception cref="ArcadeManager.Core.Exceptions.LayFileException">
    /// Unable to find a view in the LAY file
    /// </exception>
    private async Task<(MameLayFile lay, MameCfgFile cfg, byte[] bezel)> ExtractFiles(string game, string zipFile, string cfgFile, MameToRaAction options)
    {
        MameLayFile lay = null;
        MameCfgFile cfg = null;
        byte[] bezel = null;

        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Ignore
        };

        Log($"{game} Extracting files from archive {zipFile}");

        // extract files
        using (var archive = fs.OpenZipRead(zipFile))
        {
            // get layout file
            var layEntry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith("default.lay", StringComparison.InvariantCultureIgnoreCase))
                ?? throw new Exceptions.LayFileException($"Unable to find default.lay file in {zipFile}");

            using (var layStream = await layEntry.OpenAsync())
            {
                using var reader = XmlReader.Create(layStream, settings);
                lay = Serializer.DeserializeXml<MameLayFile>(reader);
            }

            // check that LAY is useful
            if (lay.Views.Length == 0) { throw new Exceptions.LayFileException("Unable to find a view in the LAY file"); }

            // get associated bezel
            var view = GetView(lay, options.UseFirstView);
            var bezelFileNameInLay = MameGetBezelFile(lay, view);
            if (!string.IsNullOrEmpty(bezelFileNameInLay))
            {
                // sometimes the bezel file name in LAY doesn't have the same case as the actual file
                var bezelFileNameInZip = fs.FindFileInZip(archive, bezelFileNameInLay);
                var bezelEntry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(bezelFileNameInZip, StringComparison.InvariantCultureIgnoreCase))
                    ?? throw new Exceptions.BezelNotFoundException($"Unable to find bezel file {bezelFileNameInZip} in {zipFile}");

                bezel = await bezelEntry.GetContentAsync();
            }
        }

        cfg = await MameGetConfigFile(cfgFile, game);

        return (lay, cfg, bezel);
    }

    /// <summary>
    /// Fill a template config with the specified values
    /// </summary>
    /// <param name="configPath">The path to the config file to fill</param>
    /// <param name="game">The game name</param>
    /// <param name="position">The position of the image</param>
    /// <param name="resolution">The target resolution</param>
    private async Task FillTemplate(string configPath, string game, Bounds position, Bounds resolution)
    {
        var content = await fs.FileReadAsync(configPath);

        content = FillTemplateContent(content, game, position, resolution);

        await fs.FileWriteAsync(configPath, content);
    }

    /// <summary>
    /// Processes a file
    /// </summary>
    /// <param name="zipFile">The file to process</param>
    /// <param name="options">The options</param>
    private async Task ProcessMameFile(string fsEntry, MameToRaAction options)
    {
        var isFolder = fs.IsDirectory(fsEntry);
        var entryName = isFolder ? fs.DirectoryName(fsEntry) : fs.FileName(fsEntry);

        // don't process files that are not zip
        if (!isFolder && !entryName.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
        {
            return;
        }

        var game = entryName.Replace(".zip", "", StringComparison.InvariantCultureIgnoreCase);

        try
        {
            var cfgFile = string.IsNullOrEmpty(options.SourceConfigs) ? string.Empty : fs.PathJoin(options.SourceConfigs, $"{game}.cfg");

            Log($"{game} processing start");

            var (lay, cfg, bezel) = isFolder
                ? await MameReadFiles(game, fsEntry, cfgFile, options)
                : await ExtractFiles(game, fsEntry, cfgFile, options);

            // extracts the data from the MAME files
            var mameBezel = MameGetBezel(options, lay, cfg);

            Log($"{game} image: {mameBezel.BezelFileName}");
            Log($"{game} source screen: {mameBezel.SourceScreenPosition}");
            Log($"{game} screen offset: {mameBezel.Offset}");

            // resize the bezel image
            Log($"{game} resizing image");
            bezel = bezelImageProcessor.Resize(
                bezel,
                (int)options.TargetResolutionBounds.Width,
                (int)options.TargetResolutionBounds.Height);

            Log($"{game} getting target screen position");
            Bounds newPosition;
            if (options.ScanBezelForScreenCoordinates)
            {
                // scan image for transparent pixels
                newPosition = bezelImageProcessor.FindScreen(bezel, options.Margin);
            }
            else
            {
                // convert from LAY and CFG
                newPosition = ApplyOffset(
                                    mameBezel.SourceScreenPosition,
                                    mameBezel.Offset,
                                    mameBezel.SourceResolution,
                                    options.TargetResolutionBounds);
            }

            Log($"{game} target screen: {newPosition}");

            if (newPosition.Width <= 0 || newPosition.Height <= 0)
            {
                LogError(options.ErrorFile, game, $"Width/height of screen are invalid: {newPosition}");
                return;
            }

            // get bezel image
            var outputImage = fs.PathJoin(options.OutputOverlays, $"{game}.png");
            if (options.Overwrite && fs.FileExists(outputImage)) { fs.FileDelete(outputImage); }
            if (options.Overwrite || !fs.FileExists(outputImage))
            {
                await fs.FileWriteBinaryAsync(outputImage, bezel);
            }

            // debug: draw target position
            bezelImageProcessor.DebugDraw(game, options.OutputDebug, outputImage, newPosition, options.TargetResolutionBounds);

            Log($"{game} creating configs");

            // create game config files
            var outputGameCfg = fs.PathJoin(options.OutputRoms, $"{game}.zip.cfg");
            fs.FileCopy(options.TemplateGameCfg, outputGameCfg, options.Overwrite);
            await FillTemplate(outputGameCfg, game, newPosition, options.TargetResolutionBounds);

            // create overlay config files
            var outputOverlayCfg = fs.PathJoin(options.OutputOverlays, $"{game}.cfg");
            fs.FileCopy(options.TemplateOverlayCfg, outputOverlayCfg, options.Overwrite);
            await FillTemplate(outputOverlayCfg, game, newPosition, options.TargetResolutionBounds);

            Log($"{game} processing done");
        }
        catch (Exception ex)
        {
            LogError(options.ErrorFile, game, ex.Message);
        }
    }

    /// <summary>
    /// Processes the Retroarch file.
    /// </summary>
    /// <param name="romFile">The rom config file.</param>
    /// <param name="options">The options.</param>
    private async Task ProcessRetroarchFile(string romFile, RaToMameAction options)
    {
        var romFileName = fs.FileName(romFile);
        var game = romFileName.Replace(".zip.cfg", "");

        Log($"{game} processing start");

        try
        {
            var target = fs.PathJoin(options.Output, game);

            // get RA processor
            var processor = await GetRetroArchConfig(romFile, options);

            Log($"{game} image: {processor.OverlayImageFileName}");
            Log($"{game} source screen: {processor.SourceScreenPosition}");

            Bounds newPosition;
            if (options.ScanBezelForScreenCoordinates)
            {
                newPosition = bezelImageProcessor.FindScreen(await fs.FileReadBinaryAsync(processor.OverlayImagePath), options.Margin);
            }
            else
            {
                // convert from LAY and CFG
                newPosition = processor.SourceScreenPosition;
            }

            Log($"{game} target screen: {newPosition}");

            // create destination folder
            if (options.Overwrite && fs.DirectoryExists(target)) { fs.DirectoryDelete(target, true); }
            if (!fs.DirectoryExists(target)) { fs.DirectoryCreate(target); }

            // copy overlay image
            fs.FileCopy(processor.OverlayImagePath, fs.PathJoin(target, processor.OverlayImageFileName), options.Overwrite);

            // resize the bezel image
            Log($"{game} processing image");
            bezelImageProcessor.Resize(
                processor.OverlayImagePath,
                (int)processor.SourceResolution.Width,
                (int)processor.SourceResolution.Height);

            // debug: draw target position
            bezelImageProcessor.DebugDraw(game, options.OutputDebug, processor.OverlayImagePath, newPosition, options.TargetResolutionBounds);

            Log($"{game} creating configs");

            // create lay file
            var outputLay = fs.PathJoin(target, "default.lay");
            fs.FileCopy(options.Template, outputLay, options.Overwrite);
            await FillTemplate(outputLay, game, newPosition, processor.SourceResolution);

            // zip overlay
            if (options.Zip)
            {
                Log($"{game} zipping file");
                var targetZip = fs.PathJoin(options.Output, $"{game}.zip");
                if (fs.FileExists(targetZip)) { fs.FileDelete(targetZip); }
                fs.CompressFolderContent(target, targetZip);
                fs.DirectoryDelete(target, true);
            }

            Log($"{game} processing done");
        }
        catch (Exception ex)
        {
            LogError(options.ErrorFile, game, ex.Message);
        }
    }

    /// <summary>
    /// Applies the specified offset to the specified bounds
    /// </summary>
    /// <param name="sourcePosition">The source screen position</param>
    /// <param name="offset">The offset to apply</param>
    /// <param name="sourceResolution">The source resolution</param>
    /// <param name="targetResolution">The target resolution</param>
    /// <returns>The new bounds</returns>
    private static Bounds ApplyOffset(Bounds sourcePosition, Offset offset, Bounds sourceResolution, Bounds targetResolution)
    {
        var newPos = sourcePosition.Clone();

        if (offset != null)
        {
            // multiply w/h by stretch = get target screen size, centered => NEW DIMENSIONS AT
            // SOURCE RESOLUTION
            newPos.Width *= offset.HStretch;
            newPos.Height *= offset.VStretch;

            // compute new base x/y (top/left): x = cx - (w / 2)
            newPos.X = sourcePosition.Center.X - (newPos.Width / 2);
            newPos.Y = sourcePosition.Center.Y - (newPos.Height / 2);

            // apply offset: x = x + ((hres / w * h) * hoffset) ; y = y + (vres * voffset) =>
            // NEW POSITION at source resolution
            if (offset.HOffset != 0)
            {
                if (newPos.Orientation == Orientation.Horizontal)
                {
                    newPos.X += (sourcePosition.Width / newPos.Width * newPos.Height) * offset.HOffset;
                }
                else
                {
                    newPos.X += sourcePosition.Width * offset.HOffset;
                }
            }

            if (offset.VOffset != 0)
            {
                if (newPos.Orientation == Orientation.Horizontal)
                {
                    newPos.Y += sourcePosition.Height * offset.VOffset;
                }
                else
                {
                    newPos.Y += (sourcePosition.Height / newPos.Height * newPos.Width) * offset.VOffset;
                }
            }
        }

        // apply target resolution => NEW COORDINATES AT TARGET RESOLUTION
        newPos.X *= targetResolution.Width / sourceResolution.Width;
        newPos.Y *= targetResolution.Height / sourceResolution.Height;
        newPos.Width *= targetResolution.Width / sourceResolution.Width;
        newPos.Height *= targetResolution.Height / sourceResolution.Height;

        return newPos;
    }

    // ######################################## FILEUTILS

    /// <summary>
    /// Fills the specified config content template with the specified infos
    /// </summary>
    /// <param name="content">The content to fill</param>
    /// <param name="game">The game name</param>
    /// <param name="position">The screen position</param>
    /// <param name="resolution">The target resolution</param>
    /// <returns>The filled content</returns>
    private static string FillTemplateContent(string content, string game, Bounds position, Bounds resolution)
    {
        content = content.Replace("{{game}}", game);

        if (position != null)
        {
            content = content.Replace("{{width}}", Math.Round(position.Width, 0).ToString())
                .Replace("{{height}}", Math.Round(position.Height, 0).ToString())
                .Replace("{{x}}", Math.Round(position.X, 0).ToString())
                .Replace("{{y}}", Math.Round(position.Y, 0).ToString())
                .Replace("{{orientation}}", position.Orientation.ToString().ToLower());
        }

        if (resolution != null)
        {
            content = content
                .Replace("{{width_res}}", Math.Round(resolution.Width, 0).ToString())
                .Replace("{{height_res}}", Math.Round(resolution.Height, 0).ToString());
        }

        return content;
    }

    /// <summary>
    /// Normalizes a path so it's understandable by System.IO.Path
    /// </summary>
    /// <param name="path">The path to normalize</param>
    /// <returns>The normalized path</returns>
    private static string NormalizePath(string path)
    {
        // RetroArch has this weird syntax where ":\" means "at the RA root"
        path = path.StartsWith(':') ? path[1..] : path;

        // Windows handles *nix slashes fine, the opposite is not true
        path = path.Replace("\\", "/");

        return path;
    }

    /// <summary>
    /// Reads the files in the specified folder
    /// </summary>
    /// <param name="game">The game name</param>
    /// <param name="folder">The folder to read</param>
    /// <param name="cfgFile">The config file</param>
    /// <param name="options">The options</param>
    /// <returns>The parsed files</returns>
    private async Task<(MameLayFile lay, MameCfgFile cfg, byte[] bezel)> MameReadFiles(string game, string folder, string cfgFile, MameToRaAction options)
    {
        byte[] bezel = null;

        Log($"{game} Reading files from folder {folder}");

        // get layout and bezel
        var layFiles = fs.FilesGetList(folder, "default.lay");
        if (layFiles == null || !layFiles.Any()) { throw new PathNotFoundException($"Unable to find a default.lay file in {folder}"); }
        var firstFileContent = await fs.FileReadAsync(layFiles.First());
        MameLayFile lay = Serializer.Deserialize<MameLayFile>(firstFileContent);

        // check that LAY is useful
        if (lay.Views.Length == 0) { throw new LayFileException("Unable to find a view in the LAY file"); }
        var view = GetView(lay, options.UseFirstView);
        var bezelFileNameInLay = MameGetBezelFile(lay, view);
        if (!string.IsNullOrEmpty(bezelFileNameInLay))
        {
            var bezelFilePath = fs.FilesGetList(folder, bezelFileNameInLay);
            if (bezelFilePath == null || !bezelFilePath.Any()) { throw new PathNotFoundException($"Unable to find the bezel file {bezelFileNameInLay}"); }
            bezel = await fs.FileReadBinaryAsync(bezelFilePath.First());
        }

        // get config file
        MameCfgFile cfg = await MameGetConfigFile(cfgFile, game);

        return (lay, cfg, bezel);
    }

    /// <summary>
    /// Parses the specified config file
    /// </summary>
    /// <param name="cfgFile">The config file</param>
    /// <param name="game">The game name</param>
    /// <returns>The parsed config file</returns>
    private async Task<MameCfgFile> MameGetConfigFile(string cfgFile, string game)
    {
        // parse the config file if it exists
        if (!string.IsNullOrEmpty(cfgFile) && fs.FileExists(cfgFile))
        {
            Log($"{game} MAME config file exists");

            var fileContent = await fs.FileReadAsync(cfgFile);
            return Serializer.Deserialize<MameCfgFile>(fileContent);
        }
        else
        {
            Log($"{game} doesn't have a MAME config file");
            return null;
        }
    }

    // ######################################## GENERATOR

    /// <summary>
    /// Generates RA config files based on overlay images
    /// </summary>
    /// <param name="options">The options</param>
    public async Task GenerateRetroArchConfigs(GenerateAction options)
    {
        Log("########## GENERATING ROM CONFIGS ##########");

        var images = fs.FilesGetList(options.ImagesFolder, "*.png");
        await Parallel.ForEachAsync(images, async (f, cancellationToken) =>
        {
            var fileName = fs.FileName(f);
            var game = fileName.Replace(".png", "");

            Log($"{game} generating config");

            // resize
            bezelImageProcessor.Resize(f, (int)options.TargetResolutionBounds.Width, (int)options.TargetResolutionBounds.Height);

            // get data from image
            var bounds = bezelImageProcessor.FindScreen(await fs.FileReadBinaryAsync(f), options.Margin);

            // generate config
            var config = fs.PathJoin(options.ImagesFolder, $"{game}.cfg");
            if (!options.Overwrite && fs.FileExists(config))
            {
                LogError(options.ErrorFile, game, $"config file already exists: {config}");
            }
            else
            {
                fs.FileDelete(config);
                await CreateConfig(options.TemplateOverlay, game, config, bounds, options.TargetResolutionBounds);
                LogCreate(options.ErrorFile, game, $"created config: {config}");
            }

            // generate rom
            var rom = fs.PathJoin(options.RomsFolder, $"{game}.zip.cfg");
            if (!options.Overwrite && fs.FileExists(rom))
            {
                LogError(options.ErrorFile, game, $"rom config file already exists: {rom}");
            }
            else
            {
                fs.FileDelete(rom);
                await CreateConfig(options.TemplateRom, game, rom, bounds, options.TargetResolutionBounds);
                LogCreate(options.ErrorFile, game, $"created rom config file: {rom}");
            }

            // debug
            if (!string.IsNullOrWhiteSpace(options.OutputDebug))
            {
                bezelImageProcessor.DebugDraw($"{game}_image", options.OutputDebug, f, bounds, options.TargetResolutionBounds);
            }

            Log($"{game} done");
        });

        Log("########## DONE ##########");

        if (createdNb > 0 || errorsNb > 0)
        {
            Log("");

            Log($"- {errorsNb} errors");
            Log($"- {createdNb} game files created");

            Log($"Check {options.ErrorFile} to see the details");
        }
    }

    // ######################################## INITIALIZER

    /// <summary>
    /// Initializes the check.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <returns>Whether the initialization has been successful</returns>
    private bool InitCheck(CheckAction options)
    {
        bool err = false;

        // check input folders
        if (!fs.DirectoryExists(options.RomsConfigFolder))
        {
            Log($"Unable to find rom directory {options.RomsConfigFolder}");
            err = true;
        }

        if (!fs.DirectoryExists(options.OverlaysConfigFolder))
        {
            Log($"Unable to find overlays directory {options.OverlaysConfigFolder}");
            err = true;
        }

        // check auto-fix
        if (options.AutoFix)
        {
            if (string.IsNullOrEmpty(options.TemplateRom) || !fs.FileExists(options.TemplateRom))
            {
                Log($"Unable to find rom config template {options.TemplateRom}");
                err = true;
            }

            if (string.IsNullOrEmpty(options.TemplateOverlay) || !fs.FileExists(options.TemplateOverlay))
            {
                Log($"Unable to find overlay config template {options.TemplateOverlay}");
                err = true;
            }
        }

        return !err;
    }

    /// <summary>
    /// Initializes common parameters
    /// </summary>
    /// <param name="options">The common parameters</param>
    private void InitCommon(BaseAction options)
    {
        if (!string.IsNullOrEmpty(options.OutputDebug) && !fs.DirectoryExists(options.OutputDebug))
        {
            fs.DirectoryCreate(options.OutputDebug);
        }

        if (!string.IsNullOrEmpty(options.ErrorFile) && fs.FileExists(options.ErrorFile))
        {
            fs.FileDelete(options.ErrorFile);
        }
    }

    /// <summary>
    /// Initializes the generation
    /// </summary>
    /// <param name="options">The generation options</param>
    /// <returns>Whether the initialization has been successful</returns>
    private bool InitGenerate(GenerateAction options)
    {
        bool err = false;

        //check input folder
        if (!fs.DirectoryExists(options.ImagesFolder))
        {
            Log($"Unable to find image folder {options.ImagesFolder}");
            err = true;
        }

        // check output folders
        if (!fs.DirectoryExists(options.RomsFolder))
        {
            Log($"Unable to find rom directory {options.RomsFolder}");
            err = true;
        }

        // check templates
        if (string.IsNullOrEmpty(options.TemplateRom) || !fs.FileExists(options.TemplateRom))
        {
            Log($"Unable to find rom config template {options.TemplateRom}");
            err = true;
        }

        if (string.IsNullOrEmpty(options.TemplateOverlay) || !fs.FileExists(options.TemplateOverlay))
        {
            Log($"Unable to find overlay config template {options.TemplateOverlay}");
            err = true;
        }

        return !err;
    }

    /// <summary>
    /// Initializes the import from MAME to Retroarch
    /// </summary>
    /// <param name="options">The options</param>
    /// <returns>Whether the initialization has been successful</returns>
    private bool InitMameToRa(MameToRaAction options)
    {
        var err = false;
        // check that input folder exists
        if (!fs.DirectoryExists(options.Source))
        {
            Log($"Unable to find directory {options.Source}");
            err = true;
        }

        // create folders
        if (!fs.DirectoryExists(options.OutputRoms))
        {
            fs.DirectoryCreate(options.OutputRoms);
        }

        if (!fs.DirectoryExists(options.OutputOverlays))
        {
            fs.DirectoryCreate(options.OutputOverlays);
        }

        // check templates
        if (!fs.FileExists(options.TemplateGameCfg))
        {
            Log($"Unable to find game config template {options.TemplateGameCfg}");
            err = true;
        }

        if (!fs.FileExists(options.TemplateOverlayCfg))
        {
            Log($"Unable to find overlay config template {options.TemplateOverlayCfg}");
            err = true;
        }

        return !err;
    }

    /// <summary>
    /// Initializes the import from Retroarch to MAME
    /// </summary>
    /// <param name="options">The options</param>
    /// <returns>Whether the initialization has been successful</returns>
    private bool InitRaToMame(RaToMameAction options)
    {
        var err = false;

        // check that input folder exists
        if (!fs.DirectoryExists(options.SourceConfigs))
        {
            Log($"Unable to find directory {options.SourceConfigs}");
            err = true;
        }

        if (!fs.DirectoryExists(options.SourceRoms))
        {
            Log($"Unable to find directory {options.SourceRoms}");
            err = true;
        }

        // create folders
        if (!fs.DirectoryExists(options.Output))
        {
            fs.DirectoryCreate(options.Output);
        }

        if (!string.IsNullOrEmpty(options.OutputDebug) && !fs.DirectoryExists(options.OutputDebug))
        {
            fs.DirectoryCreate(options.OutputDebug);
        }

        // check templates
        if (!fs.FileExists(options.Template))
        {
            Log($"Unable to find LAY template {options.Template}");
            err = true;
        }

        return !err;
    }

    // ######################################## MAMEPROCESSOR

    /// <summary>
    /// Factory for the MAME processor
    /// </summary>
    /// <param name="options">The options</param>
    /// <param name="lay">The LAY file</param>
    /// <param name="cfg">The CFG file</param>
    /// <returns>The processor</returns>
    private static MameBezel MameGetBezel(MameToRaAction options, MameLayFile lay, MameCfgFile cfg)
    {
        // extract source data
        var view = GetView(lay, options.UseFirstView);
        var sourceRes = GetSourceResolution(view);
        var bezelFile = MameGetBezelFile(lay, view);
        var sourceScreenCoordinates = GetSourceScreenCoordinates(view);
        var off = GetOffset(cfg);

        return new MameBezel(off, sourceRes, sourceScreenCoordinates, bezelFile);
    }

    /// <summary>
    /// Gets the bezel file name from the LAY file and the view
    /// </summary>
    /// <param name="lay">The LAY file</param>
    /// <param name="view">The processed view</param>
    /// <returns>The bezel file name</returns>
    private static string MameGetBezelFile(MameLayFile lay, MameLayFile.View view)
    {
        var bezelElementName = view.Bezels[0].ElementName;

        var element = lay.Elements.FirstOrDefault(e => e.Name == bezelElementName)
            ?? throw new Exceptions.LayFileException($"Unable to find an <element> with name {bezelElementName} in LAY file");

        if (element.Images == null || element.Images.Length == 0) { throw new Exceptions.LayFileException($"No images inside <element> {bezelElementName} in LAY file"); }

        return element.Images[0].File;
    }

    /// <summary>
    /// Gets the source resolution for the specified view
    /// </summary>
    /// <param name="view">The view to process</param>
    /// <returns>The source resolution</returns>
    public static Bounds GetSourceResolution(MameLayFile.View view)
    {
        if (view.Bezels.Length == 0) { throw new Exceptions.BezelNotFoundException($"Unable to find a <bezel> for the <view> {view.Name}"); }

        var bezelOfView = view.Bezels.FirstOrDefault(b => b.Bounds.X == 0 && b.Bounds.Y == 0)
            ?? throw new Exceptions.CoordinatesException($"No <bezel> inside <view> {view.Name} has coordinates starting at (0,0): I don't know how to convert");
        return bezelOfView.Bounds;
    }

    /// <summary>
    /// Gets the source screen coordinates
    /// </summary>
    /// <returns></returns>
    public static Bounds GetSourceScreenCoordinates(MameLayFile.View view)
    {
        if (view.Screens == null || view.Screens.Length == 0) { throw new Exceptions.LayFileException($"No screen found in view {view.Name}"); }
        if (view.Screens.Length > 1) { throw new Exceptions.LayFileException($"Unable to automatically process a multi-screen machine (RetroArch doesn't support it)"); }

        var screen = view.Screens[0].Bounds;

        // base bounds: screen bounds
        var bounds = screen.Clone();

        // add overlay and backdrop positions to find the largest possible box
        if (view.Overlays != null && view.Overlays.Length != 0)
        {
            foreach (var o in view.Overlays)
            {
                bounds = AddToBounds(bounds, o.Bounds);
            }
        }

        if (view.Backdrops != null && view.Backdrops.Length != 0)
        {
            foreach (var b in view.Backdrops)
            {
                bounds = AddToBounds(bounds, b.Bounds);
            }
        }

        return bounds;
    }

    /// <summary>
    /// Gets the processed view
    /// </summary>
    /// <param name="lay">The LAY file</param>
    /// <param name="useFirstView">Whether to automatically use the first found view</param>
    /// <returns>The processed view</returns>
    public static MameLayFile.View GetView(MameLayFile lay, bool useFirstView)
    {
        MameLayFile.View view;
        if (lay.Views.Length > 1 && !useFirstView)
        {
            var views = new List<string>();
            for (int i = 0; i < lay.Views.Length; i++)
            {
                views.Add($"{i}: {lay.Views[i].Name}");
            }

            int viewIndex = Ask("Please choose which bezel you want", [.. views]);
            view = lay.Views[viewIndex];
        }
        else
        {
            view = lay.Views[0];
        }

        return view;
    }

    private static int Ask(string v, string[] strings)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Adds bounds to get the largest possible box
    /// </summary>
    /// <param name="a">The first bound</param>
    /// <param name="b">The second bound</param>
    /// <returns>The largest possible bounds</returns>
    private static Bounds AddToBounds(Bounds a, Bounds b)
    {
        var result = a.Clone();

        // b is further to the left
        if (a.X > b.X)
        {
            var diff = a.X - b.X;
            result.X -= diff;
            result.Width += diff;
        }

        // b is wider
        if (a.Width < b.Width)
        {
            result.Width += b.Width - a.Width;
        }

        // b is further to the top
        if (a.Y > b.Y)
        {
            var diff = a.Y - b.Y;
            result.Y -= diff;
            result.Height += diff;
        }

        // b is taller
        if (a.Height < b.Height)
        {
            result.Height += b.Height - a.Height;
        }

        return result;
    }

    /// <summary>
    /// Gets the offset from the config file
    /// </summary>
    /// <param name="cfg">The CFG file</param>
    /// <returns>The offset</returns>
    private static Offset GetOffset(MameCfgFile cfg)
    {
        var screen = cfg?.SystemConfig?.VideoConfig?.VideoScreen;
        float epsilon = 0.00001f;

        // check that at least an offset has a value
        if (screen != null &&
                (Math.Abs(screen.HOffset - Offset.DEFAULT_OFFSET) < epsilon
                    || Math.Abs(screen.HStretch - Offset.DEFAULT_STRETCH) < epsilon
                    || Math.Abs(screen.VOffset - Offset.DEFAULT_OFFSET) < epsilon
                    || Math.Abs(screen.VStretch - Offset.DEFAULT_STRETCH) < epsilon))
        {
            return new Offset
            {
                HOffset = screen.HOffset,
                VOffset = screen.VOffset,
                HStretch = screen.HStretch == 0 ? Offset.DEFAULT_STRETCH : screen.HStretch,
                VStretch = screen.VStretch == 0 ? Offset.DEFAULT_STRETCH : screen.VStretch
            };
        }

        return null;
    }

    // ######################################## RETROARCHPROCESSOR

    /// <summary>
    /// Gets the bounds written in a config file
    /// </summary>
    /// <param name="fileContent">The content of the file</param>
    /// <returns>The rom file content</returns>
    private static Bounds GetBoundsFromConfig(string fileContent)
    {
        if (int.TryParse(GetCfgData(fileContent, "custom_viewport_width"), out int width)
            && int.TryParse(GetCfgData(fileContent, "custom_viewport_height"), out int height)
            && int.TryParse(GetCfgData(fileContent, "custom_viewport_x"), out int x)
            && int.TryParse(GetCfgData(fileContent, "custom_viewport_y"), out int y))
        {
            return new Bounds
            {
                X = x,
                Y = y,
                Width = width,
                Height = height
            };
        }

        return new Bounds { X = 0, Y = 0, Width = 0, Height = 0 };
    }

    /// <summary>
    /// Gets data from the specified config file.
    /// </summary>
    /// <param name="fileContent">The content of the file.</param>
    /// <param name="key">The key to look for.</param>
    /// <returns>The config value</returns>
    private static string GetCfgData(string fileContent, string key)
    {
        var match = Regex.Match(fileContent, BuildCfgRegex(key), RegexOptions.Multiline, TimeSpan.FromSeconds(1));
        if (match.Success && match.Captures.Count != 0)
        {
            return match.Groups[1].Value.Trim();
        }

        return null;
    }

    private async Task<RetroArchBezel> GetRetroArchConfig(string romFile, RaToMameAction options)
    {
        // get rom file content
        var romFileContent = await fs.FileReadAsync(romFile);

        // get overlay content
        var overlayCfgFileSourcePath = GetCfgData(romFileContent, "input_overlay");
        var overlayCfgFileName = fs.FileName(overlayCfgFileSourcePath);
        var overlayCfgFilePath = fs.PathJoin(options.SourceConfigs, overlayCfgFileName);
        var overlayCfgFileContent = await fs.FileReadAsync(overlayCfgFilePath);

        // extract data from configs
        var overlayImageFileName = GetCfgData(overlayCfgFileContent, "overlay0_overlay");
        var screenBounds = GetBoundsFromConfig(romFileContent);

        var xres = GetCfgData(romFileContent, "video_fullscreen_x");
        var yres = GetCfgData(romFileContent, "video_fullscreen_y");

        var resolution = new Bounds
        {
            X = 0,
            Y = 0,
            Width = int.Parse(xres ?? options.TargetResolutionBounds.Width.ToString()),
            Height = int.Parse(yres ?? options.TargetResolutionBounds.Height.ToString())
        };

        return new RetroArchBezel
        {
            OverlayImageFileName = overlayImageFileName,
            OverlayImagePath = fs.PathJoin(options.SourceConfigs, overlayImageFileName),
            SourceScreenPosition = screenBounds,
            SourceResolution = resolution
        };
    }

    /// <summary>
    /// Sets the bounds in the specified config
    /// </summary>
    /// <param name="filePath">The path to the file to set</param>
    /// <param name="bounds">The bounds to set</param>
    /// <returns>The modified config</returns>
    public async Task SetBounds(string filePath, string game, Bounds bounds, Bounds resolution)
    {
        var fileContent = await fs.FileReadAsync(filePath);

        fileContent = SetCfgData(fileContent, "custom_viewport_width", bounds.Width.ToString());
        fileContent = SetCfgData(fileContent, "custom_viewport_height", bounds.Height.ToString());
        fileContent = SetCfgData(fileContent, "custom_viewport_x", bounds.X.ToString());
        fileContent = SetCfgData(fileContent, "custom_viewport_y", bounds.Y.ToString());

        // fill placeholders
        fileContent = FillTemplateContent(fileContent, game, bounds, resolution);

        await fs.FileWriteAsync(filePath, fileContent);
    }

    /// <summary>
    /// Sets a value in the specified config file
    /// </summary>
    /// <param name="fileContent">The contents of the file</param>
    /// <param name="key">The key to set</param>
    /// <returns>The modified content</returns>
    public static string SetCfgData(string fileContent, string key, string value)
    {
        var r = BuildCfgRegex(key);
        var v = $"{key} = {value}";

        // if it exists: replace value
        if (Regex.IsMatch(fileContent, r, RegexOptions.Multiline, TimeSpan.FromSeconds(1)))
        {
            return Regex.Replace(fileContent, r, v, RegexOptions.Multiline, TimeSpan.FromSeconds(1));
        }

        // if it doesn't exist: add value
        return $"{fileContent}\n{v}";
    }

    /// <summary>
    /// Creates a config file
    /// </summary>
    /// <param name="templatePath">The path to the template</param>
    /// <param name="game">The game name</param>
    /// <param name="dest">The destination path</param>
    /// <param name="bounds">The screen bounds</param>
    /// <param name="resolution">The target resolution</param>
    public async Task CreateConfig(string templatePath, string game, string dest, Bounds bounds, Bounds resolution)
    {
        fs.FileCopy(templatePath, dest, false);
        await FillTemplate(dest, game, null, null);

        if (bounds != null)
        {
            await SetBounds(dest, game, bounds, resolution);
        }
    }

    private static string BuildCfgRegex(string key)
    {
        /// searched value looks like:
        /// key = "value"
        /// with or without spaces, with or without quotes, with or without trailing spaces
        return $"^{key}\\s*=\\s?\"?([^\"\\n]*)\"?\\s*$";
    }

    // ######################################## OUTPUT METHODS TO REWRITE

    private void LogCreate(string errorFile, string game, string v)
    {
        throw new NotImplementedException();
    }

    private void LogFix(string errorFile, string game, string v)
    {
        throw new NotImplementedException();
    }

    private void LogError(string errorFile, string game, string v)
    {
        throw new NotImplementedException();
    }

    private void Log(string v)
    {
        throw new NotImplementedException();
    }
}