using Microsoft.AspNetCore.Mvc;
using KKTMonitor.Services;

namespace KKTMonitor.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly SettingsService _settingsService;
        private readonly IServiceProvider _serviceProvider;

        public SettingsController(SettingsService settingsService, IServiceProvider serviceProvider)
        {
            _settingsService = settingsService;
            _serviceProvider = serviceProvider;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var settings = await _settingsService.GetSettings();
            
            if (settings == null)
            {
                return Ok(new { notificationEmail = "", hasSmtpSettings = false });
            }
            
            return Ok(new { 
                notificationEmail = settings.NotificationEmail ?? "",
                hasSmtpSettings = !string.IsNullOrEmpty(settings.SmtpServer)
            });
        }

        [HttpPost("notificationEmail")]
        public async Task<IActionResult> UpdateNotificationEmail([FromBody] string email)
        {
            if (string.IsNullOrEmpty(email))
                return BadRequest(new { error = "Email не может быть пустым" });

            if (!email.Contains('@') || !email.Contains('.'))
                return BadRequest(new { error = "Введите корректный email адрес" });

            var success = await _settingsService.UpdateNotificationEmail(email);
            if (success)
                return Ok(new { success = true, email });
            else
                return BadRequest(new { error = "Ошибка сохранения email" });
        }
        
        [HttpPost("smtp")]
        public async Task<IActionResult> UpdateSmtpSettings([FromBody] SmtpSettingsDto smtpSettings, [FromQuery] string password)
        {
            if (!await _settingsService.ValidatePassword(password))
                return Unauthorized(new { error = "Неверный пароль" });
            
            if (string.IsNullOrEmpty(smtpSettings.SmtpServer))
                return BadRequest(new { error = "SMTP сервер не может быть пустым" });
            
            var success = await _settingsService.UpdateSmtpSettings(smtpSettings);
            
            if (success)
                return Ok(new { success = true });
            else
                return BadRequest(new { error = "Ошибка сохранения настроек SMTP" });
        }
        
        [HttpGet("smtp")]
        public async Task<IActionResult> GetSmtpSettings([FromQuery] string password)
        {
            if (!await _settingsService.ValidatePassword(password))
                return Unauthorized(new { error = "Неверный пароль" });
            
            var settings = await _settingsService.GetSettings();
            
            bool hasPassword = !string.IsNullOrEmpty(settings?.SmtpPassword);
            
            return Ok(new { 
                smtpServer = settings?.SmtpServer ?? "",
                smtpPort = settings?.SmtpPort ?? 587,
                smtpUser = settings?.SmtpUser ?? "",
                fromEmail = settings?.FromEmail ?? "",
                enableSsl = settings?.EnableSsl ?? true,
                smtpEncryption = settings?.SmtpEncryption ?? "TLS",
                hasPassword = hasPassword
            });
        }
        
        [HttpGet("pollInterval")]
        public async Task<IActionResult> GetPollInterval()
        {
            var settings = await _settingsService.GetSettings();
            
            return Ok(new
            {
                intervalValue = settings?.PollIntervalValue ?? 1,
                intervalUnit = settings?.PollIntervalUnit ?? "hours",
                isEnabled = settings?.PollIntervalEnabled ?? true
            });
        }
        
        [HttpPost("pollInterval")]
        public async Task<IActionResult> UpdatePollInterval([FromBody] PollIntervalSettings interval)
        {
            if (interval.IntervalValue <= 0)
                return BadRequest(new { error = "Интервал должен быть больше 0" });
            
            if (interval.IntervalUnit != "minutes" && interval.IntervalUnit != "hours")
                return BadRequest(new { error = "Единица измерения должна быть minutes или hours" });
            
            var success = await _settingsService.UpdatePollInterval(interval.IntervalValue, interval.IntervalUnit, interval.IsEnabled);
            
            if (success)
                return Ok(new { success = true });
            else
                return BadRequest(new { error = "Ошибка сохранения интервала опроса" });
        }
        
        [HttpPost("testEmail")]
        public async Task<IActionResult> SendTestEmail([FromBody] TestEmailRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Email))
                return BadRequest(new { error = "Email не указан" });
            
            if (!request.Email.Contains('@') || !request.Email.Contains('.'))
                return BadRequest(new { error = "Введите корректный email адрес" });
            
            var settings = await _settingsService.GetSettings();
            
            if (settings == null || string.IsNullOrEmpty(settings.SmtpServer))
            {
                return BadRequest(new { error = "SMTP сервер не настроен. Сначала настройте SMTP в разделе ниже." });
            }
            
            if (string.IsNullOrEmpty(settings.FromEmail))
            {
                return BadRequest(new { error = "Email отправителя не настроен. Укажите его в настройках SMTP." });
            }
            
            if (string.IsNullOrEmpty(settings.SmtpPassword))
            {
                return BadRequest(new { error = "Пароль SMTP не настроен. Укажите его в настройках SMTP." });
            }
            
            var emailService = _serviceProvider.GetService<EmailService>();
            if (emailService == null)
                return BadRequest(new { error = "Email сервис не доступен" });
            
            var result = await emailService.SendNotificationAsync(
                request.Email,
                "Тестовое письмо мониторинга ККТ",
                "<h3>Тестовое письмо</h3><p>Это тестовое письмо от системы мониторинга ККТ.</p><p>Если вы получили это письмо, значит настройки SMTP выполнены корректно.</p><hr><p><small>KKTMonitor система мониторинга контрольно-кассовых машин</small></p>"
            );
            
            if (result)
            {
                return Ok(new { success = true, message = "Тестовое письмо отправлено" });
            }
            else
            {
                return BadRequest(new { error = "Не удалось отправить письмо. Проверьте настройки SMTP (сервер, порт, логин, пароль)." });
            }
        }
        
        // Zabbix методы
        [HttpGet("zabbix")]
        public async Task<IActionResult> GetZabbixSettings([FromQuery] string password)
        {
            if (!await _settingsService.ValidatePassword(password))
                return Unauthorized(new { error = "Неверный пароль" });
            
            var settings = await _settingsService.GetZabbixSettings();
            
            return Ok(new { 
                zabbixServer = settings?.ZabbixServer ?? "",
                zabbixPort = settings?.ZabbixPort ?? 10051,
                zabbixHost = settings?.ZabbixHost ?? "",
                zabbixEnabled = settings?.IsEnabled ?? false,
                zabbixKey = settings?.ZabbixKey ?? "kkt.status"
            });
        }

        [HttpPost("zabbix")]
        public async Task<IActionResult> UpdateZabbixSettings([FromBody] ZabbixSettingsDto zabbixSettings, [FromQuery] string password)
        {
            if (!await _settingsService.ValidatePassword(password))
                return Unauthorized(new { error = "Неверный пароль" });
            
            var success = await _settingsService.UpdateZabbixSettings(zabbixSettings);
            
            if (success)
                return Ok(new { success = true });
            else
                return BadRequest(new { error = "Ошибка сохранения настроек Zabbix" });
        }

        [HttpPost("zabbix/test")]
        public async Task<IActionResult> TestZabbixConnection([FromQuery] string password)
        {
            if (!await _settingsService.ValidatePassword(password))
                return Unauthorized(new { error = "Неверный пароль" });
            
            var zabbixService = _serviceProvider.GetService<ZabbixService>();
            if (zabbixService == null)
                return BadRequest(new { error = "Zabbix сервис не доступен" });
            
            var result = await zabbixService.TestConnection();
            
            if (result)
                return Ok(new { success = true, message = "Подключение к Zabbix успешно" });
            else
                return BadRequest(new { error = "Не удалось подключиться к Zabbix серверу" });
        }
    }
    
    public class PollIntervalSettings
    {
        public int IntervalValue { get; set; } = 1;
        public string IntervalUnit { get; set; } = "hours";
        public bool IsEnabled { get; set; } = true;
    }
    
    public class TestEmailRequest
    {
        public string? Email { get; set; }
    }
}