using System;

namespace KKTMonitor.Models
{
    public class Kkt
    {
        public int Id { get; set; }
        public string? SerialNumber { get; set; }
        public string? FnNumber { get; set; }
        public string? Nickname { get; set; }
        public string? Ip { get; set; }
        public string? Source { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime? LastCheck { get; set; }
        public DateTime? LastFullPoll { get; set; }
        public string? FnStatus { get; set; }
        public string? OfdStatus { get; set; }
        public string? KktStatus { get; set; }
        public string? Error { get; set; }
        public string? State { get; set; }

        // Дополнительные поля
        public DateTime? FnExpiryDate { get; set; }
        public string? ShiftState { get; set; }
        public int? FnDocsLeft { get; set; }
        public string? INN { get; set; }
        public int? LegalEntityId { get; set; }
        public int? LastBuyReceiptNumber { get; set; }
        public string? SoftwareVersion { get; set; }
        public int? SoftwareBuild { get; set; }
        public string? OfdServer { get; set; }
        public int? LastReceiptNumber { get; set; }
        public DateTime? FirstReceiptDate { get; set; }
        public DateTime? LastReceiptDate { get; set; }
        
        // Новые поля
        public string? FnDetailedStatus { get; set; }
        public string? FfdVersion { get; set; }
        public bool IsPollingStopped { get; set; } = false;

        // Поля для мягкого удаления
        public bool IsActive { get; set; } = true;
        public DateTime? DeletedAt { get; set; }

        // Данные из полного отчета (таблица 18)
        public string? OfdUrl { get; set; }
        public string? OfdName { get; set; }
        public string? OfdInn { get; set; }
        public string? TaxOfficeUrl { get; set; }
        public string? UserName { get; set; }
        public string? OperatorName { get; set; }
        public string? Address { get; set; }
        public string? PlaceOfSettlement { get; set; }
        public string? SenderEmail { get; set; }
        public string? Rnm { get; set; }
        public int? TaxSystem { get; set; }
        
        // Данные из статуса ККМ
        public int? EcrMode { get; set; }
        public string? EcrModeDescription { get; set; }
        public int? EcrAdvancedMode { get; set; }
        public string? EcrAdvancedModeDescription { get; set; }
        public int? EcrModeStatus { get; set; }
        public string? EcrModeStatusDescription { get; set; }
        
        // Данные из короткого статуса
        public double? BatteryVoltage { get; set; }
        public double? PowerSourceVoltage { get; set; }
        
        // Данные SD карты (таблица 14)
        public int? SdCardStatus { get; set; }
        public int? SdCardClusterSize { get; set; }
        public int? SdCardTotalSectors { get; set; }
        public int? SdCardFreeSectors { get; set; }
        public int? SdCardIoErrors { get; set; }
        public int? SdCardRetryCount { get; set; }
        
        // Время открытия смены
        public DateTime? ShiftOpenTime { get; set; }
        
        // Данные о перерегистрациях
        public int? FreeRegistration { get; set; }
        public int? RegistrationNumber { get; set; }
        
        // Фаза жизни ФН
        public int? FnLifeState { get; set; }
        public string? FnLifeStateDescription { get; set; }
    }
}
