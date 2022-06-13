/**********************************************************************************
Veritas: Copyright (c) 2017 Veritas Technologies LLC. All rights reserved.
EV29-2660-5195-04-15-1
**********************************************************************************/
namespace DbDifferenceGenerator
{
    using System;
    using System.Net.Mail;
    using System.Threading.Tasks;


    /// <summary>
    /// Class used to implement the SendMailMEssages.
    /// </summary>
    public class SMTPMailSenderService
    {
        /// <summary>
        /// Creates the mail addres.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns></returns>
        public MailAddress CreateMailAddres(string address)
        {
            return new MailAddress(address);
        }

        public async Task SendMessage(MailMessage msg, string smtpServer, int port)
        {
            var smtpClient = new SmtpClient(smtpServer, port);
            
            // SMTP server sometimes does not seem to respond on first try.
            // So, do retries with more waiting time on each loop
            const int maxRetries = 3;
            for (var i = 0; i <= maxRetries; i++)
            {
                try
                {
                    var loopDelay = i * 1000;
                    if (loopDelay > 0)
                        System.Threading.Tasks.Task.Delay(1500 + loopDelay).Wait();
                    
                    await smtpClient.SendMailAsync(msg);
                    break;
                }
                catch (Exception ex)
                {
                    if (i == maxRetries)
                        throw;
                }
            }
        }

        public async Task SendMessage(string fromAddress, string toAddress, string subject, string body, bool html, string smtpServer, int port)
        {
            using (var msg = new MailMessage())
            {
                msg.From = new MailAddress(fromAddress);
                msg.To.Add(new MailAddress(toAddress));
                msg.Subject = subject;
                msg.Body = body;
                msg.IsBodyHtml = html;

                await SendMessage(msg, smtpServer, port);
            }
        }

    }
}
