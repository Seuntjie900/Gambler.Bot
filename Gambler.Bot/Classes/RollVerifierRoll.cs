﻿namespace Gambler.Bot.Classes
{
    public class RollVerifierRoll
    {
        public string ServerSeed { get; set; }
        public string ClientSeed { get; set; }
        public int Nonce { get; set; }
        public string Result { get; set; }
    }
}