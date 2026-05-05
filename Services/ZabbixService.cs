using System.Net.Sockets;
using System.Text;
using KKTMonitor.Models;

namespace KKTMonitor.Services
{
    public class ZabbixService
    {
        private readonly IServiceProvider _serviceProvider;

        public ZabbixService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private async Task<ZabbixSettings?> GetZabbixSettings()
        {
            using var scope = _serviceProvider.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();
            var settings = await settingsService.GetZabbixSettings();
            return settings;
        }

        public async Task<bool> SendKktStatus(string kktIp, string serialNumber, string state, string inn, string legalName)
        {
            try
            {
                var settings = await GetZabbixSettings();
                if (settings == null || !settings.IsEnabled)
                {
                    return false;
                }

                if (string.IsNullOrEmpty(settings.ZabbixServer) || string.IsNullOrEmpty(settings.ZabbixHost))
                {
                    return false;
                }

                string zabbixKey = settings.ZabbixKey ?? "kkt.status";
                
                int statusValue = state switch
                {
                    "OK" => 0,
                    "WARNING" => 1,
                    "DANGER" => 2,
                    _ => 3
                };

                string jsonData = $"{{\"host\":\"{settings.ZabbixHost}\",\"key\":\"{zabbixKey}\",\"value\":{statusValue}}}";
                
                await SendToZabbix(jsonData);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ZABBIX] Ошибка: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> SendToZabbix(string jsonData)
        {
            try
            {
                var settings = await GetZabbixSettings();
                if (settings == null || string.IsNullOrEmpty(settings.ZabbixServer)) return false;

                byte[] data = Encoding.UTF8.GetBytes(jsonData + "\n");
                
                using var client = new TcpClient();
                await client.ConnectAsync(settings.ZabbixServer, settings.ZabbixPort ?? 10051);
                
                using var stream = client.GetStream();
                await stream.WriteAsync(data, 0, data.Length);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ZABBIX] Ошибка отправки: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> TestConnection()
        {
            try
            {
                var settings = await GetZabbixSettings();
                if (settings == null || string.IsNullOrEmpty(settings.ZabbixServer))
                    return false;

                using var client = new TcpClient();
                await client.ConnectAsync(settings.ZabbixServer, settings.ZabbixPort ?? 10051);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ZABBIX] Test connection failed: {ex.Message}");
                return false;
            }
        }
    }
}