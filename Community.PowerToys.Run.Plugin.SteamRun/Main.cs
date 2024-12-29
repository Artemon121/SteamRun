using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Windows.Controls;
using System.Windows.Input;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.SteamRun
{
    /// <summary>
    /// Main class of this plugin that implements all used interfaces.
    /// </summary>
    public class Main : IPlugin, IContextMenu, ISettingProvider, IDisposable
    {
        public static string PluginID => "5369f2045c3c40c9909545a72baeebf6";
        public string Name => "SteamRun";
        public string Description => "Launch games from Steam";

        private string? SteamInstallDir { get; set; }

        private PluginInitContext? Context { get; set; }
        private string? IconPath { get; set; }
        private bool Disposed { get; set; }

        private bool OverrideSteamInstallDir { get; set; }
        private bool HideApplications { get; set; }
        private bool HideTools { get; set; }
        private bool HideMusic { get; set; }

        /// <summary>
        /// Additional options for the plugin.
        /// </summary>
        public IEnumerable<PluginAdditionalOption> AdditionalOptions => [
            new() {
                Key = nameof(HideApplications),
                DisplayLabel = "Hide Applications",
                DisplayDescription = "Don't show applications in the search results",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                Value = HideApplications,
            },
            new() {
                Key = nameof(HideTools),
                DisplayLabel = "Hide Tools",
                DisplayDescription = "Don't show tools in the search results",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                Value = HideTools,
            },
            new() {
                Key = nameof(HideMusic),
                DisplayLabel = "Hide Music",
                DisplayDescription = "Don't show music in the search results",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                Value = HideMusic,
            },
            new() {
                Key = nameof(SteamInstallDir),
                DisplayLabel = "Override Steam install directory",
                DisplayDescription = "Override default Steam install directory. Use only if the plugin was unable to find it automatically",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.CheckboxAndTextbox,
                Value = OverrideSteamInstallDir,
                TextValue = SteamInstallDir,
            },
        ];

        /// <summary>
        /// Initialize the plugin with the given <see cref="PluginInitContext"/>.
        /// </summary>
        /// <param name="context">The <see cref="PluginInitContext"/> for this plugin.</param>
        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());

            try
            {
                SteamInstallDir = SteamFinder.FindInstallDir();
                Log.Info($"Steam install directory: {SteamInstallDir}", GetType());
            }
            catch (DirectoryNotFoundException e)
            {
                Log.Error($"Steam install directory not found: {e.Message}", GetType());
                context.API.ShowMsg("SteamRun", $"Steam install directory not found: {e.Message}");
                return;
            }
            catch (SecurityException e)
            {
                Log.Error($"Security exception: {e.Message}", GetType());
                context.API.ShowMsg("SteamRun", "Not enough permissions to get steam install directory from the registry. Try overriding it in the settings");
                return;
            }
        }

        /// <summary>
        /// Run the given app in Steam
        /// </summary>
        /// <param name="appID">ID of the app to run</param>
        private static void RunSteamApp(int appID)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "steam://run/" + appID,
                UseShellExecute = true,
            });
        }

        /// <summary>
        /// Open install directory of the given Steam app 
        /// </summary>
        private void OpenInstallDirectory(SteamLibraryApp app)
        {
            if (SteamInstallDir == null) return;

            var type = app.GetType(SteamInstallDir);
            var steamappsSubfolder = type switch
            {
                ESteamAppType.Music => "music",
                _ => "common",
            };
            var path = Path.Join(app.LibraryFolder.Path, "steamapps", steamappsSubfolder, app.InstallDir);

            if (!Path.Exists(path)) path = SteamInstallDir;

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }

        /// <summary>
        /// Return a filtered list, based on the given query.
        /// </summary>
        /// <param name="query">The query to filter the list.</param>
        /// <returns>A filtered list, can be empty when nothing was found.</returns>
        public List<Result> Query(Query query)
        {
            if (SteamInstallDir == null) return [];

            var results = new List<Result>();
            try
            {
                var libraryFolders = SteamFinder.FindLibraryFolders(SteamInstallDir);
                var appLogos = SteamFinder.FindAppLogos(SteamInstallDir);
                var allApps = libraryFolders.SelectMany(SteamFinder.FindApps);

                var apps = allApps.Where(app => app.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
                if (HideApplications) apps = apps.Where(app => app.GetType(SteamInstallDir) != ESteamAppType.Application);
                if (HideTools) apps = apps.Where(app => app.GetType(SteamInstallDir) != ESteamAppType.Tool);
                if (HideMusic) apps = apps.Where(app => app.GetType(SteamInstallDir) != ESteamAppType.Music);
                apps = apps.OrderByDescending(app => app.LastPlayed);

                foreach (var app in apps)
                {
                    results.Add(new Result
                    {
                        IcoPath = appLogos.GetValueOrDefault(app.ID, IconPath!),
                        Title = app.Name,
                        SubTitle = $"Steam {app.GetType(SteamInstallDir)} ({app.ID})",
                        ContextData = app,
                        Action = c =>
                        {
                            RunSteamApp(app.ID);
                            return true;
                        },
                    });
                };
            }
            catch (Exception e)
            {
                results.Add(new Result
                {
                    IcoPath = IconPath,
                    Title = "Error",
                    SubTitle = e.Message,
                });
                Log.Error($"Error in {e.Source} while finding Steam library apps: {e.Message}\n{e.StackTrace}", GetType());
            }

            return results;
        }

        /// <summary>
        /// Return a list context menu entries for a given <see cref="Result"/> (shown at the right side of the result).
        /// </summary>
        /// <param name="selectedResult">The <see cref="Result"/> for the list with context menu entries.</param>
        /// <returns>A list context menu entries.</returns>
        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            var contextMenus = new List<ContextMenuResult>();
            if (selectedResult.ContextData is not SteamLibraryApp app) return [];
            if (app.InstallDir != null && app.LibraryFolder != null)
            {
                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Name,
                    Title = "Open Install Folder (Ctrl+O)",
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = "\xE838", // Open Folder
                    AcceleratorModifiers = ModifierKeys.Control,
                    AcceleratorKey = Key.O,
                    Action = _ =>
                    {
                        OpenInstallDirectory(app);
                        return true;
                    },
                });
            }

            return contextMenus;
        }

        /// <summary>
        /// Creates setting panel.
        /// </summary>
        /// <returns>The control.</returns>
        /// <exception cref="NotImplementedException">method is not implemented.</exception>
        public Control CreateSettingPanel() => throw new NotImplementedException();

        /// <summary>
        /// Updates settings.
        /// </summary>
        /// <param name="settings">The plugin settings.</param>
        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            HideApplications = settings.AdditionalOptions.SingleOrDefault(x => x.Key == nameof(HideApplications))?.Value ?? false;
            HideTools = settings.AdditionalOptions.SingleOrDefault(x => x.Key == nameof(HideTools))?.Value ?? false;
            HideMusic = settings.AdditionalOptions.SingleOrDefault(x => x.Key == nameof(HideMusic))?.Value ?? false;
            var steamInstallDirSettings = settings.AdditionalOptions.SingleOrDefault(x => x.Key == nameof(SteamInstallDir));
            if (steamInstallDirSettings != null)
            {
                OverrideSteamInstallDir = steamInstallDirSettings.Value;
                if (OverrideSteamInstallDir)
                {
                    SteamInstallDir = steamInstallDirSettings.TextValue;
                }
                else
                {
                    SteamInstallDir = SteamFinder.FindInstallDir();
                    steamInstallDirSettings.TextValue = SteamInstallDir;
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Log.Info("Dispose", GetType());

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Wrapper method for <see cref="Dispose()"/> that dispose additional objects and events form the plugin itself.
        /// </summary>
        /// <param name="disposing">Indicate that the plugin is disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (Disposed || !disposing)
            {
                return;
            }

            if (Context?.API != null)
            {
                Context.API.ThemeChanged -= OnThemeChanged;
            }

            Disposed = true;
        }

        private void UpdateIconPath(Theme theme) => IconPath = theme == Theme.Light || theme == Theme.HighContrastWhite ? Context?.CurrentPluginMetadata.IcoPathLight : Context?.CurrentPluginMetadata.IcoPathDark;

        private void OnThemeChanged(Theme currentTheme, Theme newTheme) => UpdateIconPath(newTheme);
    }
}