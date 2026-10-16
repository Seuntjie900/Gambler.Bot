using Gambler.Bot.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Gambler.Bot.Classes
{
    class InstanceSettings
    {
        public string Site { get; set; }
        public string Username { get; set; }
        public string KeepassId { get; set; }
        public bool AutoLogin { get; set; }
        public string Game { get; set; }
        public string Currency { get; set; }
        public string ConnectionMode { get; set; }
    }
}
