using Gambler.Bot.Strategies.Helpers;
using Gambler.Bot.Classes;
using Gambler.Bot.Common.Helpers;
using Gambler.Bot.Core.Helpers;
using Gambler.Bot.ViewModels.Common;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Gambler.Bot.ViewModels.AppSettings
{
    public class GlobalSettingsViewModel : ViewModelBase
    {
        public Interaction<Unit?, Unit?> CloseWindow { get; internal set; }
        public event EventHandler SettingsSaved;
        public GeneralSettingsViewModel General { get; set; }
        public ErrorsViewModel Errors { get; set; }
        public BetStorageViewModel Storage { get; set; }
        public TriggersViewModel Triggers { get; set; }

        public EmailSettingsViewModel Emails { get; set; }

        private PersonalSettings _settings;
        public PersonalSettings Settings { get => _settings; }
        public bool Cancelled { get; set; } = false;

        public void SetSettings(PersonalSettings settings)
        {
            _settings = CopyHelper.CreateCopy(settings);
            General.Settings = _settings;
            Storage.Settings = _settings;
            Emails.Settings = _settings;
            Triggers.SetTriggers(_settings?.Notifications);
            Errors.Clear();
            foreach (var error in _settings?.ErrorSettings)
            {
                Errors.AddItem(error);
            }
        }

        public GlobalSettingsViewModel(ILogger logger) : base(logger)
        {
            CloseWindow = new Interaction<Unit?, Unit?>();
            General = new GeneralSettingsViewModel(logger);
            Errors = new ErrorsViewModel(logger);
            Storage = new BetStorageViewModel(logger);
            Triggers = new TriggersViewModel(logger) { Notifications = true };
            Emails = new EmailSettingsViewModel(logger);
        }

        public async Task Save()
        {
            if (!Storage.Verify())
            {
                //show error message
                return;
            }
            Emails.Save();
            UISettings.Settings = General.UiSettings;
            SettingsSaved?.Invoke(this, new EventArgs());
            await CloseWindow.Handle(null);

        }

        public async Task Cancel()
        {
            //call close window interaction
            Cancelled = true;
            await CloseWindow.Handle(null);
        }
    }
}
