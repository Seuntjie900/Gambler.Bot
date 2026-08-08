using Avalonia.Interactivity;
using Gambler.Bot.Strategies.Helpers;
using Gambler.Bot.Classes;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System.Collections.Generic;

namespace Gambler.Bot.ViewModels.AppSettings
{
    public class BetStorageViewModel: ViewModelBase
    {
        public List<string> Storages { get; set; }
        private PersonalSettings settings;

        public PersonalSettings Settings
        {
            get { return settings; }
            set { settings = value; this.RaisePropertyChanged();
                Password = settings.EncryptConstring ? settings.EncrConnectionString : "";
                SelectedStorageTypeIndex = Storages.IndexOf(settings.Provider);
                 
            }
        }

        private string password;

        public string Password
        {
            get { return password; }
            set { password = value; }
        }
        private int selectedStorageTypeIndex = 0;

        public int SelectedStorageTypeIndex
        {
            get { return selectedStorageTypeIndex; }
            set { selectedStorageTypeIndex = value; this.RaisePropertyChanged(); SetStorageType(); }
        }

        private void SetStorageType()
        {
            switch (SelectedStorageTypeIndex)
            {
                case 0: DBVM = new SQLiteViewModel(_logger) ; break;
                case 1: DBVM = new SQLServerViewModel(_logger); break;
                case 2: DBVM = new MySqlViewModel(_logger); break;
                case 3: DBVM = new MongoDBViewModel(_logger); break;
                case 4: DBVM = new PostGresViewModel(_logger); break;
                case 5: DBVM = null; UpdateSettings(); break;
                default: DBVM = new SQLiteViewModel(_logger); break;
            }
        }

        private iDatabaseForm dbVM;

        public iDatabaseForm DBVM
        {
            get { return dbVM; }
            set { dbVM = value; this.RaisePropertyChanged(); }
        }


        public BetStorageViewModel(ILogger logger) : base(logger)
        {
            SelectedStorageTypeIndex = 0;
            Storages = new List<string>() { "SQLite", "SQL Server", "MySQL", "MongoDb", "PostGres", "None" };
        }

        public void UpdateSettings()
        {
            Settings.Provider = DBVM?.Provider()??"None";
            Settings.EncryptConstring = !string.IsNullOrWhiteSpace(Password);
            Settings.SetConnectionString(DBVM?.ConnectionString(), Password);
        }

        public bool Verify()
        {
            return DBVM?.Validate()??true;
        }

        
    }
}
