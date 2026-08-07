using Gambler.Bot.Classes;
using Gambler.Bot.Emails;
using Gambler.Bot.Interfaces;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gambler.Bot.ViewModels.AppSettings
{
    public class EmailSettingsViewModel : ViewModelBase
    {
        PersonalSettings settings;
        public PersonalSettings Settings { get=>settings; set { settings = value; Load(); }}
        public IEmailProvider Provider { get; set; }
        public List<string> Providers { get; set; }
        private string selectedProvider;
        public string SelectedProvider 
        { 
            get => selectedProvider; 
            set
            {
                selectedProvider = value;
                this.RaisePropertyChanged(nameof(SelectedProvider));
                this.RaisePropertyChanged(nameof(ShowSMTP));
            }
        }
        public bool ShowSMTP { get => SelectedProvider == "SMTP"; }
        public EmailSettingsViewModel(ILogger logger) : base(logger)
        {
            Providers = typeof(IEmailProvider).Assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && typeof(IEmailProvider).IsAssignableFrom(type)).Select(x => x.Name)
                .ToList(); 
    
        }
        private void Load()
        {
            if (Settings != null)
            {
                SelectedProvider = settings.EmailProviderType;
               
                if (SelectedProvider=="SMTP" )
                {
                    string sFile = Path.Combine(InstanceViewModel.SettingsDirectory, "SMTP.json");
                    if (File.Exists(sFile))
                    {
                        string ssettings = File.ReadAllText(sFile);
                        var settings = JsonSerializer.Deserialize<SMTPSettings>(ssettings);
                        this.smtpHost = settings.Host;
                        this.smtpPass = settings.Password;
                        this.smtpPort = settings.Port;
                        this.smtpUser = settings.Username;
                        this.Sender = settings.Sender;
                        this.Email = settings.Recipient;
                    }
                }
            }
        }
        public string smtpHost { get; set; }
        public int smtpPort { get; set; }
        public string smtpUser { get; set; }
        public string smtpPass { get; set; }
        public string Email { get; set; }
        public string Sender { get; set; }

        public void Save()
        {
            settings.EmailProviderType = SelectedProvider;
            switch (selectedProvider)
            {
                case "SMTP": SMTPSettings tmp = new SMTPSettings() { Host=smtpHost, Password=smtpPass, Port=smtpPort, Username=smtpUser, Sender =Sender, Recipient=Email };
                    File.WriteAllText(Path.Combine(InstanceViewModel.SettingsDirectory, "SMTP.json"), JsonSerializer.Serialize(tmp));
                    break;
            }
        }
    }
}
