namespace KKTMonitor.Models
{
    public class ZabbixSettings
    {
        public int Id { get; set; }
        public string? ZabbixServer { get; set; }      // IP или hostname Zabbix сервера
        public int? ZabbixPort { get; set; }           // Порт Zabbix трепера (по умолчанию 10051)
        public string? ZabbixHost { get; set; }        // Имя хоста в Zabbix
        public bool IsEnabled { get; set; } = false;   // Включена ли отправка
        public string? ZabbixKey { get; set; }         // Ключ элементов данных (по умолчанию "kkt.status")
        public DateTime? UpdatedAt { get; set; }
    }
}