﻿using DoormatBot.Helpers;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KryGamesBot.Ava.ViewModels.Common
{
    public class AdvancedViewModel:ViewModelBase
    {
		private InternalBetSettings _settings;

		public InternalBetSettings BetSettings
		{
			get { return _settings; }
			set { _settings = value; this.RaisePropertyChanged(); }
		}
        List<string> Actions = new List<string> { "Withdraw", "Tip", "Stop" };
        List<string> Compares = new List<string> { "Profit", "Balance" };

    }
}
