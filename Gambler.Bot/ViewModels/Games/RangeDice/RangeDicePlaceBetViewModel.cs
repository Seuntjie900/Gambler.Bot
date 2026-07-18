using Gambler.Bot.Classes.BetsPanel;
using Gambler.Bot.Common.Games;
using Gambler.Bot.Common.Games.Dice;
using ReactiveUI;
using System;
using System.Windows.Input;

namespace Gambler.Bot.ViewModels.Games.Dice
{
    public class DicePlaceBetViewModel :ViewModelBase, iPlaceBet
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

        private bool _highChecked;

        public bool HighChecked
        {
            get { return _highChecked; }
            set { _highChecked = value; this.RaisePropertyChanged(); this.RaisePropertyChanged(nameof(LowChecked)); }
        }

        private bool showHighLow=true;

        public bool ShowHighLow
        {
            get { return showHighLow; }
            set { showHighLow = value; this.RaisePropertyChanged(); this.RaisePropertyChanged(nameof(ShowButton)); this.RaisePropertyChanged(nameof(ShowToggle)); }
        }



        public bool LowChecked
        {
            get { return !HighChecked; }
            set { HighChecked = !value; }
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



        public DicePlaceBetViewModel(Microsoft.Extensions.Logging.ILogger logger) : base(logger)
        {
            BetHighCommand = ReactiveCommand.Create(BetHigh);
            BetLowCommand = ReactiveCommand.Create(BetLow);

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

        private bool showToggle=false;

        public bool ShowToggle
        {
            get { return showToggle && showHighLow; }
            set { showToggle = value; this.RaisePropertyChanged();this.RaisePropertyChanged(nameof(ShowButton)); }
        }

        public bool ShowButton { get=>!ShowToggle && showHighLow; }
        public IGameConfig GameSettings { get; set; }

        public virtual Bot.Common.Games.Games Game => Bot.Common.Games.Games.Dice;

        public virtual event EventHandler<PlaceBetEventArgs> PlaceBet;

        protected virtual void Bet(bool High)
        {
            PlaceBet?.Invoke(this, new PlaceBetEventArgs(new PlaceDiceBet(Amount, High, Chance)));
        }

        private void BetHigh()
        {
            Bet(true);
        }
        private void BetLow()
        {
            Bet(false);
        }

        public void BetCommand()
        {
            Bet(HighChecked);
        }
    }
}
