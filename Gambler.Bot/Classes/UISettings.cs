using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Gambler.Bot.Classes
{
    public class UISettings: INotifyPropertyChanged
    {
        public static UISettings Settings = new UISettings();
        public static bool Portable = App.IsPortable();
        public static bool Resetting = false;
        bool? darkMode = true;
        public bool? DarkMode { get=>darkMode; set { darkMode = value; RaisePropertyChanged(); } }
        string themeName;
        public string ThemeName { get => themeName; set { themeName = value; RaisePropertyChanged(); } }
        int chartBets = 1000;
        public int ChartBets { get => chartBets; set { chartBets = value; RaisePropertyChanged(); } }

        int liveBets = 100;
        public int LiveBets { get => liveBets; set { liveBets = value; RaisePropertyChanged(); } }
        int consoleLines = 1000;
        public int ConsoleLines { get => consoleLines; set { consoleLines = value; RaisePropertyChanged(); } }
        bool asyncUIUpdates = false;
        public bool AsyncUIUpdates { get => asyncUIUpdates; set { asyncUIUpdates = value; RaisePropertyChanged(); } }
        string donateMode;
        string updateMode ="Prompt";
        public string UpdateMode 
        { 
            get =>updateMode; 
            set 
            { updateMode = value; RaisePropertyChanged(); } 
        }
        public string DonateMode
        {
            get => donateMode;
            set { donateMode = value; RaisePropertyChanged(); }
        }


        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaisePropertyChanged( [CallerMemberName] string propertyName = null)
        {

            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));

        }
    }
}
