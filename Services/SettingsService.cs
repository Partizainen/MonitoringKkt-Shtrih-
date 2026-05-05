using Dapper;
using KKTMonitor.Models;
using System.Data;

namespace KKTMonitor.Services
{
    public class SettingsService
    {
        private readonly DbContext _db;

        public SettingsService(DbContext db)
        {
            _db = db;
        }

        private IDbConnection CreateConnection() => _db.CreateConnection();

        public async Task<Settings?> GetSettings()
        {
            using var conn = CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Settings>(@"
                SELECT 
                    id AS Id,
                    notification_email AS NotificationEmail,
                    updated_at AS UpdatedAt,
                    smtp_server AS SmtpServer,
                    smtp_port AS SmtpPort,
                    smtp_user AS SmtpUser,
                    smtp_password AS SmtpPassword,
                    from_email AS FromEmail,
                    enable_ssl AS EnableSsl,
                    smtp_encryption AS SmtpEncryption,
                    poll_interval_value AS PollIntervalValue,
                    poll_interval_unit AS PollIntervalUnit,
                    poll_interval_enabled AS PollIntervalEnabled,
                    zabbix_server AS ZabbixServer,
                    zabbix_port AS ZabbixPort,
                    zabbix_host AS ZabbixHost,
                    zabbix_enabled AS ZabbixEnabled,
                    zabbix_key AS ZabbixKey
                FROM settings WHERE id = 1");
        }

        public async Task<bool> UpdateNotificationEmail(string email)
        {
            using var conn = CreateConnection();
            
            var exists = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM settings WHERE id = 1");
            
            if (exists == 0)
            {
                var inserted = await conn.ExecuteAsync(@"
                    INSERT INTO settings (id, notification_email, updated_at)
                    VALUES (1, @email, @now)",
                    new { email, now = DateTime.Now });
                return inserted > 0;
            }
            else
            {
                var updated = await conn.ExecuteAsync(@"
                    UPDATE settings 
                    SET notification_email = @email, updated_at = @now
                    WHERE id = 1",
                    new { email, now = DateTime.Now });
                return updated > 0;
            }
        }

        public async Task<bool> UpdateSmtpSettings(SmtpSettingsDto smtpSettings)
        {
            using var conn = CreateConnection();
            
            var exists = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM settings WHERE id = 1");
            
            string passwordToSave = smtpSettings.SmtpPassword;
            
            if (string.IsNullOrEmpty(passwordToSave))
            {
                var current = await conn.QueryFirstOrDefaultAsync<Settings>("SELECT smtp_password FROM settings WHERE id = 1");
                passwordToSave = current?.SmtpPassword ?? "";
            }
            
            if (exists == 0)
            {
                var inserted = await conn.ExecuteAsync(@"
                    INSERT INTO settings (id, smtp_server, smtp_port, smtp_user, smtp_password, from_email, smtp_encryption, enable_ssl, updated_at)
                    VALUES (1, @SmtpServer, @SmtpPort, @SmtpUser, @SmtpPassword, @FromEmail, @SmtpEncryption, 1, @now)",
                    new { 
                        smtpSettings.SmtpServer, 
                        smtpSettings.SmtpPort, 
                        smtpSettings.SmtpUser, 
                        SmtpPassword = passwordToSave,
                        smtpSettings.FromEmail, 
                        smtpSettings.SmtpEncryption,
                        now = DateTime.Now 
                    });
                return inserted > 0;
            }
            else
            {
                var updated = await conn.ExecuteAsync(@"
                    UPDATE settings 
                    SET smtp_server = @SmtpServer, 
                        smtp_port = @SmtpPort, 
                        smtp_user = @SmtpUser, 
                        smtp_password = @SmtpPassword, 
                        from_email = @FromEmail, 
                        smtp_encryption = @SmtpEncryption,
                        enable_ssl = 1,
                        updated_at = @now
                    WHERE id = 1",
                    new { 
                        smtpSettings.SmtpServer, 
                        smtpSettings.SmtpPort, 
                        smtpSettings.SmtpUser, 
                        SmtpPassword = passwordToSave,
                        smtpSettings.FromEmail, 
                        smtpSettings.SmtpEncryption,
                        now = DateTime.Now 
                    });
                return updated > 0;
            }
        }
        
        public async Task<bool> UpdatePollInterval(int value, string unit, bool enabled)
        {
            using var conn = CreateConnection();
            
            var exists = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM settings WHERE id = 1");
            
            if (exists == 0)
            {
                var inserted = await conn.ExecuteAsync(@"
                    INSERT INTO settings (id, poll_interval_value, poll_interval_unit, poll_interval_enabled, updated_at)
                    VALUES (1, @value, @unit, @enabled, @now)",
                    new { value, unit, enabled, now = DateTime.Now });
                return inserted > 0;
            }
            else
            {
                var updated = await conn.ExecuteAsync(@"
                    UPDATE settings 
                    SET poll_interval_value = @value, poll_interval_unit = @unit, poll_interval_enabled = @enabled, updated_at = @now
                    WHERE id = 1",
                    new { value, unit, enabled, now = DateTime.Now });
                return updated > 0;
            }
        }
        
        // Zabbix методы
        public async Task<ZabbixSettings?> GetZabbixSettings()
        {
            using var conn = CreateConnection();
            var settings = await conn.QueryFirstOrDefaultAsync<Settings>("SELECT * FROM settings WHERE id = 1");
            
            if (settings == null) return null;
            
            return new ZabbixSettings
            {
                Id = settings.Id,
                ZabbixServer = settings.ZabbixServer,
                ZabbixPort = settings.ZabbixPort ?? 10051,
                ZabbixHost = settings.ZabbixHost,
                IsEnabled = settings.ZabbixEnabled ?? false,
                ZabbixKey = settings.ZabbixKey ?? "kkt.status",
                UpdatedAt = settings.UpdatedAt
            };
        }

        public async Task<bool> UpdateZabbixSettings(ZabbixSettingsDto zabbixSettings)
        {
            using var conn = CreateConnection();
            
            var exists = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM settings WHERE id = 1");
            
            if (exists == 0)
            {
                var inserted = await conn.ExecuteAsync(@"
                    INSERT INTO settings (id, zabbix_server, zabbix_port, zabbix_host, zabbix_enabled, zabbix_key, updated_at)
                    VALUES (1, @ZabbixServer, @ZabbixPort, @ZabbixHost, @ZabbixEnabled, @ZabbixKey, @now)",
                    new { 
                        zabbixSettings.ZabbixServer, 
                        zabbixSettings.ZabbixPort, 
                        zabbixSettings.ZabbixHost, 
                        zabbixSettings.ZabbixEnabled, 
                        zabbixSettings.ZabbixKey,
                        now = DateTime.Now 
                    });
                return inserted > 0;
            }
            else
            {
                var updated = await conn.ExecuteAsync(@"
                    UPDATE settings 
                    SET zabbix_server = @ZabbixServer, 
                        zabbix_port = @ZabbixPort, 
                        zabbix_host = @ZabbixHost, 
                        zabbix_enabled = @ZabbixEnabled,
                        zabbix_key = @ZabbixKey,
                        updated_at = @now
                    WHERE id = 1",
                    new { 
                        zabbixSettings.ZabbixServer, 
                        zabbixSettings.ZabbixPort, 
                        zabbixSettings.ZabbixHost, 
                        zabbixSettings.ZabbixEnabled,
                        zabbixSettings.ZabbixKey,
                        now = DateTime.Now 
                    });
                return updated > 0;
            }
        }
        
        public Task<bool> ValidatePassword(string password)
        {
            return Task.FromResult(password == "MonitoringKkt");
        }
    }
    
    public class SmtpSettingsDto
    {
        public string? SmtpServer { get; set; }
        public int? SmtpPort { get; set; }
        public string? SmtpUser { get; set; }
        public string? SmtpPassword { get; set; }
        public string? FromEmail { get; set; }
        public bool? EnableSsl { get; set; }
        public string? SmtpEncryption { get; set; }
    }
    
    public class ZabbixSettings
    {
        public int Id { get; set; }
        public string? ZabbixServer { get; set; }
        public int? ZabbixPort { get; set; }
        public string? ZabbixHost { get; set; }
        public bool IsEnabled { get; set; }
        public string? ZabbixKey { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
    
    public class ZabbixSettingsDto
    {
        public string? ZabbixServer { get; set; }
        public int? ZabbixPort { get; set; }
        public string? ZabbixHost { get; set; }
        public bool ZabbixEnabled { get; set; }
        public string? ZabbixKey { get; set; }
    }
}