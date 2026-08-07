using Gambler.Bot.Classes;
using Gambler.Bot.Common.Helpers;
using Gambler.Bot.Helpers;
using Gambler.Bot.Strategies.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Gambler.Bot.Interfaces
{
    public interface IEmailProvider
    {
        void LoadSettings(string Folder, string botname);
        
        Task SendEmail(Trigger trigger, SiteStats siteStats, SessionStats sessionStats);
        Task SendEmail(SiteAction siteAction);
        Task SendEmail(ErrorActions errorAction);
        Task SendEmail(TriggerAction triggerAction);

        public static Type GetTypeFromName(string Name)
        {
            if (string.IsNullOrWhiteSpace(Name))
                return null;
            Type tInterface = typeof(IEmailProvider);
            Type tProvider = tInterface.Assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && typeof(IEmailProvider).IsAssignableFrom(type) && type.Name == Name).FirstOrDefault();
            return tProvider;
        }
        public static IEmailProvider CreateFromName(string Name)
        {
            Type t = GetTypeFromName(Name);
            return Activator.CreateInstance(t) as IEmailProvider;
            
        }
    }

   
}
