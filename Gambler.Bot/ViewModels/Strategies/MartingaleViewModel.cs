using Gambler.Bot.Strategies.Strategies;
using Gambler.Bot.Strategies.Strategies.Abstractions;
using Gambler.Bot.Classes.BetsPanel;
using Gambler.Bot.Classes.Strategies;
using Gambler.Bot.ViewModels.Games.Dice;
using ReactiveUI;
using System;
using System.ComponentModel;
using static Gambler.Bot.Core.Sites.WolfBet;
using System.Collections.Generic;
using Gambler.Bot.Helpers;
using Gambler.Bot.Common.Games;
using ShimSkiaSharp;

namespace Gambler.Bot.ViewModels.Strategies
{
    internal class MartingaleViewModel : ViewModelBase, IStrategy
    {
        private Martingale _strategy;

        public Martingale Strategy
        {
            get { return _strategy; }
            set { _strategy = value; this.RaisePropertyChanged(); }
        }

        public List<MartingaleMultiplierMode> MultiplierModes { get; set; }

        public decimal? Multiplier 
        { 
            get => Strategy?.BaseMultiplier; 
            set 
            { 
                Strategy.BaseMultiplier = value??2; 
                this.RaisePropertyChanged(nameof(Multiplier)); 
                this.RaisePropertyChanged(nameof(PercIncrease)); 
            } 
        }
        public decimal? PercIncrease 
        { 
            get => Multiplier*100m-100m; 
            set 
            { 
                Multiplier = ((value ?? 1) + 100m)/100m; 
            } 
        }
        public decimal? WinMultiplier
        {
            get => Strategy?.WinBaseMultiplier;
            set
            {
                Strategy.WinBaseMultiplier = value ?? 2;
                this.RaisePropertyChanged(nameof(WinMultiplier));
                this.RaisePropertyChanged(nameof(WinPercIncrease));
            }
        }
        public decimal? WinPercIncrease
        {
            get => WinMultiplier * 100m - 100m;
            set
            {
                WinMultiplier = ((value ?? 1) + 100m) / 100m;
            }
        }

        private Bot.Common.Games.Games  _game;

        private iPlaceBet _placeBetVM;

        public iPlaceBet PlaceBetVM
        {
            get { return _placeBetVM; }
            set 
            { 
                _placeBetVM = value;
                SyncStartControl();
                this.RaisePropertyChanged(); 
                
            }
        }

        public MartingaleMultiplierMode MultiMode {  get=>Strategy.MultiplierMode; set { Strategy.MultiplierMode = value; this.RaisePropertyChanged(nameof(MultiMode)); this.RaisePropertyChanged(nameof(ShowMax)); this.RaisePropertyChanged(nameof(ShowModifier)); } }
        public MartingaleMultiplierMode WinMultiMode { get => Strategy.WinMultiplierMode; set { Strategy.WinMultiplierMode = value; this.RaisePropertyChanged(nameof(WinMultiMode)); this.RaisePropertyChanged(nameof(ShowWinMax)); this.RaisePropertyChanged(nameof(ShowWinModifier)); } }

        public bool ShowMax { get => Strategy.MultiplierMode == MartingaleMultiplierMode.Max; }
        
        public bool ShowModifier {  get => Strategy.MultiplierMode == MartingaleMultiplierMode.Variable || Strategy.MultiplierMode == MartingaleMultiplierMode.ChangeOnce; }

        public bool ShowWinMax { get => Strategy.WinMultiplierMode == MartingaleMultiplierMode.Max; }

        public bool ShowWinModifier { get => Strategy.WinMultiplierMode == MartingaleMultiplierMode.Variable || Strategy.WinMultiplierMode == MartingaleMultiplierMode.ChangeOnce; }

        public Bot.Common.Games.Games Game
        {
            get { return _game; }
            set { _game = value; this.RaisePropertyChanged(); }
        }

        public MartingaleViewModel(Microsoft.Extensions.Logging.ILogger logger) : base(logger)
        {
            MultiplierModes = new List<MartingaleMultiplierMode>();
            foreach (MartingaleMultiplierMode x in Enum.GetValues(typeof(MartingaleMultiplierMode)))
            {
                MultiplierModes.Add(x);
            }
        }
        public void GameChanged(Bot.Common.Games.Games newGame, IGameConfig config)
        {
            if (PlaceBetVM!=null && PlaceBetVM is INotifyPropertyChanged notify)
            {
                notify.PropertyChanged -= Notify2_PropertyChanged;
            }
            Game = newGame;
            switch (Game)
            {
                case Bot.Common.Games.Games.Dice: PlaceBetVM = new DicePlaceBetViewModel(_logger) { ShowToggle = true };break;
                case Bot.Common.Games.Games.Twist: PlaceBetVM = new DicePlaceBetViewModel(_logger) { ShowToggle = true }; break;
                case Bot.Common.Games.Games.Limbo: PlaceBetVM = new DicePlaceBetViewModel(_logger) { ShowHighLow=false }; break;
                default: PlaceBetVM = null; break;
            }
            if (PlaceBetVM != null && PlaceBetVM is INotifyPropertyChanged notify2)
            {
                notify2.PropertyChanged += Notify2_PropertyChanged;
            }
            if (PlaceBetVM!=null)
            {
                PlaceBetVM.GameSettings = config;
            }
        }

        private void Notify2_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.PropertyName))
                return;
            object value = sender.GetType().GetProperty(e.PropertyName).GetValue(sender);
            switch (e.PropertyName)
            {
                case "Amount":
                    Strategy.MinBet = (decimal)value;
                    break;
                case "Chance":
                    Strategy.BaseChance = (decimal)value;
                    break;
                case "HighChecked":
                    Strategy.starthigh = (bool)value;
                    break;
            }
        }

        public void SetStrategy(BaseStrategy Strategy)
        {
            if (Strategy == null) 
                throw new ArgumentNullException();
            if (!(Strategy is Martingale mart))
                throw new ArgumentException("Must be martingale to use thise viewmodel");

            this.Strategy = mart;
            SyncStartControl();
        }

        void SyncStartControl()
        {
            if (PlaceBetVM is DicePlaceBetViewModel dice)
            {
                dice.Amount = Strategy.MinBet;
                dice.HighChecked = Strategy.starthigh;
                dice.Chance = Strategy.BaseChance;

                //dice.ShowAmount = false;
            }
        }

        public void Saving()
        {
            if (PlaceBetVM is DicePlaceBetViewModel dice)
            {
                Strategy.MinBet = dice.Amount;
                Strategy.starthigh = dice.HighChecked;
                Strategy.BaseChance = dice.Chance;
                
            }
        }
        public bool TopAlign()
        {
            return true;
        }
        public void Dispose()
        {
            _placeBetVM = null;
        }
    }
}
