using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using KKTMonitor.Models;

namespace KKTMonitor.Services
{
    public class KktPoller : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly KktDriverService _driver;
        private readonly KktStateService _stateService;
        
        private readonly int _quickPollIntervalSeconds = 60;
        private readonly int _pollTimeoutMinutes = 3;
        
        private readonly Dictionary<string, (string State, DateTime ChangedAt, bool AlertSent)> _stateChangeTracker = new();
        
        private List<string> _activeScheduleTimes = new List<string>();
        private DateTime _lastScheduleCheck = DateTime.MinValue;

        public KktPoller(
            IServiceScopeFactory scopeFactory,
            KktDriverService driver,
            KktStateService stateService)
        {
            _scopeFactory = scopeFactory;
            _driver = driver;
            _stateService = stateService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("KKT Poller started - Quick poll every 60 sec, Full poll by schedule");
            Console.WriteLine($"Poll timeout: {_pollTimeoutMinutes} minutes");
            
            await UpdateScheduleCache();
            
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                
                if ((now - _lastScheduleCheck).TotalMinutes > 5)
                {
                    await UpdateScheduleCache();
                }
                
                bool shouldRunFullPoll = ShouldRunFullPoll(now);
                
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var kktService = scope.ServiceProvider.GetRequiredService<KktService>();
                    var legalService = scope.ServiceProvider.GetRequiredService<LegalEntityService>();
                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                    var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();
                    var zabbixService = scope.ServiceProvider.GetRequiredService<ZabbixService>();
                    
                    var settings = await settingsService.GetSettings();
                    var notificationEmail = settings?.NotificationEmail;
                    
                    var kkts = await kktService.GetAllRaw(false);
                    var activeKkts = kkts.Where(k => !k.IsPollingStopped).ToList();
                    
                    if (shouldRunFullPoll)
                    {
                        Console.WriteLine($"[Poll cycle] {now:HH:mm:ss} - FULL POLL by schedule, KKTs count: {activeKkts.Count}");
                    }
                    else
                    {
                        Console.WriteLine($"[Poll cycle] {now:HH:mm:ss} - Quick poll, KKTs count: {activeKkts.Count}");
                    }
                    
                    foreach (var kkt in activeKkts)
                    {
                        if (string.IsNullOrEmpty(kkt.Ip)) continue;
                        
                        using var pollCts = new CancellationTokenSource(TimeSpan.FromMinutes(_pollTimeoutMinutes));
                        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, pollCts.Token);
                        
                        try
                        {
                            var pollTask = PollSingleKkt(kkt, shouldRunFullPoll, kktService, legalService, emailService, zabbixService, notificationEmail);
                            await pollTask.WaitAsync(linkedCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine($"[Timeout] Poll for {kkt.Ip} exceeded {_pollTimeoutMinutes} minutes");
                            await HandleNetworkTimeout(kkt, kktService, emailService, zabbixService, notificationEmail);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"ERR: {kkt.Ip} - {ex.Message}");
                            await HandleNetworkError(kkt, kktService, emailService, zabbixService, notificationEmail);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Poller global error: {ex.Message}");
                }
                
                await Task.Delay(TimeSpan.FromSeconds(_quickPollIntervalSeconds), stoppingToken);
            }
        }
        
        private async Task UpdateScheduleCache()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var scheduleService = scope.ServiceProvider.GetRequiredService<ScheduleService>();
                _activeScheduleTimes = await scheduleService.GetActiveScheduleTimes();
                _lastScheduleCheck = DateTime.Now;
                Console.WriteLine($"[Schedule] Updated schedule cache: {(_activeScheduleTimes.Count > 0 ? string.Join(", ", _activeScheduleTimes) : "no active schedules")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Schedule] Error updating schedule cache: {ex.Message}");
            }
        }
        
        private bool ShouldRunFullPoll(DateTime now)
        {
            string currentTime = now.ToString("HH:mm");
            return _activeScheduleTimes.Contains(currentTime);
        }
        
        private async Task PollSingleKkt(Kkt kkt, bool needFullPoll, KktService kktService, 
            LegalEntityService legalService, EmailService emailService, ZabbixService zabbixService, string? notificationEmail)
        {
            string oldState = kkt.State ?? "UNKNOWN";
            string newState = oldState;
            Kkt? data = null;
            
            if (needFullPoll)
            {
                Console.WriteLine($"[Full poll by schedule] {kkt.Ip}...");
                data = await _driver.PollFull(kkt.Ip ?? "");
                
                if (data != null)
                {
                    data.LastCheck = DateTime.Now;
                    data.LastFullPoll = DateTime.Now;
                    data.IsPollingStopped = kkt.IsPollingStopped;
                    
                    if (!string.IsNullOrEmpty(data.INN))
                    {
                        string normalizedInn = data.INN.TrimStart('0');
                        var legal = await legalService.GetByInn(normalizedInn);
                        if (legal != null)
                        {
                            data.LegalEntityId = legal.Id;
                        }
                        else
                        {
                            data.LegalEntityId = kkt.LegalEntityId;
                        }
                    }
                    else
                    {
                        data.LegalEntityId = kkt.LegalEntityId;
                    }
                    
                    await kktService.UpdateKktData(data);
                    newState = _stateService.Calculate(data);
                    await kktService.UpdateKktState(data, newState);
                    
                    Console.WriteLine($"[Full poll by schedule] {kkt.Ip}: OK, State={newState}");
                }
            }
            else
            {
                bool isOnline = await _driver.CheckNetworkAccessibility(kkt.Ip ?? "");
                string newKktStatus = isOnline ? "ONLINE" : "OFFLINE";
                
                newState = _stateService.CalculateOverallStatus(
                    kkt.State ?? "OK",
                    isOnline,
                    kkt.LastFullPoll
                );
                
                await kktService.UpdateKktStatus(kkt.Ip ?? "", newKktStatus, newState);
                
                Console.WriteLine($"[Quick poll] {kkt.Ip}: {newKktStatus}, State={newState}");
            }
            
            // Отправка email уведомлений
            if (!string.IsNullOrEmpty(notificationEmail) && newState != oldState && (newState == "WARNING" || newState == "DANGER"))
            {
                string reason = _stateService.GetStateChangeReason(data ?? kkt, newState);
                await emailService.SendKktAlertAsync(
                    notificationEmail,
                    kkt.Ip ?? "",
                    kkt.SerialNumber ?? kkt.Ip ?? "",
                    oldState,
                    newState,
                    reason
                );
            }
            
            // Отправка статуса в Zabbix
            if (newState != oldState)
            {
                await zabbixService.SendKktStatus(
                    kkt.Ip ?? "",
                    kkt.SerialNumber ?? "",
                    newState,
                    kkt.INN ?? "",
                    ""
                );
            }
        }
        
        private async Task HandleNetworkTimeout(Kkt kkt, KktService kktService, 
            EmailService emailService, ZabbixService zabbixService, string? notificationEmail)
        {
            string oldState = kkt.State ?? "UNKNOWN";
            
            string newState = _stateService.CalculateOverallStatus(
                kkt.State ?? "OK",
                false,
                kkt.LastFullPoll
            );
            
            await kktService.UpdateKktStatus(kkt.Ip ?? "", "TIMEOUT", newState);
            
            if (!string.IsNullOrEmpty(notificationEmail) && newState != oldState && (newState == "WARNING" || newState == "DANGER"))
            {
                await emailService.SendKktAlertAsync(
                    notificationEmail,
                    kkt.Ip ?? "",
                    kkt.SerialNumber ?? kkt.Ip ?? "",
                    oldState,
                    newState,
                    "Превышен таймаут опроса ККМ"
                );
            }
            
            if (newState != oldState)
            {
                await zabbixService.SendKktStatus(
                    kkt.Ip ?? "",
                    kkt.SerialNumber ?? "",
                    newState,
                    kkt.INN ?? "",
                    ""
                );
            }
        }
        
        private async Task HandleNetworkError(Kkt kkt, KktService kktService, 
            EmailService emailService, ZabbixService zabbixService, string? notificationEmail)
        {
            string oldState = kkt.State ?? "UNKNOWN";
            
            string newState = _stateService.CalculateOverallStatus(
                kkt.State ?? "OK",
                false,
                kkt.LastFullPoll
            );
            
            await kktService.UpdateKktStatus(kkt.Ip ?? "", "ERROR", newState);
            
            if (!string.IsNullOrEmpty(notificationEmail) && newState != oldState && (newState == "WARNING" || newState == "DANGER"))
            {
                await emailService.SendKktAlertAsync(
                    notificationEmail,
                    kkt.Ip ?? "",
                    kkt.SerialNumber ?? kkt.Ip ?? "",
                    oldState,
                    newState,
                    "Ошибка при опросе ККМ"
                );
            }
            
            if (newState != oldState)
            {
                await zabbixService.SendKktStatus(
                    kkt.Ip ?? "",
                    kkt.SerialNumber ?? "",
                    newState,
                    kkt.INN ?? "",
                    ""
                );
            }
        }
    }
}