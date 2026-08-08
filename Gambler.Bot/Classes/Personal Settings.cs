using Gambler.Bot.Common.Enums;
using Gambler.Bot.Core.Helpers;
using Gambler.Bot.Helpers;
using Gambler.Bot.Interfaces;
using Gambler.Bot.Strategies.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Gambler.Bot.Classes
{
    public class PersonalSettings
    {
        private ObservableCollection<Trigger> _triggers = new ObservableCollection<Trigger>();
        private Dictionary<ErrorType, ErrorSetting> _Errors = new Dictionary<ErrorType, ErrorSetting>();

        public ObservableCollection<Trigger> Notifications { get { return _triggers; } set { _triggers = value; } }
        private List<ErrorSetting> errorSettings;

        public List<ErrorSetting> ErrorSettings
        {
            get { return ArrayFromDictonary(); }
            set {  CompareErrorDictionary(value); }
        }

        private List<ErrorSetting> ArrayFromDictonary()
        {
            List<ErrorSetting> settings = new List<ErrorSetting>();
            foreach (ErrorType x in _Errors.Keys)
            {
                settings.Add(_Errors[x]);
            }
            return settings;
        }

        private void CompareErrorDictionary(List<ErrorSetting> newSettings)
        {
            foreach (ErrorSetting x in newSettings)
            {
                if (_Errors.ContainsKey(x.Type))
                {
                    _Errors[x.Type] = x;
                }
                else
                {
                    _Errors.Add(x.Type, x);
                }
            }
        }


        #region Error Settings
        //betting error
        //Withdrawal error
        //Invest error
        //Tip Error
        //Reset Seed Error
        /*public ErrorActions BetError { get; set; } = ErrorActions.Retry;
        public ErrorActions BalanceTooLow { get; set; } = ErrorActions.Stop;
        public ErrorActions WithdrawalError { get; set; } = ErrorActions.Resume;
        public ErrorActions InvestError { get; set; } = ErrorActions.Resume;
        public ErrorActions TipError { get; set; } = ErrorActions.Resume;
        public ErrorActions ResetSeedError { get; set; } = ErrorActions.Resume;*/
        public int RetryDelay { get; set; } = 30;
        public int RetryAttempts { get; set; } = 0;

        #endregion

        public string EncrConnectionString { get; set; }

        public string Provider { get; set; }

        public bool EncryptConstring { get; set; }

        
        public string GetConnectionString(string Password)
        {
            if (EncryptConstring)
            {
                return EncryptionHelper.Decrypt(EncrConnectionString, Password);
            }
            else
            {
                return EncrConnectionString;
            }
        }

        public void SetConnectionString(string ConnectionString, string Password)
        {
            if (EncryptConstring)
            {
                EncrConnectionString = EncryptionHelper.Encrypt(ConnectionString, Password);
            }
            else
            {
                EncrConnectionString = ConnectionString;
            }
        }

        public void EnsureErrorSettings()
        {
            if (!_Errors.ContainsKey(ErrorType.BalanceTooLow))
                _Errors.Add(ErrorType.BalanceTooLow,new PersonalSettings.ErrorSetting { Type = ErrorType.BalanceTooLow, Action = ErrorActions.Retry });
            if (!_Errors.ContainsKey(ErrorType.BetMismatch))
                _Errors.Add(ErrorType.BetMismatch, new PersonalSettings.ErrorSetting { Type = ErrorType.BetMismatch, Action = ErrorActions.Stop });
            if (!_Errors.ContainsKey(ErrorType.InvalidBet))
                _Errors.Add(ErrorType.InvalidBet, new PersonalSettings.ErrorSetting { Type = ErrorType.InvalidBet, Action = ErrorActions.Stop });
            if (!_Errors.ContainsKey(ErrorType.NotImplemented))
                _Errors.Add(ErrorType.NotImplemented, new PersonalSettings.ErrorSetting { Type = ErrorType.NotImplemented, Action = ErrorActions.Stop });
            if (!_Errors.ContainsKey(ErrorType.Other))
                _Errors.Add(ErrorType.Other, new PersonalSettings.ErrorSetting { Type = ErrorType.Other, Action = ErrorActions.Stop });
            if (!_Errors.ContainsKey(ErrorType.ResetSeed))
                _Errors.Add(ErrorType.ResetSeed, new PersonalSettings.ErrorSetting { Type = ErrorType.ResetSeed, Action = ErrorActions.Resume });
            if (!_Errors.ContainsKey(ErrorType.Tip))
                _Errors.Add(ErrorType.Tip, new PersonalSettings.ErrorSetting { Type = ErrorType.Tip, Action = ErrorActions.Resume });
            if (!_Errors.ContainsKey(ErrorType.Unknown))
                _Errors.Add(ErrorType.Unknown, new PersonalSettings.ErrorSetting { Type = ErrorType.Unknown, Action = ErrorActions.Stop });
            if (!_Errors.ContainsKey(ErrorType.Withdrawal))
                _Errors.Add(ErrorType.Withdrawal, new PersonalSettings.ErrorSetting { Type = ErrorType.Withdrawal, Action = ErrorActions.Resume });
            if (!_Errors.ContainsKey(ErrorType.BetTooLow))
                _Errors.Add(ErrorType.BetTooLow, new PersonalSettings.ErrorSetting { Type = ErrorType.BetTooLow, Action = ErrorActions.Stop });
            if (!_Errors.ContainsKey(ErrorType.Bank))
                _Errors.Add(ErrorType.Bank, new PersonalSettings.ErrorSetting { Type = ErrorType.Bank, Action = ErrorActions.Resume });
            
        }

        public ErrorSetting GetErrorSetting(ErrorType Type)
        {
            if (_Errors.ContainsKey(Type))
                return _Errors[Type];
            return null;
        }

        public class ErrorSetting: INotifyPropertyChanged
        {
            ErrorType type;
            ErrorActions action;
            public ErrorType Type { 
                get => type; 
                set { type = value; RaisePropertyChanged(); } }
            public ErrorActions Action { 
                get => action; 
                set { action = value; RaisePropertyChanged(); } }

            public event PropertyChangedEventHandler PropertyChanged;
            public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
            {

                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));

            }
        }
        public class GetConstringPWEventArgs:EventArgs
        {
            public string Password { get; set; }
        }

        public string KeepassDatabase { get; set; }

        public static PersonalSettings Default()
        {
            PersonalSettings settings = new PersonalSettings();
            settings.EncryptConstring = false;
            settings.Provider = "SQLite";
            settings.EncrConnectionString = string.Format("Data Source={0};",Path.Combine( Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) , "Gambler.Bot","GamblerBot.db"));
            settings.RetryAttempts = 5;
            settings.RetryDelay = 30;
            settings.EnsureErrorSettings();
            
            
            return settings;
        }

        public string EmailProviderType { get; set; }
    }
   
}

