using System;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KKTMonitor.Models;

namespace KKTMonitor.Services
{
    public class KktDriverService
    {
        private dynamic? _currentDrv;
        private string? _currentIp;

        // Настройки для работы через Ethernet-to-COM
        private readonly int _commandDelayMs = 800;
        private readonly int _tcpTimeoutMs = 45000;
        private readonly int _connectionTimeoutMs = 30000;

        public Kkt Poll(string ip)
        {
            var result = new Kkt
            {
                Ip = ip,
                LastSeen = DateTime.Now,
                KktStatus = "UNKNOWN"
            };

            try
            {
                Type? type = Type.GetTypeFromProgID("AddIn.DrvFR");
                if (type == null)
                    throw new Exception("DrvFR не зарегистрирован в системе");

                dynamic drv = Activator.CreateInstance(type)!;

                drv.ConnectionType = 6;
                drv.ProtocolType = 0;
                drv.IPAddress = ip;
                drv.UseIPAddress = true;
                drv.TCPPort = 7778;
                drv.Timeout = _tcpTimeoutMs;
                drv.Password = 30;

                Console.WriteLine("[DEBUG] " + ip + ": 1/14 - Connecting...");
                int connectResult = drv.Connect();
                if (connectResult != 0)
                {
                    result.KktStatus = "OFFLINE";
                    result.Error = "Не удалось подключиться к " + ip + ":" + drv.TCPPort + ", код " + connectResult + ": " + drv.ResultCodeDescription;
                    result.State = "DANGER";
                    return result;
                }
                Console.WriteLine("[DEBUG] " + ip + ": 1/14 - Connected OK");
                Thread.Sleep(_commandDelayMs);

                Console.WriteLine("[DEBUG] " + ip + ": 2/14 - Reading serial number...");
                int readSerialResult = drv.ReadSerialNumber();
                if (readSerialResult == 0)
                {
                    result.SerialNumber = drv.SerialNumber?.ToString();
                    Console.WriteLine("[DEBUG] " + ip + ": 2/14 - SerialNumber = " + result.SerialNumber);
                }
                else
                {
                    Console.WriteLine("[DEBUG] " + ip + ": 2/14 - ReadSerialNumber failed, code=" + readSerialResult);
                    result.SerialNumber = drv.SerialNumber?.ToString();
                }
                Thread.Sleep(_commandDelayMs);

                Console.WriteLine("[DEBUG] " + ip + ": 3/14 - Reading ECR status...");
                int statusResult = drv.ReadECRStatus();
                result.KktStatus = (statusResult == 0) ? "ONLINE" : "ERROR";
                Console.WriteLine("[DEBUG] " + ip + ": 3/14 - ReadECRStatus = " + statusResult);
                Thread.Sleep(_commandDelayMs);

                Console.WriteLine("[DEBUG] " + ip + ": 4/14 - Getting INN...");
                try
                {
                    int getEcrResult = drv.GetECRStatus();
                    if (getEcrResult == 0)
                    {
                        result.INN = drv.INN?.ToString();
                        Console.WriteLine("[DEBUG] " + ip + ": 4/14 - INN = " + result.INN);
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] " + ip + ": 4/14 - GetECRStatus failed, code=" + getEcrResult);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[DEBUG] " + ip + ": 4/14 - INN error: " + ex.Message);
                }
                Thread.Sleep(_commandDelayMs);

                Console.WriteLine("[DEBUG] " + ip + ": 5/14 - Getting FN status...");
                try
                {
                    int fnStatusResult = drv.FNGetStatus();
                    if (fnStatusResult == 0)
                    {
                        result.FnNumber = drv.SerialNumber?.ToString();
                        Console.WriteLine("[DEBUG] " + ip + ": 5/14 - FnNumber = " + result.FnNumber);
                        
                        int lifeState = drv.FNLifeState;
                        result.FnLifeState = lifeState;
                        
                        result.FnLifeStateDescription = lifeState switch
                        {
                            0 => "Настройка (производственная стадия)",
                            1 => "Готовность к фискализации",
                            3 => "Фискальный режим (активен)",
                            7 => "Фискальный режим закрыт, передача ФД в ОФД",
                            15 => "Чтение данных из Архива ФН",
                            _ => "Неизвестно (" + lifeState + ")"
                        };
                        
                        result.FnDetailedStatus = lifeState switch
                        {
                            0 => "Новый (не настроен)",
                            1 => "Готовность к фискализации",
                            3 => "Работает (фискализирован)",
                            7 => "Закрыт (архив)",
                            15 => "Истёк срок / Заполнен",
                            _ => "Неизвестно (" + lifeState + ")"
                        };
                        
                        result.FnStatus = lifeState switch
                        {
                            3 => "ACTIVE",
                            7 => "CLOSED",
                            15 => "ARCHIVE",
                            _ => "UNKNOWN(" + lifeState + ")"
                        };
                        
                        Console.WriteLine("[DEBUG] " + ip + ": 5/14 - FnLifeState=" + lifeState + " (" + result.FnLifeStateDescription + ")");
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] " + ip + ": 5/14 - FNGetStatus failed, code=" + fnStatusResult);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[DEBUG] " + ip + ": 5/14 - FNGetStatus error: " + ex.Message);
                }
                Thread.Sleep(_commandDelayMs);

                Console.WriteLine("[DEBUG] " + ip + ": 6/14 - Getting FFD version from Table17...");
                result.FfdVersion = GetFfdVersionFromTable17(drv, ip);
                Thread.Sleep(_commandDelayMs);

                Console.WriteLine("[DEBUG] " + ip + ": 7/14 - Getting FN capacity...");
                try
                {
                    int memResult = drv.FNGetFreeMemoryResource();
                    if (memResult == 0)
                    {
                        result.FnDocsLeft = drv.FN5YearResource;
                        Console.WriteLine("[DEBUG] " + ip + ": 7/14 - FnDocsLeft = " + result.FnDocsLeft);
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] " + ip + ": 7/14 - FNGetFreeMemoryResource failed, code=" + memResult);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[DEBUG] " + ip + ": 7/14 - FNGetFreeMemoryResource error: " + ex.Message);
                }
                Thread.Sleep(_commandDelayMs);

                Console.WriteLine("[DEBUG] " + ip + ": 8/14 - Getting firmware version...");
                try
                {
                    int softResult = drv.GetECRStatus();
                    if (softResult == 0)
                    {
                        result.SoftwareVersion = drv.ECRSoftVersion?.ToString();
                        result.SoftwareBuild = drv.ECRBuild;
                        Console.WriteLine("[DEBUG] " + ip + ": 8/14 - SoftwareVersion = " + result.SoftwareVersion + ", Build = " + result.SoftwareBuild);
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] " + ip + ": 8/14 - GetECRStatus failed, code=" + softResult);
                        result.SoftwareVersion = drv.ECRSoftVersion?.ToString();
                        result.SoftwareBuild = drv.ECRBuild;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[DEBUG] " + ip + ": 8/14 - SoftwareVersion error: " + ex.Message);
                }
                Thread.Sleep(_commandDelayMs);

                Console.WriteLine("[DEBUG] " + ip + ": 9/14 - Getting OFD server...");
                try
                {
                    int fiscalResult = drv.FNGetFiscalizationResult();
                    if (fiscalResult == 0)
                    {
                        result.OfdServer = drv.INNOFD?.ToString();
                        Console.WriteLine("[DEBUG] " + ip + ": 9/14 - OfdServer = " + result.OfdServer);
                    }
                }
                catch { }
                Thread.Sleep(_commandDelayMs);

                Console.WriteLine("[DEBUG] " + ip + ": 10/14 - Getting OFD status...");
                try
                {
                    int ofdResult = drv.FNGetInfoExchangeStatus();
                    if (ofdResult == 0)
                    {
                        int infoBits = drv.InfoExchangeStatus;
                        bool ofdAvailable = (infoBits & 1) == 1;
                        result.OfdStatus = ofdAvailable ? "OK" : "FAIL";
                        Console.WriteLine("[DEBUG] " + ip + ": 10/14 - OfdStatus = " + result.OfdStatus);
                    }
                }
                catch { }
                Thread.Sleep(_commandDelayMs);

                Console.WriteLine("[DEBUG] " + ip + ": 11/14 - Getting shift state...");
                try
                {
                    int sessionResult = drv.FNGetCurrentSessionParams();
                    if (sessionResult == 0)
                    {
                        result.ShiftState = (drv.FNSessionState == 1) ? "OPEN" : "CLOSED";
                        Console.WriteLine("[DEBUG] " + ip + ": 11/14 - ShiftState = " + result.ShiftState);
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] " + ip + ": 11/14 - FNGetCurrentSessionParams failed, code=" + sessionResult);
                        if (result.FnStatus == "ARCHIVE" || (result.FnDetailedStatus != null && result.FnDetailedStatus.Contains("Закрыт")))
                        {
                            result.ShiftState = "ARCHIVE";
                            Console.WriteLine("[DEBUG] " + ip + ": 11/14 - ShiftState = " + result.ShiftState + " (FN is closed)");
                        }
                        else
                        {
                            result.ShiftState = "UNKNOWN";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[DEBUG] " + ip + ": 11/14 - ShiftState error: " + ex.Message);
                    result.ShiftState = "UNKNOWN";
                }
                Thread.Sleep(_commandDelayMs);

                Console.WriteLine("[DEBUG] " + ip + ": 12/14 - Getting first receipt date...");
                try
                {
                    for (int i = 1; i <= 10; i++)
                    {
                        drv.DocumentNumber = i;
                        int findResult = drv.FNFindDocument();
                        if (findResult == 0)
                        {
                            if (drv.DocumentType == 1 || drv.DocumentType == 3)
                            {
                                result.FirstReceiptDate = drv.Date;
                                Console.WriteLine("[DEBUG] " + ip + ": 12/14 - FirstReceiptDate = " + result.FirstReceiptDate.Value.ToString("yyyy-MM-dd"));
                                break;
                            }
                        }
                        Thread.Sleep(_commandDelayMs / 2);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[DEBUG] " + ip + ": 12/14 - GetFirstReceiptDate error: " + ex.Message);
                }
                Thread.Sleep(_commandDelayMs);

                Console.WriteLine("[DEBUG] " + ip + ": 13/14 - Getting last receipt number...");
                int? lastReceiptNumber = null;
                try
                {
                    for (int i = 10000; i >= 1; i--)
                    {
                        drv.DocumentNumber = i;
                        int findResult = drv.FNFindDocument();
                        if (findResult == 0)
                        {
                            lastReceiptNumber = i;
                            Console.WriteLine("[DEBUG] " + ip + ": 13/14 - Last document number = " + lastReceiptNumber);
                            break;
                        }
                        if (i % 100 == 0) Thread.Sleep(_commandDelayMs / 2);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[DEBUG] " + ip + ": 13/14 - LastReceiptNumber error: " + ex.Message);
                }
                result.LastReceiptNumber = lastReceiptNumber;

                Console.WriteLine("[DEBUG] " + ip + ": 14/14 - Getting last receipt date from FN...");
                if (lastReceiptNumber.HasValue && lastReceiptNumber.Value > 0)
                {
                    try
                    {
                        drv.DocumentNumber = lastReceiptNumber.Value;
                        int findResult = drv.FNFindDocument();
                        if (findResult == 0)
                        {
                            result.LastReceiptDate = drv.Date;
                            Console.WriteLine("[DEBUG] " + ip + ": 14/14 - LastReceiptDate = " + result.LastReceiptDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[DEBUG] " + ip + ": 14/14 - LastReceiptDate error: " + ex.Message);
                    }
                }

                result.LastCheck = DateTime.Now;
                drv.Disconnect();

                result.State = CalculateState(result);
                Console.WriteLine("[DEBUG] " + ip + ": Final State = " + result.State);

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DEBUG] " + ip + ": EXCEPTION - " + ex.Message);
                result.KktStatus = "ERROR";
                result.Error = ex.Message;
                result.State = "DANGER";
                return result;
            }
        }

        public async Task<Kkt?> PollFull(string ip)
        {
            var result = Poll(ip);
            
            if (result.KktStatus == "OFFLINE" || result.KktStatus == "ERROR")
                return result;
            
            try
            {
                Type? type = Type.GetTypeFromProgID("AddIn.DrvFR");
                if (type == null) return result;
                
                dynamic drv = Activator.CreateInstance(type)!;
                
                drv.ConnectionType = 6;
                drv.ProtocolType = 0;
                drv.IPAddress = ip;
                drv.UseIPAddress = true;
                drv.TCPPort = 7778;
                drv.Timeout = _tcpTimeoutMs;
                drv.Password = 30;
                
                int connectResult = drv.Connect();
                if (connectResult != 0)
                {
                    drv.Disconnect();
                    return result;
                }
                
                // Получаем статус ККМ (GetECRStatus)
                int getEcrResult = drv.GetECRStatus();
                if (getEcrResult == 0)
                {
                    result.EcrMode = drv.ECRMode;
                    result.EcrModeDescription = drv.ECRModeDescription?.ToString();
                    result.EcrAdvancedMode = drv.ECRAdvancedMode;
                    result.EcrAdvancedModeDescription = drv.ECRAdvancedModeDescription?.ToString();
                    result.EcrModeStatus = drv.ECRModeStatus;
                    result.EcrModeStatusDescription = GetModeStatusDescription(drv.ECRModeStatus);
                    Console.WriteLine("[DEBUG] " + ip + ": EcrAdvancedMode=" + result.EcrAdvancedMode + ", EcrModeStatus=" + result.EcrModeStatus);
                }
                Thread.Sleep(_commandDelayMs);
                
                // Получаем короткий статус (напряжение)
                int shortStatusResult = drv.GetShortECRStatus();
                if (shortStatusResult == 0)
                {
                    result.BatteryVoltage = drv.BatteryVoltage;
                    result.PowerSourceVoltage = drv.PowerSourceVoltage;
                    Console.WriteLine("[DEBUG] " + ip + ": BatteryVoltage=" + result.BatteryVoltage + ", PowerSourceVoltage=" + result.PowerSourceVoltage);
                }
                Thread.Sleep(_commandDelayMs);
                
                // Читаем таблицу 14 (SD карта)
                drv.TableNumber = 14;
                drv.RowNumber = 1;
                
                drv.FieldNumber = 1;
                if (drv.ReadTable() == 0) 
                {
                    result.SdCardStatus = drv.ValueOfFieldInteger;
                    Console.WriteLine("[DEBUG] " + ip + ": SdCardStatus=" + result.SdCardStatus);
                }
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 2;
                if (drv.ReadTable() == 0) result.SdCardClusterSize = drv.ValueOfFieldInteger;
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 3;
                if (drv.ReadTable() == 0) result.SdCardTotalSectors = drv.ValueOfFieldInteger;
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 4;
                if (drv.ReadTable() == 0) result.SdCardFreeSectors = drv.ValueOfFieldInteger;
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 5;
                if (drv.ReadTable() == 0) result.SdCardIoErrors = drv.ValueOfFieldInteger;
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 6;
                if (drv.ReadTable() == 0) result.SdCardRetryCount = drv.ValueOfFieldInteger;
                Thread.Sleep(_commandDelayMs);
                
                // Читаем таблицу 18 (ФН данные для передачи)
                drv.TableNumber = 18;
                drv.RowNumber = 1;
                
                drv.FieldNumber = 1;
                if (drv.ReadTable() == 0) 
                {
                    string? serialFromTable = drv.ValueOfFieldString?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(serialFromTable))
                        result.SerialNumber = serialFromTable;
                    Console.WriteLine("[DEBUG] " + ip + ": SerialNumber from table18=" + result.SerialNumber);
                }
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 2;
                if (drv.ReadTable() == 0) 
                {
                    string? innFromTable = drv.ValueOfFieldString?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(innFromTable))
                        result.INN = innFromTable;
                    Console.WriteLine("[DEBUG] " + ip + ": INN from table18=" + result.INN);
                }
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 3;
                if (drv.ReadTable() == 0) result.Rnm = drv.ValueOfFieldString?.ToString()?.Trim();
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 4;
                if (drv.ReadTable() == 0) 
                {
                    string? fnFromTable = drv.ValueOfFieldString?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(fnFromTable))
                        result.FnNumber = fnFromTable;
                }
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 5;
                if (drv.ReadTable() == 0) result.TaxSystem = drv.ValueOfFieldInteger;
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 7;
                if (drv.ReadTable() == 0) result.UserName = drv.ValueOfFieldString?.ToString()?.Trim();
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 8;
                if (drv.ReadTable() == 0) result.OperatorName = drv.ValueOfFieldString?.ToString()?.Trim();
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 9;
                if (drv.ReadTable() == 0) result.Address = drv.ValueOfFieldString?.ToString()?.Trim();
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 10;
                if (drv.ReadTable() == 0) result.OfdName = drv.ValueOfFieldString?.ToString()?.Trim();
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 11;
                if (drv.ReadTable() == 0) 
                {
                    result.OfdUrl = drv.ValueOfFieldString?.ToString()?.Trim();
                    Console.WriteLine("[DEBUG] " + ip + ": OfdUrl=" + result.OfdUrl);
                }
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 12;
                if (drv.ReadTable() == 0) result.OfdInn = drv.ValueOfFieldString?.ToString()?.Trim();
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 13;
                if (drv.ReadTable() == 0) result.TaxOfficeUrl = drv.ValueOfFieldString?.ToString()?.Trim();
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 14;
                if (drv.ReadTable() == 0) result.PlaceOfSettlement = drv.ValueOfFieldString?.ToString()?.Trim();
                Thread.Sleep(_commandDelayMs);
                
                drv.FieldNumber = 15;
                if (drv.ReadTable() == 0) result.SenderEmail = drv.ValueOfFieldString?.ToString()?.Trim();
                Thread.Sleep(_commandDelayMs);
                
                // Получаем информацию о сроке действия ФН
                try
                {
                    Console.WriteLine("[DEBUG] " + ip + ": Calling FNGetExpirationTime...");
                    int expiryResult = drv.FNGetExpirationTime();
                    Console.WriteLine("[DEBUG] " + ip + ": FNGetExpirationTime result = " + expiryResult);
                    
                    if (expiryResult == 0)
                    {
                        DateTime? expiryDate = null;
                        
                        try
                        {
                            expiryDate = drv.Date;
                            Console.WriteLine("[DEBUG] " + ip + ": Date from drv.Date = " + expiryDate);
                        }
                        catch (Exception dateEx)
                        {
                            Console.WriteLine("[DEBUG] " + ip + ": Error getting drv.Date: " + dateEx.Message);
                        }
                        
                        // Проверяем, что дата не является 01.01.1970 (Unix epoch)
                        bool isFnClosed = result.FnLifeState == 7 || result.FnLifeState == 15;
                        
                        if (expiryDate.HasValue && expiryDate.Value.Year > 1970 && !isFnClosed)
                        {
                            result.FnExpiryDate = expiryDate;
                            Console.WriteLine("[DEBUG] " + ip + ": Valid FnExpiryDate = " + result.FnExpiryDate);
                        }
                        else
                        {
                            Console.WriteLine("[DEBUG] " + ip + ": Invalid or not applicable expiry date (FnLifeState=" + result.FnLifeState + "), setting to null");
                            result.FnExpiryDate = null;
                        }
                        
                        result.FreeRegistration = drv.FreeRegistration;
                        result.RegistrationNumber = drv.RegistrationNumber;
                        
                        Console.WriteLine("[DEBUG] " + ip + ": FreeRegistration = " + result.FreeRegistration);
                        Console.WriteLine("[DEBUG] " + ip + ": RegistrationNumber = " + result.RegistrationNumber);
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] " + ip + ": FNGetExpirationTime failed, code=" + expiryResult + ", error=" + drv.ResultCodeDescription);
                        result.FnExpiryDate = null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[DEBUG] " + ip + ": FNGetExpirationTime exception: " + ex.Message);
                    result.FnExpiryDate = null;
                }
                
                drv.Disconnect();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DEBUG] " + ip + ": PollFull exception: " + ex.Message);
            }
            
            return result;
        }

        private string? GetFfdVersionFromTable17(dynamic drv, string ip)
        {
            try
            {
                int currentPassword = drv.Password;
                drv.Password = 30;
                
                drv.TableNumber = 17;
                int getTableStructResult = drv.GetTableStruct();
                
                if (getTableStructResult != 0)
                {
                    Console.WriteLine("[DEBUG] " + ip + ": 6/14 - GetTableStruct failed: " + drv.ResultCodeDescription);
                    drv.Password = currentPassword;
                    return "Не определён";
                }
                
                int fieldCount = drv.FieldNumber;
                int targetFieldNumber = -1;
                
                for (int field = 1; field <= fieldCount; field++)
                {
                    drv.TableNumber = 17;
                    drv.FieldNumber = field;
                    int getFieldStructResult = drv.GetFieldStruct();
                    
                    if (getFieldStructResult == 0)
                    {
                        string fieldName = drv.FieldName?.ToString() ?? "";
                        if (fieldName.Contains("ФОРМАТ ФД") || fieldName == "RUS ФОРМАТ ФД")
                        {
                            targetFieldNumber = field;
                            Console.WriteLine("[DEBUG] " + ip + ": 6/14 - Found target field '" + fieldName + "' at position " + field);
                            break;
                        }
                    }
                }
                
                string? ffdVersion = null;
                
                if (targetFieldNumber > 0)
                {
                    drv.TableNumber = 17;
                    drv.RowNumber = 1;
                    drv.FieldNumber = targetFieldNumber;
                    
                    int readResult = drv.ReadTable();
                    if (readResult == 0)
                    {
                        int ffdValue = drv.ValueOfFieldInteger;
                        Console.WriteLine("[DEBUG] " + ip + ": 6/14 - Table17[" + targetFieldNumber + "] = " + ffdValue);
                        
                        ffdVersion = ffdValue switch
                        {
                            0 => "ФФД 1.0",
                            2 => "ФФД 1.05",
                            3 => "ФФД 1.1",
                            4 => "ФФД 1.2",
                            _ => ffdValue > 0 ? "ФФД " + ffdValue : "Не определён"
                        };
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] " + ip + ": 6/14 - ReadTable failed: " + drv.ResultCodeDescription);
                        ffdVersion = "Не определён";
                    }
                }
                else
                {
                    Console.WriteLine("[DEBUG] " + ip + ": 6/14 - Target field not found, trying direct field 17...");
                    
                    drv.TableNumber = 17;
                    drv.RowNumber = 1;
                    drv.FieldNumber = 17;
                    
                    int directResult = drv.ReadTable();
                    if (directResult == 0)
                    {
                        int ffdValue = drv.ValueOfFieldInteger;
                        Console.WriteLine("[DEBUG] " + ip + ": 6/14 - Direct field 17 = " + ffdValue);
                        
                        ffdVersion = ffdValue switch
                        {
                            0 => "ФФД 1.0",
                            2 => "ФФД 1.05",
                            3 => "ФФД 1.1",
                            4 => "ФФД 1.2",
                            _ => ffdValue > 0 ? "ФФД " + ffdValue : "Не определён"
                        };
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] " + ip + ": 6/14 - Direct read failed: " + drv.ResultCodeDescription);
                        ffdVersion = "Не определён";
                    }
                }
                
                drv.Password = currentPassword;
                Console.WriteLine("[DEBUG] " + ip + ": 6/14 - FfdVersion = " + ffdVersion);
                return ffdVersion;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DEBUG] " + ip + ": 6/14 - Exception in GetFfdVersionFromTable17: " + ex.Message);
                return "Не определён";
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

        private string CalculateState(Kkt kkt)
        {
            var now = DateTime.Now;
            
            if (kkt.KktStatus == "ERROR" ||
                (kkt.LastCheck.HasValue && (now - kkt.LastCheck.Value).TotalHours > 24))
            {
                return "DANGER";
            }
            
            if (kkt.LastCheck.HasValue && (now - kkt.LastCheck.Value).TotalHours > 1)
            {
                return "WARNING";
            }
            
            return "OK";
        }

        public async Task<bool> CheckNetworkAccessibility(string ip, int timeoutMs = 2000)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, timeoutMs);
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        public Task<bool> CheckOfdServerAccessibility(string ofdServerUrl = "https://www.nalog.gov.ru", int timeoutMs = 5000)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
                var response = httpClient.GetAsync(ofdServerUrl).Result;
                return Task.FromResult(response.IsSuccessStatusCode);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public Task<DateTime?> GetFirstRegistrationDate(string ip)
        {
            try
            {
                dynamic? drv = GetConnectedDriver(ip);
                if (drv == null) return Task.FromResult<DateTime?>(null);

                for (int i = 1; i <= 10; i++)
                {
                    drv.DocumentNumber = i;
                    int result = drv.FNFindDocument();
                    if (result == 0 && drv.DocumentType == 1)
                    {
                        return Task.FromResult<DateTime?>(drv.Date);
                    }
                    Thread.Sleep(_commandDelayMs / 2);
                }
                return Task.FromResult<DateTime?>(null);
            }
            catch
            {
                return Task.FromResult<DateTime?>(null);
            }
            finally
            {
                DisconnectDriver();
            }
        }

        public Task<int?> GetLastReceiptNumber(string ip)
        {
            try
            {
                dynamic? drv = GetConnectedDriver(ip);
                if (drv == null) return Task.FromResult<int?>(null);

                for (int i = 10000; i >= 1; i--)
                {
                    drv.DocumentNumber = i;
                    int findResult = drv.FNFindDocument();
                    if (findResult == 0 && drv.DocumentType == 3)
                    {
                        return Task.FromResult<int?>(i);
                    }
                    if (i % 100 == 0) Thread.Sleep(_commandDelayMs / 2);
                }
                return Task.FromResult<int?>(null);
            }
            catch
            {
                return Task.FromResult<int?>(null);
            }
            finally
            {
                DisconnectDriver();
            }
        }

        public Task<DateTime?> GetFirstReceiptDate(string ip)
        {
            return GetFirstRegistrationDate(ip);
        }

        public Task<DateTime?> GetFirstReceiptDatePlusDays(string ip, int daysToAdd)
        {
            var firstDate = GetFirstReceiptDate(ip).Result;
            return Task.FromResult(firstDate?.AddDays(daysToAdd));
        }

        public Task<string?> GetFirmwareVersion(string ip)
        {
            try
            {
                dynamic? drv = GetConnectedDriver(ip);
                if (drv == null) return Task.FromResult<string?>(null);

                int result = drv.GetECRStatus();
                if (result == 0)
                {
                    return Task.FromResult(drv.ECRSoftVersion?.ToString());
                }
                return Task.FromResult<string?>(null);
            }
            catch
            {
                return Task.FromResult<string?>(null);
            }
            finally
            {
                DisconnectDriver();
            }
        }

        public Task<ShiftStateInfo> GetShiftState(string ip)
        {
            var result = new ShiftStateInfo();
            try
            {
                dynamic? drv = GetConnectedDriver(ip);
                if (drv == null)
                {
                    result.Error = "Не удалось подключиться";
                    return Task.FromResult(result);
                }

                int sessionResult = drv.FNGetCurrentSessionParams();
                if (sessionResult == 0)
                {
                    result.IsOpen = drv.FNSessionState == 1;
                    result.SessionNumber = drv.SessionNumber;
                    result.ReceiptNumber = drv.ReceiptNumber;
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }
            finally
            {
                DisconnectDriver();
            }
            return Task.FromResult(result);
        }

        public Task<int?> GetRemainingFnCapacity(string ip)
        {
            try
            {
                dynamic? drv = GetConnectedDriver(ip);
                if (drv == null) return Task.FromResult<int?>(null);

                int memResult = drv.FNGetFreeMemoryResource();
                if (memResult == 0)
                {
                    return Task.FromResult<int?>((int?)drv.FN5YearResource);
                }
                return Task.FromResult<int?>(null);
            }
            catch
            {
                return Task.FromResult<int?>(null);
            }
            finally
            {
                DisconnectDriver();
            }
        }

        public Task<KktErrorStatus> GetKktErrorStatus(string ip)
        {
            var result = new KktErrorStatus();
            try
            {
                dynamic? drv = GetConnectedDriver(ip);
                if (drv == null)
                {
                    result.HasError = false;
                    return Task.FromResult(result);
                }

                int statusResult = drv.GetShortECRStatus();
                if (statusResult != 0)
                {
                    result.HasError = true;
                    result.ErrorCode = statusResult;
                    result.ErrorMessage = drv.ResultCodeDescription;
                    return Task.FromResult(result);
                }

                result.HasError = false;
            }
            catch
            {
                result.HasError = false;
            }
            finally
            {
                DisconnectDriver();
            }
            return Task.FromResult(result);
        }

        private dynamic? GetConnectedDriver(string ip)
        {
            try
            {
                Type? type = Type.GetTypeFromProgID("AddIn.DrvFR");
                if (type == null) return null;

                _currentDrv = Activator.CreateInstance(type);
                _currentIp = ip;

                if (_currentDrv == null) return null;

                _currentDrv.ConnectionType = 6;
                _currentDrv.ProtocolType = 0;
                _currentDrv.IPAddress = ip;
                _currentDrv.UseIPAddress = true;
                _currentDrv.TCPPort = 7778;
                _currentDrv.Timeout = _tcpTimeoutMs;
                _currentDrv.Password = 30;

                int connectResult = _currentDrv.Connect();
                return connectResult == 0 ? _currentDrv : null;
            }
            catch
            {
                return null;
            }
        }

        private void DisconnectDriver()
        {
            try
            {
                _currentDrv?.Disconnect();
                _currentDrv = null;
                _currentIp = null;
            }
            catch { }
        }
    }

    public class ShiftStateInfo
    {
        public bool IsOpen { get; set; }
        public int? SessionNumber { get; set; }
        public int? ReceiptNumber { get; set; }
        public DateTime? OpenDate { get; set; }
        public object? OpenTime { get; set; }
        public double HoursOpen { get; set; }
        public bool IsMoreThan24Hours { get; set; }
        public string? Error { get; set; }
    }

    public class KktErrorStatus
    {
        public bool HasError { get; set; }
        public bool HasWarning { get; set; }
        public int? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public int? ErrorFlags { get; set; }
        public int? WarningFlags { get; set; }
    }
}