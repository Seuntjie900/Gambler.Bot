using Gambler.Bot.Classes.BetsPanel;
using Gambler.Bot.Common.Games;
using Gambler.Bot.Common.Games.RangeDice;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
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
            set { amount = value; this.RaisePropertyChanged(nameof(Amount));Calculate( nameof(Amount)); }
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
                this.RaisePropertyChanged(nameof(RangePreview)); Calculate(nameof(Min));
            }
        }

        private decimal max = 74.5m;

        public decimal Max
        {
            get { return max; }
            set
            {
                if (max == value) return;
                max = value;
                this.RaisePropertyChanged(nameof(Max));
                this.RaisePropertyChanged(nameof(RangePreview)); Calculate(nameof(Max));
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
                this.RaisePropertyChanged(nameof(RangePreview)); Calculate(nameof(Min2));
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
                this.RaisePropertyChanged(nameof(RangePreview)); Calculate(nameof(Max2));
            }
        }

        private decimal chance = 49.5m;

        public decimal Chance
        {
            get { return chance; }
            set { chance = value; this.RaisePropertyChanged(nameof(Chance)); Calculate(nameof(Chance)); }
        }

        private decimal payout = 2;

        public decimal Payout
        {
            get { return payout; }
            set { payout = value; this.RaisePropertyChanged(nameof(Payout)); Calculate(nameof(Payout)); }
        }
        decimal GetChance()
        {
            decimal chance = 0;

            if (type == RangeDiceType.In)
                chance= max - min - 0.01m;
            if (type == RangeDiceType.Out)
                chance=((GameSettings as RangeDiceConfig)?.MaxRoll??100m) - max + min;
            if (type == RangeDiceType.Double)
                chance=(max - min - 0.01m)+ (max2 - min2 - 0.01m);
            return Math.Round(chance,5);
        }
        bool iscalc = false;
        void AdjustRangeFromChance()
        {
            decimal tmpChance = GetChance();
            while (Chance != tmpChance)
            {
                if (type == RangeDiceType.In || type == RangeDiceType.Double)
                {
                    //If game mode is in, adjust min down for bigger chance and up for smaller. max up for bigger, down for smaller
                    if (max - min >= ((GameSettings as RangeDiceConfig)?.MaxRoll ?? 100m )- (GameSettings?.Edge??1m))
                        return;//this is invalid
                    decimal diff = tmpChance - chance;
                    decimal tmpMin = min + (diff/2m);
                    decimal tmpMax = max - (diff / 2m);
                    if (tmpMin<0)
                    {
                        tmpMax -= tmpMin;
                        tmpMin = 0;
                    }
                    if(tmpMax> ((GameSettings as RangeDiceConfig)?.MaxRoll ?? 100m))
                    {
                        tmpMin += tmpMax - ((GameSettings as RangeDiceConfig)?.MaxRoll ?? 100m);
                        tmpMax = ((GameSettings as RangeDiceConfig)?.MaxRoll ?? 100m);
                    }
                    Min = tmpMin;
                    Max = tmpMax;
                }
                if (type == RangeDiceType.Out)
                {
                    //If game mode is in, adjust min up for bigger chance and down for smaller. max down for bigger, up for smaller
                    
                    decimal diff = tmpChance - chance;
                    decimal tmpMin = min - (diff / 2m);
                    decimal tmpMax = max + (diff / 2m);
                    if (tmpMin < 0)
                    {
                        tmpMax -= tmpMin;
                        tmpMin = 0;
                    }
                    if (tmpMax > ((GameSettings as RangeDiceConfig)?.MaxRoll ?? 100m))
                    {
                        tmpMin += tmpMax - ((GameSettings as RangeDiceConfig)?.MaxRoll ?? 100m);
                        tmpMax = ((GameSettings as RangeDiceConfig)?.MaxRoll ?? 100m);
                    }
                }
                
                tmpChance = GetChance();
            }
        }
        void Calculate(string s)
        {
            if (iscalc)
                return;
            iscalc = true;
            decimal tmpChance = GetChance();
            switch (s)
            {
                case nameof(Type):
                case nameof(Min):
                case nameof(Max):
                case nameof(Min2):
                case nameof(Max2):
                    if (Chance!= tmpChance)
                        Chance = tmpChance;
                    if (Payout != Math.Round((100m - (GameSettings?.Edge ?? 1)) / Chance, 5))
                    {
                        var newPayout = Math.Round((100m - (GameSettings?.Edge ?? 1)) / Chance, 5);
                        Payout = newPayout;
                    }
                    break;
                case nameof(Chance):
                    if (Chance != 0 )
                    {
                        if (Payout != Math.Round((100m - (GameSettings?.Edge ?? 1)) / Chance, 5))
                        {
                            var newPayout = Math.Round((100m - (GameSettings?.Edge ?? 1)) / Chance, 5);
                            Payout = newPayout;
                        }

                        AdjustRangeFromChance();
                        
                    }
                    break;
                case nameof(Payout):
                    if (Payout != 0)
                    {
                        if (Chance != Math.Round((100m - (GameSettings?.Edge ?? 1m)) / Payout,5))
                        {
                            var newChance = Math.Round((100m - (GameSettings?.Edge ?? 1m)) / Payout, 5);
                            Chance = newChance;
                        }
                        AdjustRangeFromChance();
                    }
                    break;
            }
            iscalc = false;
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
                Calculate(nameof(Type));
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
