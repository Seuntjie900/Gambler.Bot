using Gambler.Bot.Classes.BetsPanel;
using Gambler.Bot.Common.Games;
using Gambler.Bot.Common.Games.RangeDice;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace Gambler.Bot.ViewModels.Games.RangeDice
{
    public class RangeDicePlaceBetViewModel : ViewModelBase, iPlaceBet
    {
        private const decimal MinimumAmount = 0.00000001m;

        public RangeDicePlaceBetViewModel(ILogger logger) : base(logger)
        {
            TypeOptions = Enum.GetValues<RangeDiceType>();
            DoubleAmountCommand = ReactiveCommand.Create(DoubleAmount);
            HalfAmountCommand = ReactiveCommand.Create(HalveAmount);
        }

        public IReadOnlyList<RangeDiceType> TypeOptions { get; }

        private bool _showAmount = true;

        public bool ShowAmount
        {
            get { return _showAmount; }
            set { _showAmount = value; this.RaisePropertyChanged(); }
        }

        public ICommand DoubleAmountCommand { get; }
        public ICommand HalfAmountCommand { get; }

        private decimal amount = 0.00000100m;

        public decimal Amount
        {
            get { return amount; }
            set { amount = value; this.RaisePropertyChanged(nameof(Amount)); }
        }

        private decimal min = 25m;

        public decimal Min
        {
            get { return min; }
            set
            {
                if (min == value) return;
                min = value;
                this.RaisePropertyChanged(nameof(Min));
                this.RaisePropertyChanged(nameof(RangePreview));
            }
        }

        private decimal max = 75m;

        public decimal Max
        {
            get { return max; }
            set
            {
                if (max == value) return;
                max = value;
                this.RaisePropertyChanged(nameof(Max));
                this.RaisePropertyChanged(nameof(RangePreview));
            }
        }

        private decimal min2 = 25m;

        public decimal Min2
        {
            get { return min2; }
            set
            {
                if (min2 == value) return;
                min2 = value;
                this.RaisePropertyChanged(nameof(Min2));
                this.RaisePropertyChanged(nameof(RangePreview));
            }
        }

        private decimal max2 = 75m;

        public decimal Max2
        {
            get { return max2; }
            set
            {
                if (max2 == value) return;
                max2 = value;
                this.RaisePropertyChanged(nameof(Max2));
                this.RaisePropertyChanged(nameof(RangePreview));
            }
        }

        private RangeDiceType type = RangeDiceType.In;

        public RangeDiceType Type
        {
            get { return type; }
            set
            {
                if (type == value) return;
                type = value;
                this.RaisePropertyChanged(nameof(Type));
                this.RaisePropertyChanged(nameof(ShowSecondRange));
                this.RaisePropertyChanged(nameof(RangePreview));
            }
        }

        public bool ShowSecondRange => Type == RangeDiceType.Double;

        public string RangePreview => Type switch
        {
            RangeDiceType.In => $"In {Min} - {Max}",
            RangeDiceType.Out => $"Out {Min} - {Max}",
            RangeDiceType.Double => $"In {Min} - {Max} or {Min2} - {Max2}",
            _ => string.Empty
        };

        public void DoubleAmount()
        {
            Amount *= 2;
            if (Amount < MinimumAmount)
            {
                Amount = MinimumAmount;
            }
        }

        public void HalveAmount()
        {
            Amount /= 2;
            if (Amount < MinimumAmount)
            {
                Amount = MinimumAmount;
            }
        }

        public IGameConfig GameSettings { get; set; }

        public virtual Bot.Common.Games.Games Game => Bot.Common.Games.Games.RangeDice;

        public virtual event EventHandler<PlaceBetEventArgs> PlaceBet;

        protected virtual void Bet()
        {
            PlaceBet?.Invoke(this, new PlaceBetEventArgs(
                Type == RangeDiceType.Double
                    ? new PlaceRangeDiceBet(Type, Amount, Min, Max, Min2, Max2)
                    : new PlaceRangeDiceBet(Type, Amount, Min, Max)));
        }

        public void BetCommand()
        {
            Bet();
        }
    }
}
