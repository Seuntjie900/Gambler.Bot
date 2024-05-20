﻿using DoormatBot.Helpers;
using KryGamesBot.Ava.Classes;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KryGamesBot.Ava.ViewModels.AppSettings
{
    public class GeneralSettingsViewModel: ViewModelBase
    {
        private PersonalSettings settings;

        public PersonalSettings Settings
        {
            get { return settings; }
            set { settings = value; this.RaisePropertyChanged(); }
        }

        private UISettings uiSettings;

        public UISettings UiSettings
        {
            get { return uiSettings; }
            set { uiSettings = value; this.RaisePropertyChanged(); }
        }

        

        public string DonateMode
        {
            get { return UiSettings?.DonateMode; }
            set 
            { 
                if (UiSettings != null)
                    UiSettings.DonateMode = value; 
                this.RaisePropertyChanged(nameof(ShowDonatePercentage));
            }
        }

        public bool ShowDonatePercentage { get => DonateMode == "Prompt"|| DonateMode=="Auto"; }

        public GeneralSettingsViewModel(ILogger logger) : base(logger)
        {
            UiSettings = UISettings.Settings;
        }
    }
}
