using ActiproSoftware.UI.Avalonia.Controls;
using ActiproSoftware.UI.Avalonia.Themes;
using Gambler.Bot.Classes;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gambler.Bot.ViewModels
{
    public class MainViewModel:ViewModelBase
    {
        private InstanceViewModel _instance;
        public InstanceViewModel Instance { get =>_instance; set { _instance = value; this.RaisePropertyChanged(); } }
        static string uiSettingsFile;
        internal static ILogger log = null;
        public MainViewModel(Microsoft.Extensions.Logging.ILogger logger) : base(logger)
        {
            _logger.LogDebug("MainViewModel created");
           
            log = logger;
            uiSettingsFile = string.Empty;
            UISettings.Portable = App.IsPortable(); //this needs to integrate with velopacks portable thing
            if (UISettings.Portable)
            {
                logger.LogDebug("Portable");
                uiSettingsFile = "";
            }
            else
            {
                uiSettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Gambler.Bot");
            }
            Instance = new InstanceViewModel(logger);
            uiSettingsFile = Path.Combine(uiSettingsFile, "UISettings.json");
            if (File.Exists(uiSettingsFile))
            {
                logger.LogDebug("Loading UI settings");
                UISettings.Settings = JsonSerializer.Deserialize<UISettings>(File.ReadAllText(uiSettingsFile));
                //change the theme somehow?
                if (UISettings.Settings.DarkMode != null)
                {
                    App.Current.RequestedThemeVariant = UISettings.Settings.DarkMode == true ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
                }
                if ((UISettings.Settings?.ThemeName??"Default") != "Default")
                {
                    if (!ModernTheme.TryGetCurrent(out var theme))
                    {
                        return;
                    }
                    logger.LogDebug("Applying theme");
                    theme.Definition.AccentColorRampName = UISettings.Settings.ThemeName;
                    theme.RefreshResources();
                }
                _= Loaded();
            }
        }

        public async Task Loaded()
        {
            _logger.LogDebug("Check for updates");
            try
            {
                if (UISettings.Settings.UpdateMode == "Auto")
                {
                    await App.UpdateMyApp();
                }
                else if (UISettings.Settings.UpdateMode == "Prompt")
                {
                    _logger.LogDebug("Check if there are updates");
                    if (await App.HasUpdate())
                    {
                        _logger.LogDebug("Updates found");
                        var result = await MessageBox.Show("There is an update available. Would you like to update now?", "Update Available", MessageBoxButtons.YesNo, MessageBoxImage.Information);
                        if (result == MessageBoxResult.Yes)
                        {
                            await App.UpdateMyApp();
                        }
                    }
                    else
                    {
                        _logger.LogDebug("no updates");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates");
            }
        }

        public void SaveUISettings()
        {
            if (!UISettings.Resetting)
            {
                if (Path.GetDirectoryName(uiSettingsFile)!=string.Empty && !Directory.Exists(Path.GetDirectoryName(uiSettingsFile)))
                    Directory.CreateDirectory(Path.GetDirectoryName(uiSettingsFile));

                File.WriteAllText(uiSettingsFile, JsonSerializer.Serialize(UISettings.Settings));
            }
        }

        internal static void ClearUiSettings()
        {
            if (File.Exists(uiSettingsFile))
                File.Delete(uiSettingsFile);
        }

        internal void OnClosing()
        {
            Instance?.OnClosing();
            SaveUISettings();
        }
    }
}
