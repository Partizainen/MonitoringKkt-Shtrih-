using Microsoft.AspNetCore.Mvc;
using KKTMonitor.Services;
using KKTMonitor.Models;
using Dapper;

#pragma warning disable CS8601
#pragma warning disable CS8604

namespace KKTMonitor.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KktController : ControllerBase
    {
        private readonly KktService _service;
        private readonly KktDriverService _driver;
        private readonly IServiceProvider _serviceProvider;

        public KktController(KktService service, KktDriverService driver, IServiceProvider serviceProvider)
        {
            _service = service;
            _driver = driver;
            _serviceProvider = serviceProvider;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] bool includeInactive = false)
        {
            var data = await _service.GetAllWithLegalName(includeInactive);
            return Ok(data);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var kkt = await _service.GetById(id);
            if (kkt == null)
                return NotFound();
            
            Console.WriteLine($"[API] GetById {id}: SerialNumber={kkt.SerialNumber}, State={kkt.State}");
            
            return Ok(kkt);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] KktCreateDto kktDto)
        {
            if (string.IsNullOrEmpty(kktDto.Ip))
                return BadRequest(new { error = "IP адрес обязателен" });
            
            var kkt = new Kkt
            {
                SerialNumber = kktDto.SerialNumber,
                Nickname = kktDto.Nickname,
                Ip = kktDto.Ip,
                Source = kktDto.Source ?? "manual",
                LastSeen = DateTime.Now,
                IsActive = kktDto.IsActive,
                LegalEntityId = kktDto.LegalEntityId,
                INN = kktDto.Inn
            };
            
            using var conn = _service.CreateConnection();
            var id = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO kkt (serial_number, nickname, ip, source, last_seen, is_active, legal_entity_id, inn)
                VALUES (@SerialNumber, @Nickname, @Ip, @Source, @LastSeen, @IsActive, @LegalEntityId, @INN);
                SELECT LAST_INSERT_ID();", kkt);
            
            return Ok(new { id, success = true });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] KktUpdateDto kktDto)
        {
            var kkt = await _service.GetKktById(id);
            if (kkt == null)
                return NotFound(new { error = "ККМ не найдена" });
            
            using var conn = _service.CreateConnection();
            await conn.ExecuteAsync(@"
                UPDATE kkt SET 
                    serial_number = @SerialNumber,
                    nickname = @Nickname,
                    ip = @Ip,
                    is_active = @IsActive,
                    inn = @INN
                WHERE id = @Id", new
            {
                Id = id,
                kktDto.SerialNumber,
                kktDto.Nickname,
                kktDto.Ip,
                kktDto.IsActive,
                INN = kktDto.Inn
            });
            
            return Ok(new { success = true });
        }

        [HttpPut("{id}/nickname")]
        public async Task<IActionResult> SetNickname(int id, [FromBody] string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                return BadRequest("Nickname cannot be empty");
            await _service.UpdateNickname(id, nickname);
            return Ok();
        }

        [HttpPut("{id}/legalEntity")]
        public async Task<IActionResult> SetLegalEntity(int id, [FromBody] int? legalEntityId)
        {
            await _service.UpdateLegalEntity(id, legalEntityId);
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            bool deleted = await _service.SoftDelete(id);
            return deleted ? Ok() : NotFound();
        }

        [HttpPost("{id}/restore")]
        public async Task<IActionResult> Restore(int id)
        {
            bool restored = await _service.Restore(id);
            return restored ? Ok() : NotFound();
        }

        [HttpDelete("{id}/permanent")]
        public async Task<IActionResult> PermanentDelete(int id)
        {
            bool deleted = await _service.DeletePermanently(id);
            return deleted ? Ok() : NotFound();
        }

        [HttpPost("syncLegalEntities")]
        public IActionResult SyncLegalEntities()
        {
            var dbContext = new DbContext(HttpContext?.RequestServices?.GetService<IConfiguration>()!);
            var legalService = new LegalEntityService(dbContext);
            int updated = _service.SyncLegalEntities(legalService).Result;
            return Ok(new { updated });
        }

        [HttpPost("stopPolling/{id}")]
        public async Task<IActionResult> StopPolling(int id)
        {
            Console.WriteLine("[DEBUG] StopPolling called for id: " + id);
            
            var kkt = await _service.GetKktById(id);
            if (kkt == null)
            {
                Console.WriteLine("[DEBUG] KKT not found: " + id);
                return NotFound(new { error = "ККМ не найдена" });
            }
            
            Console.WriteLine("[DEBUG] Current IsPollingStopped: " + kkt.IsPollingStopped);
            
            await _service.UpdatePollingStatus(id, true);
            
            var updatedKkt = await _service.GetKktById(id);
            Console.WriteLine("[DEBUG] Updated IsPollingStopped: " + (updatedKkt?.IsPollingStopped ?? false));
            
            return Ok(new { success = true, message = "Опрос ККМ остановлен", isPollingStopped = true });
        }

        [HttpPost("startPolling/{id}")]
        public async Task<IActionResult> StartPolling(int id)
        {
            Console.WriteLine("[DEBUG] StartPolling called for id: " + id);
            
            var kkt = await _service.GetKktById(id);
            if (kkt == null)
            {
                Console.WriteLine("[DEBUG] KKT not found: " + id);
                return NotFound(new { error = "ККМ не найдена" });
            }
            
            Console.WriteLine("[DEBUG] Current IsPollingStopped: " + kkt.IsPollingStopped);
            
            await _service.UpdatePollingStatus(id, false);
            
            var updatedKkt = await _service.GetKktById(id);
            Console.WriteLine("[DEBUG] Updated IsPollingStopped: " + (updatedKkt?.IsPollingStopped ?? false));
            
            return Ok(new { success = true, message = "Опрос ККМ запущен", isPollingStopped = false });
        }

        [HttpPost("poll/{id}")]
        public async Task<IActionResult> PollKkt(int id)
        {
            try
            {
                var kkt = await _service.GetKktById(id);
                if (kkt == null)
                    return NotFound(new { error = "ККМ не найдена" });
                
                var ip = kkt?.Ip;
                if (string.IsNullOrEmpty(ip))
                    return BadRequest(new { error = "IP адрес не указан" });
                
                Console.WriteLine($"[Manual poll] Starting FULL manual poll for {ip}...");
                
                Kkt? result = null;
                try
                {
                    result = await _driver.PollFull(ip);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Manual poll] Poll error: {ex.Message}");
                }
                
                if (result != null && result.KktStatus != "OFFLINE" && result.KktStatus != "ERROR" && result.SerialNumber != null)
                {
                    result.LastCheck = DateTime.Now;
                    result.LastFullPoll = DateTime.Now;
                    result.IsPollingStopped = kkt?.IsPollingStopped ?? false;
                    result.Id = id;
                    
                    if (!string.IsNullOrEmpty(result.INN))
                    {
                        string normalizedInn = result.INN.TrimStart('0');
                        var legalService = _serviceProvider.GetService<LegalEntityService>();
                        if (legalService != null)
                        {
                            var legal = await legalService.GetByInn(normalizedInn);
                            if (legal != null)
                                result.LegalEntityId = legal.Id;
                        }
                    }
                    
                    await _service.UpdateKktData(result);
                    Console.WriteLine($"[Manual poll] Successfully updated data for {ip}, State={result.State}");
                }
                else
                {
                    Console.WriteLine($"[Manual poll] Poll failed for {ip}, updating only status");
                    await _service.UpdateKktStatus(ip, "OFFLINE", "DANGER");
                    
                    result = await _service.GetKktById(id);
                    if (result != null)
                    {
                        result.State = "DANGER";
                        result.KktStatus = "OFFLINE";
                        result.Error = result.Error ?? "Не удалось подключиться к ККМ";
                    }
                }
                
                Console.WriteLine($"[Manual poll] Completed for {ip}, State={result?.State}");
                
                var updatedKkt = await _service.GetById(id);
                
                return Ok(new
                {
                    success = true,
                    ip,
                    serialNumber = result?.SerialNumber,
                    inn = result?.INN,
                    fnNumber = result?.FnNumber,
                    lastReceiptNumber = result?.LastReceiptNumber,
                    state = result?.State,
                    lastFullPoll = result?.LastFullPoll,
                    firstReceiptDate = result?.FirstReceiptDate,
                    fnDetailedStatus = result?.FnDetailedStatus,
                    ffdVersion = result?.FfdVersion,
                    shiftState = result?.ShiftState,
                    softwareVersion = result?.SoftwareVersion,
                    softwareBuild = result?.SoftwareBuild,
                    ofdStatus = result?.OfdStatus,
                    ofdUrl = result?.OfdUrl,
                    ofdName = result?.OfdName,
                    ecrMode = result?.EcrMode,
                    ecrAdvancedMode = result?.EcrAdvancedMode,
                    batteryVoltage = result?.BatteryVoltage,
                    powerSourceVoltage = result?.PowerSourceVoltage,
                    sdCardStatus = result?.SdCardStatus,
                    fnExpiryDate = result?.FnExpiryDate
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Manual poll] Error: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("fullReport/{ip}")]
        public async Task<IActionResult> GetFullReport(string ip)
        {
            var results = new Dictionary<string, object>();
            
            try
            {
                Type? type = Type.GetTypeFromProgID("AddIn.DrvFR");
                if (type == null)
                    return BadRequest(new { error = "DrvFR не зарегистрирован" });

                dynamic drv = Activator.CreateInstance(type)!;

                drv.ConnectionType = 6;
                drv.ProtocolType = 0;
                drv.IPAddress = ip;
                drv.UseIPAddress = true;
                drv.TCPPort = 7778;
                drv.Timeout = 10000;
                drv.Password = 30;

                int connectResult = drv.Connect();
                if (connectResult != 0)
                    return BadRequest(new { error = $"Не удалось подключиться: {drv.ResultCodeDescription}" });

                int getEcrResult = drv.GetECRStatus();
                if (getEcrResult == 0)
                {
                    results["ecrStatus"] = new
                    {
                        ECRMode = drv.ECRMode,
                        ECRModeDescription = drv.ECRModeDescription?.ToString(),
                        ECRAdvancedMode = drv.ECRAdvancedMode,
                        ECRAdvancedModeDescription = drv.ECRAdvancedModeDescription?.ToString(),
                        ECRModeStatus = drv.ECRModeStatus,
                        ECRModeStatusDescription = GetModeStatusDescription(drv.ECRModeStatus),
                        ECRFlags = drv.ECRFlags
                    };
                }
                
                int shortStatusResult = drv.GetShortECRStatus();
                if (shortStatusResult == 0)
                {
                    results["shortEcrStatus"] = new
                    {
                        BatteryVoltage = drv.BatteryVoltage,
                        PowerSourceVoltage = drv.PowerSourceVoltage,
                        ECRMode = drv.ECRMode,
                        ECRModeDescription = drv.ECRModeDescription?.ToString(),
                        ECRAdvancedMode = drv.ECRAdvancedMode,
                        ECRAdvancedModeDescription = drv.ECRAdvancedModeDescription?.ToString(),
                        QuantityOfOperations = drv.QuantityOfOperations
                    };
                }

                var table14 = new Dictionary<string, object>();
                drv.TableNumber = 14;
                drv.RowNumber = 1;
                
                drv.FieldNumber = 1;
                if (drv.ReadTable() == 0) table14["status"] = drv.ValueOfFieldInteger;
                drv.FieldNumber = 2;
                if (drv.ReadTable() == 0) table14["clusterSize"] = drv.ValueOfFieldInteger;
                drv.FieldNumber = 3;
                if (drv.ReadTable() == 0) table14["totalSectors"] = drv.ValueOfFieldInteger;
                drv.FieldNumber = 4;
                if (drv.ReadTable() == 0) table14["freeSectors"] = drv.ValueOfFieldInteger;
                drv.FieldNumber = 5;
                if (drv.ReadTable() == 0) table14["ioErrors"] = drv.ValueOfFieldInteger;
                drv.FieldNumber = 6;
                if (drv.ReadTable() == 0) table14["retryCount"] = drv.ValueOfFieldInteger;
                results["table14"] = table14;
                
                var table18 = new Dictionary<string, object>();
                drv.TableNumber = 18;
                drv.RowNumber = 1;
                
                drv.FieldNumber = 1;
                if (drv.ReadTable() == 0) table18["serialNumber"] = drv.ValueOfFieldString?.ToString()?.Trim() ?? "";
                drv.FieldNumber = 2;
                if (drv.ReadTable() == 0) table18["inn"] = drv.ValueOfFieldString?.ToString()?.Trim() ?? "";
                drv.FieldNumber = 3;
                if (drv.ReadTable() == 0) table18["rnm"] = drv.ValueOfFieldString?.ToString()?.Trim() ?? "";
                drv.FieldNumber = 4;
                if (drv.ReadTable() == 0) table18["fsSerialNumber"] = drv.ValueOfFieldString?.ToString()?.Trim() ?? "";
                drv.FieldNumber = 5;
                if (drv.ReadTable() == 0) table18["taxSystem"] = drv.ValueOfFieldInteger;
                drv.FieldNumber = 6;
                if (drv.ReadTable() == 0) table18["workMode"] = drv.ValueOfFieldInteger;
                drv.FieldNumber = 7;
                if (drv.ReadTable() == 0) table18["user"] = drv.ValueOfFieldString?.ToString()?.Trim() ?? "";
                drv.FieldNumber = 8;
                if (drv.ReadTable() == 0) table18["operator"] = drv.ValueOfFieldString?.ToString()?.Trim() ?? "";
                drv.FieldNumber = 9;
                if (drv.ReadTable() == 0) table18["address"] = drv.ValueOfFieldString?.ToString()?.Trim() ?? "";
                drv.FieldNumber = 10;
                if (drv.ReadTable() == 0) table18["ofdName"] = drv.ValueOfFieldString?.ToString()?.Trim() ?? "";
                drv.FieldNumber = 11;
                if (drv.ReadTable() == 0) table18["ofdUrl"] = drv.ValueOfFieldString?.ToString()?.Trim() ?? "";
                drv.FieldNumber = 12;
                if (drv.ReadTable() == 0) table18["ofdInn"] = drv.ValueOfFieldString?.ToString()?.Trim() ?? "";
                drv.FieldNumber = 13;
                if (drv.ReadTable() == 0) table18["taxOfficeUrl"] = drv.ValueOfFieldString?.ToString()?.Trim() ?? "";
                drv.FieldNumber = 14;
                if (drv.ReadTable() == 0) table18["placeOfSettlement"] = drv.ValueOfFieldString?.ToString()?.Trim() ?? "";
                drv.FieldNumber = 15;
                if (drv.ReadTable() == 0) table18["senderEmail"] = drv.ValueOfFieldString?.ToString()?.Trim() ?? "";
                results["table18"] = table18;
                
                var fnExpiryInfo = new Dictionary<string, object>();
                try
                {
                    int expiryResult = drv.FNGetExpirationTime();
                    if (expiryResult == 0)
                    {
                        fnExpiryInfo["expiryDate"] = drv.Date.ToString("yyyy-MM-dd");
                        fnExpiryInfo["freeRegistrations"] = drv.FreeRegistration;
                        fnExpiryInfo["registrationsDone"] = drv.RegistrationNumber;
                    }
                    else
                    {
                        fnExpiryInfo["error"] = drv.ResultCodeDescription?.ToString() ?? "";
                    }
                }
                catch (Exception ex)
                {
                    fnExpiryInfo["error"] = ex.Message;
                }
                results["fnExpiryInfo"] = fnExpiryInfo;
                
                drv.Disconnect();
                
                return Ok(results);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("testConnect/{ip}")]
        public async Task<IActionResult> TestConnect(string ip)
        {
            try
            {
                Type? type = Type.GetTypeFromProgID("AddIn.DrvFR");
                if (type == null)
                    return BadRequest(new { error = "DrvFR не зарегистрирован" });
                
                dynamic drv = Activator.CreateInstance(type)!;
                
                drv.ConnectionType = 6;
                drv.ProtocolType = 0;
                drv.IPAddress = ip;
                drv.UseIPAddress = true;
                drv.TCPPort = 7778;
                drv.Timeout = 5000;
                drv.Password = 30;
                
                int connectResult = drv.Connect();
                string resultDesc = drv.ResultCodeDescription;
                drv.Disconnect();
                
                return Ok(new { connectResult, resultDesc });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        private string GetModeStatusDescription(int modeStatus)
        {
            return modeStatus switch
            {
                0 => "Нормальный режим",
                1 => "Ожидание команды",
                2 => "Выполнение команды",
                3 => "Ошибка",
                _ => "Неизвестно"
            };
        }
    }

    public class KktCreateDto
    {
        public string? SerialNumber { get; set; }
        public string? Inn { get; set; }
        public string? Nickname { get; set; }
        public string? Ip { get; set; }
        public string? Source { get; set; }
        public bool IsActive { get; set; } = true;
        public int? LegalEntityId { get; set; }
    }

    public class KktUpdateDto
    {
        public string? SerialNumber { get; set; }
        public string? Inn { get; set; }
        public string? Nickname { get; set; }
        public string? Ip { get; set; }
        public bool IsActive { get; set; } = true;
    }
}