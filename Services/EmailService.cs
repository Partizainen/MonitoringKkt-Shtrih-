using System.Net;
using System.Net.Mail;

namespace KKTMonitor.Services
{
    public class EmailService
    {
        private readonly IServiceProvider _serviceProvider;

        public EmailService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        
        private async Task<SmtpSettingsDto?> GetSmtpSettings()
        {
            using var scope = _serviceProvider.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();
            var settings = await settingsService.GetSettings();
            
            if (settings == null || string.IsNullOrEmpty(settings.SmtpServer))
                return null;
            
            Console.WriteLine("[EMAIL] SMTP Server: " + settings.SmtpServer);
            Console.WriteLine("[EMAIL] SMTP Port: " + settings.SmtpPort);
            Console.WriteLine("[EMAIL] SMTP User: " + settings.SmtpUser);
            Console.WriteLine("[EMAIL] From Email: " + settings.FromEmail);
            Console.WriteLine("[EMAIL] Encryption: " + settings.SmtpEncryption);
            Console.WriteLine("[EMAIL] Has Password: " + !string.IsNullOrEmpty(settings.SmtpPassword));
            
            return new SmtpSettingsDto
            {
                SmtpServer = settings.SmtpServer,
                SmtpPort = settings.SmtpPort ?? 587,
                SmtpUser = settings.SmtpUser,
                SmtpPassword = settings.SmtpPassword,
                FromEmail = settings.FromEmail,
                EnableSsl = settings.EnableSsl ?? true,
                SmtpEncryption = settings.SmtpEncryption ?? "TLS"
            };
        }

        private SmtpClient CreateSmtpClient(SmtpSettingsDto smtpSettings)
        {
            var client = new SmtpClient(smtpSettings.SmtpServer, smtpSettings.SmtpPort ?? 587);
            
            if (!string.IsNullOrEmpty(smtpSettings.SmtpUser) && !string.IsNullOrEmpty(smtpSettings.SmtpPassword))
            {
                client.Credentials = new NetworkCredential(smtpSettings.SmtpUser, smtpSettings.SmtpPassword);
                Console.WriteLine("[EMAIL] Credentials set for: " + smtpSettings.SmtpUser);
            }
            else
            {
                Console.WriteLine("[EMAIL] No credentials provided");
            }
            
            switch (smtpSettings.SmtpEncryption)
            {
                case "SSL":
                    client.EnableSsl = true;
                    client.Port = smtpSettings.SmtpPort ?? 465;
                    Console.WriteLine("[EMAIL] Using SSL on port " + client.Port);
                    break;
                case "TLS":
                    client.EnableSsl = true;
                    Console.WriteLine("[EMAIL] Using TLS on port " + client.Port);
                    break;
                case "None":
                    client.EnableSsl = false;
                    Console.WriteLine("[EMAIL] No encryption");
                    break;
                default:
                    client.EnableSsl = true;
                    Console.WriteLine("[EMAIL] Default TLS");
                    break;
            }
            
            client.Timeout = 30000;
            
            return client;
        }

        public async Task<bool> SendNotificationAsync(string toEmail, string subject, string body)
        {
            try
            {
                Console.WriteLine("[EMAIL] Sending to: " + toEmail);
                Console.WriteLine("[EMAIL] Subject: " + subject);
                
                var smtpSettings = await GetSmtpSettings();
                if (smtpSettings == null)
                {
                    Console.WriteLine("[EMAIL] SMTP not configured");
                    return false;
                }
                
                if (string.IsNullOrEmpty(smtpSettings.FromEmail))
                {
                    Console.WriteLine("[EMAIL] FromEmail is empty");
                    return false;
                }
                
                if (string.IsNullOrEmpty(toEmail))
                {
                    Console.WriteLine("[EMAIL] ToEmail is empty");
                    return false;
                }

                using var client = CreateSmtpClient(smtpSettings);
                using var message = new MailMessage(smtpSettings.FromEmail, toEmail, subject, body)
                {
                    IsBodyHtml = true
                };

                await client.SendMailAsync(message);
                Console.WriteLine("[EMAIL] Sent successfully");
                return true;
            }
            catch (SmtpException ex)
            {
                Console.WriteLine("[EMAIL] SMTP Error: " + ex.StatusCode + " - " + ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EMAIL] Error: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> SendKktAlertAsync(string toEmail, string kktIp, string serialNumber, string oldState, string newState, string reason)
        {
            if (newState != "WARNING" && newState != "DANGER")
            {
                Console.WriteLine("[EMAIL] Skip notification - new state is " + newState);
                return false;
            }
            
            var subject = "⚠️ Изменение состояния ККМ " + (serialNumber ?? kktIp);
            var body = $@"
                <html>
                <head><meta charset='utf-8'></head>
                <body>
                    <h2>Уведомление о состоянии ККМ</h2>
                    <table border='0' cellpadding='5'>
                        <tr><td><b>IP адрес:</b></td><td>{kktIp}</td></tr>
                        <tr><td><b>Заводской номер:</b></td><td>{serialNumber ?? "Неизвестен"}</td></tr>
                        <tr><td><b>Предыдущее состояние:</b></td><td>{oldState}</td></tr>
                        <tr><td><b>Новое состояние:</b></td><td><b style='color:red'>{newState}</b></td></tr>
                        <tr><td><b>Причина:</b></td><td>{reason}</td></tr>
                        <tr><td><b>Время события:</b></td><td>{DateTime.Now:dd.MM.yyyy HH:mm:ss}</td></tr>
                    </table>
                    <hr>
                    <small>KKTMonitor система мониторинга ККМ</small>
                </body>
                </html>";

            return await SendNotificationAsync(toEmail, subject, body);
        }

        public async Task<bool> SendKktStillUnavailableAlertAsync(string toEmail, string kktIp, string serialNumber, string state, int minutes)
        {
            var subject = "⚠️ ККМ " + (serialNumber ?? kktIp) + " недоступен уже " + minutes + " минут";
            var body = $@"
                <html>
                <head><meta charset='utf-8'></head>
                <body>
                    <h2>⚠️ Длительная недоступность ККМ</h2>
                    <table border='0' cellpadding='5'>
                        <tr><td><b>IP адрес:</b></td><td>{kktIp}</td></tr>
                        <tr><td><b>Заводской номер:</b></td><td>{serialNumber ?? "Неизвестен"}</td></tr>
                        <tr><td><b>Текущий статус:</b></td><td><b style='color:red'>{state}</b></td></tr>
                        <tr><td><b>Длительность:</b></td><td>{minutes} минут</td></tr>
                        <tr><td><b>Время проверки:</b></td><td>{DateTime.Now:dd.MM.yyyy HH:mm:ss}</td></tr>
                    </table>
                    <hr>
                    <small>KKTMonitor система мониторинга ККМ</small>
                </body>
                </html>";

            return await SendNotificationAsync(toEmail, subject, body);
        }
    }
}
