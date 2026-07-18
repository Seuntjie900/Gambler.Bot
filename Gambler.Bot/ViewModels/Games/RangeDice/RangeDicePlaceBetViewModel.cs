using Gambler.Bot.Classes.BetsPanel;
using Gambler.Bot.Common.Games;
using Gambler.Bot.Common.Games.RangeDice;
using ReactiveUI;
using System;
using System.Windows.Input;

namespace Gambler.Bot.ViewModels.Games.RangeDice
{
    public class RangeDicePlaceBetViewModel :ViewModelBase, iPlaceBet
    {
        private bool _showAmount=true;

        public bool ShowAmount
        {
            get { return _showAmount; }
            set { _showAmount = value; this.RaisePropertyChanged(); }
        }
        private bool _showChance = true;

        public bool ShowChance
        {
            get { return _showChance; }
            set { _showChance = value; this.RaisePropertyChanged(); }
        }

        
        public ICommand BetHighCommand { get; }
        public ICommand BetLowCommand { get; }

        public ICommand DoubleAmountCommand { get; }
        public ICommand HalfAmountCommand { get; }
        public ICommand DoubleChanceCommand { get; }
        public ICommand HalfChanceCommand { get; }
        public ICommand DoublePayoutCommand { get; }
        public ICommand HalfPayoutCommand { get; }

        private decimal amount=0.00000100m;

        public decimal Amount
        {
            get { return amount; }
            set { amount = value; this.RaisePropertyChanged(nameof(Amount)); Calculate(nameof(Amount)); }
        }

        private decimal chance=49.5m;

        public decimal Chance
        {
            get { return chance; }
            set { chance = value; this.RaisePropertyChanged(nameof(Chance)); Calculate(nameof(Chance)); }
        }

        private decimal payout=2;

        public decimal Payout
        {
            get { return payout; }
            set { payout = value; this.RaisePropertyChanged(nameof(Payout)); Calculate(nameof(Payout)); }
        }

        private decimal profit=0.00000100m;

        public decimal Profit
        {
            get { return profit; }
            set { profit = value; this.RaisePropertyChanged(nameof(Profit)); Calculate(nameof(Profit)); }
        }
        private decimal min=25m;

        public decimal Min
        {
            get { return min; }
            set { min = value; this.RaisePropertyChanged(nameof(Min)); Calculate(nameof(Min)); }
        }
        private decimal max=75m;

        public decimal Max
        {
            get { return max; }
            set { max = value; this.RaisePropertyChanged(nameof(Max)); Calculate(nameof(Max)); }
        }
        private decimal min2=25m;

        public decimal Min2
        {
            get { return min2; }
            set { min2 = value; this.RaisePropertyChanged(nameof(Min2)); Calculate(nameof(Min2)); }
        }
        private decimal max2=75m;

        public decimal Max2
        {
            get { return max2; }
            set { max2 = value; this.RaisePropertyChanged(nameof(Max2)); Calculate(nameof(Max2)); }
        }
        private RangeDiceType type= RangeDiceType.In  ;

        public RangeDiceType Type
        {
            get { return type; }
            set { type = value; this.RaisePropertyChanged(nameof(Type)); Calculate(nameof(Type)); }
        }
        

        public RangeDicePlaceBetViewModel(Microsoft.Extensions.Logging.ILogger logger) : base(logger)
        {
            DoubleAmountCommand = ReactiveCommand.Create(DoubleAmount);
            HalfAmountCommand = ReactiveCommand.Create(HalveAmount);
            DoubleChanceCommand = ReactiveCommand.Create(DoubleChance);
            HalfChanceCommand = ReactiveCommand.Create(HalveChance);
            DoublePayoutCommand = ReactiveCommand.Create(DoublePayout);
            HalfPayoutCommand = ReactiveCommand.Create(HalvePayout);
            Calculate(nameof(Amount));
        }

        void DoubleAmount()
        {
            Amount = Amount * 2;
            if (Amount< 0.00000001m )
            {
                amount = 0.00000001m;
            }
        }

        void HalveAmount()
        {
            Amount = Amount / 2;
            if (Amount < 0.00000001m )
            {
                amount = 0;
            }
        }

        void DoubleChance()
        {
            if (Chance < 50)
                Chance *= 2m;
            else Chance += 100m - (Chance/2m);
        }
        void HalveChance()
        { 
            Chance /= 2m;
        }
        void HalvePayout()
        {
            Payout /= 2m;
        }
        void DoublePayout()
        {
            Payout *= 2m;
        }
        void Calculate(string s)
        {
            switch (s)
            {
                case nameof(Amount):
                    if (Profit != (Amount * Payout) - Amount)
                    {
                        Profit = (Amount * Payout) - Amount;
                    }
                    break;
                case nameof(Chance):
                    if (Chance != 0)
                    {
                        if (Payout != (100m - (GameSettings?.Edge??1)) / Chance)
                        {
                            Payout = (100m - (GameSettings?.Edge ?? 1)) / Chance;
                        }
                    }
                    break;
                case nameof(Payout):
                    if (Payout != 0)
                    {
                        if (Chance != (100m - (GameSettings?.Edge ?? 1)) / Payout)
                        {
                            Chance = (100m - (GameSettings?.Edge ?? 1)) / Payout;
                        }
                        if (Profit != Amount * Payout - Amount)
                            Profit = Amount * Payout - Amount;
                    }
                    break;
            }
        }

      
        public IGameConfig GameSettings { get; set; }

        public virtual Bot.Common.Games.Games Game => Bot.Common.Games.Games.Dice;

        public virtual event EventHandler<PlaceBetEventArgs> PlaceBet;

        protected virtual void Bet()
        {
            PlaceBet?.Invoke(this, new PlaceBetEventArgs(new PlaceRangeDiceBet(Type, 
                Amount, min, max,min2,max2)));
        }


        public void BetCommand()
        {
            Bet();
        }
    }
}
