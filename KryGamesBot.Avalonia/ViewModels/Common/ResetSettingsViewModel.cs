﻿using DoormatBot.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KryGamesBot.Ava.ViewModels.Common
{
    public class ResetSettingsViewModel:ViewModelBase
    {
		private InternalBetSettings _betSettings;

		public InternalBetSettings BetSettings
        {
			get { return _betSettings; }
			set { _betSettings = value; }
		}

	}
}
