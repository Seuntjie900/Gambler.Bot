using Gambler.Bot.Common.Helpers;
using Gambler.Bot.Helpers;
using Gambler.Bot.Interfaces;
using Gambler.Bot.Strategies.Helpers;
using System;
using System.IO;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gambler.Bot.Emails
{
    internal class SMTP : IEmailProvider
    {

        string Recipient;
        SmtpClient client;
        public string botName { get; set; }
        public string sender { get; set; }
        public SMTP()
        {

        }
        
        public void LoadSettings(string Folder, string BotName)
        {
            this.botName = BotName;
            string FilePath = Path.Combine(Folder, "SMTP.json");
            if (File.Exists(FilePath))
            {
                string sfile = File.ReadAllText(FilePath);
                SMTPSettings settings = JsonSerializer.Deserialize<SMTPSettings>(sfile); 
                client = new SmtpClient(settings.Host, settings.Port);
                if (!string.IsNullOrWhiteSpace(settings.Username))
                {
                    client.Credentials = new System.Net.NetworkCredential(settings.Username, settings.Password);
                }
                sender = settings.Sender;
                Recipient = settings.Recipient;
            }
            else
            {
                client = new SmtpClient("");
            }
        }

        public async Task SendEmail(Trigger trigger, SiteStats siteStats, SessionStats sessionStats)
        {
            string subject = (botName ?? "Gambler.Bot") + " - " + trigger.ToString();
            string body = $"{(botName ?? "Gambler.Bot")} has triggered a notification{Environment.NewLine}{Environment.NewLine}"+
                $"Trigger: {trigger.ToString()}{Environment.NewLine}{Environment.NewLine}"+
                $"{trigger.PrintResults(siteStats, sessionStats)}";
            await SendSMPT(subject, body);
        }

        public async Task SendEmail(SiteAction siteAction)
        {
            string subject = (botName??"Gambler.Bot") + " - " + siteAction.ToString();
            string body = $"{(botName ?? "Gambler.Bot")} has performed the following action: {siteAction}";
            await SendSMPT(subject, body);
        }

        public async Task SendEmail(ErrorActions errorAction)
        {
            string subject = (botName ?? "Gambler.Bot") + " - " + errorAction.ToString();
            string body = $"{(botName ?? "Gambler.Bot")} has performed the following action: {errorAction}";
            await SendSMPT(subject, body);
        }

        public async Task SendEmail(TriggerAction triggerAction)
        {
            string subject = (botName ?? "Gambler.Bot") + " - " + triggerAction.ToString();
            string body = $"{(botName ?? "Gambler.Bot")} has performed the following action: {triggerAction}";
            await SendSMPT(subject, body);
        }

        async Task SendSMPT(string subject, string body)
        {
            try
            {
                System.Net.Mail.MailMessage message = new System.Net.Mail.MailMessage();
                message.To.Add(this.Recipient);
                message.Subject = subject;
                message.From = new System.Net.Mail.MailAddress(sender);
                message.Body = body;
                message.Sender = new MailAddress( sender);
                await client.SendMailAsync(message);

            }
            catch
            {

            }
        }
    }

    public class SMTPSettings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Sender { get; set; }
        public string Recipient { get; set; }
    }
}
