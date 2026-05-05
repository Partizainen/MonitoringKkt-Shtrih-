using System.ComponentModel.DataAnnotations.Schema;

namespace KKTMonitor.Models
{
    public class Settings
    {
        public int Id { get; set; }
        
        [Column("notification_email")]
        public string? NotificationEmail { get; set; }
        
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        
        // Настройки SMTP
        [Column("smtp_server")]
        public string? SmtpServer { get; set; }
        
        [Column("smtp_port")]
        public int? SmtpPort { get; set; }
        
        [Column("smtp_user")]
        public string? SmtpUser { get; set; }
        
        [Column("smtp_password")]
        public string? SmtpPassword { get; set; }
        
        [Column("from_email")]
        public string? FromEmail { get; set; }
        
        [Column("enable_ssl")]
        public bool? EnableSsl { get; set; }
        
        [Column("smtp_encryption")]
        public string? SmtpEncryption { get; set; }
        
        // Настройки интервала опроса
        [Column("poll_interval_value")]
        public int? PollIntervalValue { get; set; } = 1;
        
        [Column("poll_interval_unit")]
        public string? PollIntervalUnit { get; set; } = "hours";
        
        [Column("poll_interval_enabled")]
        public bool? PollIntervalEnabled { get; set; } = true;
        
        // Настройки Zabbix
        [Column("zabbix_server")]
        public string? ZabbixServer { get; set; }
        
        [Column("zabbix_port")]
        public int? ZabbixPort { get; set; } = 10051;
        
        [Column("zabbix_host")]
        public string? ZabbixHost { get; set; }
        
        [Column("zabbix_enabled")]
        public bool? ZabbixEnabled { get; set; } = false;
        
        [Column("zabbix_key")]
        public string? ZabbixKey { get; set; } = "kkt.status";
    }
}