﻿using Avalonia.Threading;
using Gambler.Bot.Classes;
using Gambler.Bot.Common.Events;
using Gambler.Bot.Common.Games;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Input;

namespace Gambler.Bot.ViewModels.Common
{
    public class SimulationViewModel:ViewModelBase
    {
        
        public event EventHandler<CanSimulateEventArgs> CanStart;
        Stopwatch SimTimer = new Stopwatch();
        private ProfitChartViewModel chrt;

        public ProfitChartViewModel Chart
        {
            get { return chrt; }
            set { chrt = value; }
        }


        private AutoBet bot;

        public AutoBet Bot
        {
            get { return bot; }
            set { bot = value; Game = bot?.CurrentGame ?? Gambler.Bot.Common.Games.Games.Dice; }
        }


        private Gambler.Bot.Strategies.Helpers.Simulation simulation;

        public Gambler.Bot.Strategies.Helpers.Simulation CurrentSimulation
        {
            get { return simulation; }
            set { simulation = value; }
        }

        private SessionStatsViewModel statsViewModel;

        public SessionStatsViewModel Stats
        {
            get { return statsViewModel; }
            set { statsViewModel = value; }
        }

        private decimal startingBalance=1;

        public decimal StartingBalance
        {
            get { return startingBalance; }
            set { startingBalance = value; }
        }

        private long numberOfBets=1000000;

        public long NumberOfBets
        {
            get { return numberOfBets; }
            set { numberOfBets = value; }
        }

        private decimal progress;

        public decimal Progress
        {
            get { return progress; }
            set { progress = value; this.RaisePropertyChanged(); }
        }

        public decimal Balance { get => CurrentSimulation?.Balance ?? 0; }
        public TimeSpan TimeRunning { get; set; }
        public TimeSpan ProjectedTime { get; set; }
        public TimeSpan ProjectedRemaining { get; set; }

        public Gambler.Bot.Common.Games.Games Game { get; set; }

        public SimulationViewModel(ILogger logger):base(logger)
        {
           StartCommand = ReactiveCommand.Create(Start);
            StopCommand = ReactiveCommand.Create(Stop);
            SaveCommand = ReactiveCommand.Create(Save);
            Stats = new SessionStatsViewModel(logger);
            Chart = new ProfitChartViewModel(logger) { Enabled = false, MaxItems=10000 };
        }

        private bool canSave;

        public bool CanSave
        {
            get { return canSave; }
            set { canSave = value; this.RaisePropertyChanged(); }
        }

        private bool log=true;

        public bool Log
        {
            get { return log; }
            set { log = value; this.RaisePropertyChanged(); }
        }

        private bool running;

        public bool Running
        {
            get { return running; }
            set { running = value; this.RaisePropertyChanged(); this.RaisePropertyChanged(nameof(NotRunning)); }
        }

        public bool NotRunning
        {
            get { return !Running; }            
        }

        public ICommand StopCommand { get; }
        void Stop()
        {
            CurrentSimulation?.StopSim();
        }

        private string error;

        public string Error
        {
            get { return error; }
            set { error = value; this.RaisePropertyChanged(); this.RaisePropertyChanged(nameof(showError)); }
        }
        public bool showError { get => !string.IsNullOrWhiteSpace(Error); }

        public ICommand StartCommand { get; }
        void Start()
        {
            var args = new CanSimulateEventArgs() { CanSimulate = true };
            CanStart?.Invoke(this, args);
            if (!args.CanSimulate)
            {
                Error = "Cannot start simulation. The bot might be betting or running a simulation in the programmer mode.";
                return;
            }
            if (Running )
            {
                Error = "Simulation already running";
                
                return;
            }
            Error = "";
            CurrentSimulation = bot.InitializeSim(startingBalance, NumberOfBets, "tmp.csv",Log);
            
            CanSave = false;
            CurrentSimulation.OnSimulationWriting += CurrentSimulation_OnSimulationWriting;
            CurrentSimulation.OnSimulationComplete += CurrentSimulation_OnSimulationComplete;
            CurrentSimulation.OnBetSimulated += CurrentSimulation_OnBetSimulated;
            SimTimer.Start();
            Running = true;
            CanSave = false;
            CurrentSimulation.Start(Game);
            Stats.Stats = CurrentSimulation.Stats;
            Chart.Reset();
            //chrt.MaxItems = 10000;
        }
        List<decimal> Bets = new List<decimal>();
        DateTime LastCheck = DateTime.Today;
        long counter = 0;
        private void CurrentSimulation_OnBetSimulated(object? sender, BetFinisedEventArgs e)
        {

            Bets.Add(e.NewBet.Profit);
            if (chrt.Enabled)
            {
                //chrt.AddPoint(e.NewBet.Profit, e.NewBet.IsWin);
                if (Bets.Count % 100 == 0)
                {
                    chrt.AddRange(Bets).Wait();
                    //Thread.Sleep(10);

                    Bets.Clear();
                }
            }
            if (simulation.TotalBetsPlaced % 10000 == 0 || (DateTime.Now - LastCheck).Seconds >=1)
            {
                LastCheck = DateTime.Now;
                UpdateStats();
                Bets.Clear();
            }
            /*if (Bets.Count > 100)
                Bets.RemoveAt(0);*/
        }

        private void CurrentSimulation_OnSimulationComplete(object? sender, EventArgs e)
        {
            Finished();
        }

        void Finished()
        {
            if (!Dispatcher.UIThread.CheckAccess())
                Dispatcher.UIThread.Invoke(Finished);
            else
            {

                SimTimer.Stop();
                Gambler.Bot.Strategies.Helpers.Simulation tmp = CurrentSimulation;
                Stats.StatsUpdated(tmp.Stats);
                long ElapsedMilliseconds = SimTimer.ElapsedMilliseconds;
                if ((decimal)tmp.Bets == 0)
                { 
                    Progress = (decimal)tmp.TotalBetsPlaced / (decimal)tmp.Bets;
                    decimal totaltime = ElapsedMilliseconds / Progress;
                    TimeRunning = TimeSpan.FromMilliseconds(ElapsedMilliseconds);
                    ProjectedTime = TimeSpan.FromMilliseconds((double)totaltime);
                    ProjectedRemaining = TimeSpan.FromMilliseconds((double)totaltime - ElapsedMilliseconds);
                }

                this.RaisePropertyChanged(nameof(Progress));
                this.RaisePropertyChanged(nameof(TimeRunning));
                this.RaisePropertyChanged(nameof(ProjectedTime));
                this.RaisePropertyChanged(nameof(ProjectedRemaining));
                this.RaisePropertyChanged(nameof(CurrentSimulation));

                Running = false;
                SimTimer.Reset();
                CanSave = true;
            }
        }

        

        void UpdateStats()
        {
            if (!Dispatcher.UIThread.CheckAccess())
                Dispatcher.UIThread.Invoke(UpdateStats);
            else
            {
                Gambler.Bot.Strategies.Helpers.Simulation tmp = CurrentSimulation;
                //Console.WriteLine("Simulation Progress: " + tmp.TotalBetsPlaced + " bets of " + tmp.Bets);

                if (tmp.TotalBetsPlaced > 0)
                {
                    long ElapsedMilliseconds = SimTimer.ElapsedMilliseconds;
                    progress = (decimal)tmp.TotalBetsPlaced / (decimal)tmp.Bets;

                    decimal totaltime = ElapsedMilliseconds / Progress;
                    TimeRunning = TimeSpan.FromMilliseconds(ElapsedMilliseconds);
                    ProjectedTime = TimeSpan.FromMilliseconds((double)totaltime);
                    ProjectedRemaining = TimeSpan.FromMilliseconds((double)totaltime - ElapsedMilliseconds);

                    this.RaisePropertyChanged(nameof(Balance));
                    this.RaisePropertyChanged(nameof(Progress));
                    this.RaisePropertyChanged(nameof(TimeRunning));
                    this.RaisePropertyChanged(nameof(ProjectedTime));
                    this.RaisePropertyChanged(nameof(ProjectedRemaining));
                    this.RaisePropertyChanged(nameof(CurrentSimulation));
                    Stats.StatsUpdated(CurrentSimulation.Stats);
                }
            }
        }

        private void CurrentSimulation_OnSimulationWriting(object? sender, EventArgs e)
        {
            
            
        }

        public ICommand SaveCommand { get; }
        public void Save()
        {
           /* SaveFileDialog dg = new SaveFileDialog();
            if (dg.ShowDialog() ?? false)
            {
                CurrentSimulation.MoveLog(dg.FileName);
            }*/
        }

        public void Save(string path)
        {
            CurrentSimulation.MoveLog(path);
        }
        
    }
    public class CanSimulateEventArgs : EventArgs
    {
        public bool CanSimulate { get; set; }
    }
}
