using Gambler.Bot.Strategies.Strategies;
using Gambler.Bot.Strategies.Strategies.Abstractions;
using Gambler.Bot.Classes.BetsPanel;
using Gambler.Bot.Classes.Strategies;
using Gambler.Bot.ViewModels.Games.Dice;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gambler.Bot.Common.Games;

namespace Gambler.Bot.ViewModels.Strategies
{
    internal class FibonacciViewModel : ViewModelBase, IStrategy
    {
        private Fibonacci _strategy;

        public Fibonacci Strategy
        {
            get { return _strategy; }
            set { _strategy = value; this.RaisePropertyChanged(); }
        }
        private Bot.Common.Games.Games _game;

        private iPlaceBet _placeBetVM;

        public iPlaceBet PlaceBetVM
        {
            get { return _placeBetVM; }
            set { _placeBetVM = value; SyncStartControl(); this.RaisePropertyChanged(); }
        }


        public Bot.Common.Games.Games Game
        {
            get { return _game; }
            set { _game = value; this.RaisePropertyChanged(); }
        }

        public FibonacciViewModel(Microsoft.Extensions.Logging.ILogger logger) : base(logger)
        {
            
        }
        public void GameChanged(Bot.Common.Games.Games newGame, IGameConfig config)
        {
            if (PlaceBetVM != null && PlaceBetVM is INotifyPropertyChanged notify)
            {
                notify.PropertyChanged -= Notify2_PropertyChanged;
            }
            Game = newGame;
            PlaceBetVM = iPlaceBet.GetFromGame(newGame, _logger);
            if (PlaceBetVM!=null)
                PlaceBetVM.GameSettings = config;
            if (PlaceBetVM != null && PlaceBetVM is INotifyPropertyChanged notify2)
            {
                notify2.PropertyChanged += Notify2_PropertyChanged;
            }
        }
        void SyncStartControl()
        {
            if (PlaceBetVM is DicePlaceBetViewModel dice)
            {
                dice.Amount = Strategy.minbet;
                dice.Chance = Strategy.Chance;
                //dice.ShowAmount = false;
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
                    Strategy.minbet = (decimal)value;
                    break;
                case "Chance":
                    Strategy.Chance = (decimal)value;
                    break;
            }
        }

        public void SetStrategy(BaseStrategy Strategy)
        {
            if (Strategy == null)
                throw new ArgumentNullException();
            if (!(Strategy is Fibonacci mart))
                throw new ArgumentException("Must be martingale to use thise viewmodel");

            this.Strategy = mart;
            if (PlaceBetVM is DicePlaceBetViewModel dice)
            {
                dice.Amount = mart.minbet;
                dice.Chance = mart.Chance;
                dice.ShowAmount = false;
            }
        }
        public void Saving()
        {

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
