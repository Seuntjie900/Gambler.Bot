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
    internal class DAlembertViewModel:ViewModelBase, IStrategy
    {
		private DAlembert _strategy;

		public DAlembert Strategy
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

        public DAlembertViewModel(Microsoft.Extensions.Logging.ILogger logger) : base(logger)
        {
            
        }
        public void GameChanged(Bot.Common.Games.Games newGame, IGameConfig config)
        {
            if (PlaceBetVM != null && PlaceBetVM is INotifyPropertyChanged notify)
            {
                notify.PropertyChanged -= Notify2_PropertyChanged;
            }
            Game = newGame;
            switch (Game)
            {
                case Bot.Common.Games.Games.Dice: PlaceBetVM = new DicePlaceBetViewModel(_logger) { ShowToggle = true }; break;
                case Bot.Common.Games.Games.Twist: PlaceBetVM = new DicePlaceBetViewModel(_logger) { ShowToggle = true }; break;
                case Bot.Common.Games.Games.Limbo: PlaceBetVM = new DicePlaceBetViewModel(_logger) { ShowHighLow = false }; break;
                default: PlaceBetVM = null; break;
            }
            if (PlaceBetVM != null && PlaceBetVM is INotifyPropertyChanged notify2)
            {
                notify2.PropertyChanged += Notify2_PropertyChanged;
            }
            if (PlaceBetVM != null)
                PlaceBetVM.GameSettings = config;
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
                    Strategy.Chance = (decimal)value;
                    break;
            }
        }

        public void SetStrategy(BaseStrategy Strategy)
        {
            if (Strategy == null)
                throw new ArgumentNullException();
            if (!(Strategy is DAlembert mart))
                throw new ArgumentException("Must be martingale to use thise viewmodel");

            this.Strategy = mart;
            SyncStartControl();
        }

        void SyncStartControl()
        {
            if (PlaceBetVM is DicePlaceBetViewModel dice)
            {
                dice.Amount = Strategy.MinBet;
                dice.Chance = Strategy.Chance;
                //dice.ShowAmount = false;
            }
        }
        public void Saving()
        {
            if (PlaceBetVM is DicePlaceBetViewModel dice)
            {
                Strategy.MinBet = dice.Amount;

                Strategy.Chance = dice.Chance;

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
