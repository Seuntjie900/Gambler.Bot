using Gambler.Bot.Common.Games;
using Gambler.Bot.Common.Games.Dice;
using Gambler.Bot.ViewModels.Games.Dice;
using Gambler.Bot.ViewModels.Games.Limbo;
using Gambler.Bot.ViewModels.Games.RangeDice;
using Gambler.Bot.ViewModels.Games.Twist;
using Microsoft.Extensions.Logging;
using System;
using static IronPython.Modules._ast;

namespace Gambler.Bot.Classes.BetsPanel
{
    public interface iLiveBet
    {
        void AddBet(Bet newBet);
        event EventHandler<ViewBetEventArgs> BetClicked;

    }

    public interface iPlaceBet
    {
        public Games Game { get; }
        event EventHandler<PlaceBetEventArgs> PlaceBet;
        public IGameConfig GameSettings { get; set; }
        void BetCommand();

        public static iPlaceBet GetFromGame(Games game, ILogger logger)
        {
            switch (game)
            {
                
                case Bot.Common.Games.Games.Crash:
                case Bot.Common.Games.Games.Roulette:
                case Bot.Common.Games.Games.Plinko:
                    break;
                case
                    Bot.Common.Games.Games.Dice:

                    {
                        return new DicePlaceBetViewModel(logger);
                        break;
                    }
                case
                Bot.Common.Games.Games.Twist:

                    {
                        return new TwistPlaceBetViewModel(logger);
                        break;
                    }
                case
                    Bot.Common.Games.Games.Limbo:
                    return new LimboPlaceBetViewModel(logger);
                    break;
                case Bot.Common.Games.Games.RangeDice:
                    return new RangeDicePlaceBetViewModel(logger);
                    break;
            }
            return null;
       
        }
    }

    public interface iBetResult
    {

    }

    public class ViewBetEventArgs : EventArgs
    {
        public Bet BetToView { get; set; }
        public ViewBetEventArgs(Bet bettoview)
        {
            this.BetToView = bettoview;
        }
    }
    public class PlaceBetEventArgs : EventArgs
    {
        public PlaceBet NewBet { get; set; }
        public PlaceBetEventArgs(PlaceBet NewBet)
        {
            this.NewBet = NewBet;
        }
    }

}
