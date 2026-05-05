using KKTMonitor.Models;

namespace KKTMonitor.Services
{
    public class KktStateService
    {
        /// <summary>
        /// Рассчитать статус ККМ на основе всех параметров
        /// </summary>
        public string Calculate(Kkt kkt)
        {
            var now = DateTime.Now;
            
            // ========== DANGER условия ==========
            
            // ККМ не отвечает по сети
            if (kkt.KktStatus == "OFFLINE" || kkt.KktStatus == "ERROR" || kkt.KktStatus == "TIMEOUT")
                return "DANGER";
            
            // SD карта в статусе DANGER (любое значение кроме 0)
            if (kkt.SdCardStatus.HasValue && kkt.SdCardStatus.Value != 0)
                return "DANGER";
            
            // Дата замены ФН меньше текущей даты на 10 дней или меньше
            if (kkt.FnExpiryDate.HasValue)
            {
                var daysUntilExpiry = (kkt.FnExpiryDate.Value - now).TotalDays;
                if (daysUntilExpiry <= 10)
                    return "DANGER";
            }
            
            // Остаточная ёмкость ФН меньше 50000
            if (kkt.FnDocsLeft.HasValue && kkt.FnDocsLeft.Value < 50000)
                return "DANGER";
            
            // ФН в состоянии ARCHIVE (закрыт)
            if (kkt.FnStatus == "ARCHIVE")
                return "DANGER";
            
            // ========== WARNING условия ==========
            
            // Напряжение батареи меньше 3V
            if (kkt.BatteryVoltage.HasValue && kkt.BatteryVoltage.Value < 3)
                return "WARNING";
            
            // Напряжение источника меньше 24V
            if (kkt.PowerSourceVoltage.HasValue && kkt.PowerSourceVoltage.Value < 24)
                return "WARNING";
            
            // Дата последнего чека более 14 дней от текущей даты
            if (kkt.LastReceiptDate.HasValue)
            {
                var daysSinceLastReceipt = (now - kkt.LastReceiptDate.Value).TotalDays;
                if (daysSinceLastReceipt > 14)
                    return "WARNING";
            }
            
            // Дата замены ФН меньше 30 дней до окончания, но не меньше 10
            if (kkt.FnExpiryDate.HasValue)
            {
                var daysUntilExpiry = (kkt.FnExpiryDate.Value - now).TotalDays;
                if (daysUntilExpiry > 10 && daysUntilExpiry <= 30)
                    return "WARNING";
            }
            
            // Остаточная ёмкость ФН меньше 100000 но не меньше 50000
            if (kkt.FnDocsLeft.HasValue && kkt.FnDocsLeft.Value < 100000 && kkt.FnDocsLeft.Value >= 50000)
                return "WARNING";
            
            // ========== OK условия ==========
            
            return "OK";
        }
        
        /// <summary>
        /// Рассчитать общий статус для главной страницы с учётом сетевой доступности
        /// </summary>
        public string CalculateOverallStatus(string kktState, bool isOnline, DateTime? lastFullPoll)
        {
            var now = DateTime.Now;
            
            // Если ККМ недоступна по сети
            if (!isOnline)
            {
                // Если последний полный опрос был более 60 минут назад
                if (lastFullPoll.HasValue && (now - lastFullPoll.Value).TotalMinutes > 60)
                    return "DANGER";
                
                // Если последний полный опрос был более 30 минут назад
                if (lastFullPoll.HasValue && (now - lastFullPoll.Value).TotalMinutes > 30)
                    return "WARNING";
                
                return kktState;
            }
            
            // ККМ доступна по сети
            if (kktState == "OK")
                return "OK";
            
            return kktState;
        }
        
        /// <summary>
        /// Получить описание причины изменения статуса (для email уведомлений)
        /// </summary>
        public string GetStateChangeReason(Kkt kkt, string newState)
        {
            var reasons = new List<string>();
            var now = DateTime.Now;
            
            if (newState == "DANGER")
            {
                if (kkt.KktStatus == "OFFLINE")
                    reasons.Add("ККМ не отвечает по сети (OFFLINE)");
                if (kkt.KktStatus == "ERROR")
                    reasons.Add($"Ошибка при опросе ККМ: {kkt.Error ?? "Неизвестная ошибка"}");
                if (kkt.KktStatus == "TIMEOUT")
                    reasons.Add("Превышен таймаут опроса ККМ");
                if (kkt.SdCardStatus.HasValue && kkt.SdCardStatus.Value != 0)
                    reasons.Add($"Ошибка SD карты (код {kkt.SdCardStatus})");
                if (kkt.FnExpiryDate.HasValue && (kkt.FnExpiryDate.Value - now).TotalDays <= 10)
                    reasons.Add($"Срок действия ФН истекает {(kkt.FnExpiryDate.Value - now).TotalDays:F0} дней");
                if (kkt.FnDocsLeft.HasValue && kkt.FnDocsLeft.Value < 50000)
                    reasons.Add($"Остаточная ёмкость ФН критически мала: {kkt.FnDocsLeft.Value:N0}");
                if (kkt.FnStatus == "ARCHIVE")
                    reasons.Add("Фискальный накопитель закрыт (архив)");
            }
            
            if (newState == "WARNING")
            {
                if (kkt.BatteryVoltage.HasValue && kkt.BatteryVoltage.Value < 3)
                    reasons.Add($"Низкое напряжение батареи: {kkt.BatteryVoltage.Value:F1}V");
                if (kkt.PowerSourceVoltage.HasValue && kkt.PowerSourceVoltage.Value < 24)
                    reasons.Add($"Низкое напряжение источника: {kkt.PowerSourceVoltage.Value:F1}V");
                if (kkt.LastReceiptDate.HasValue && (now - kkt.LastReceiptDate.Value).TotalDays > 14)
                    reasons.Add($"Давно не было чеков: {(now - kkt.LastReceiptDate.Value).TotalDays:F0} дней");
                if (kkt.FnExpiryDate.HasValue)
                {
                    var daysUntilExpiry = (kkt.FnExpiryDate.Value - now).TotalDays;
                    if (daysUntilExpiry > 10 && daysUntilExpiry <= 30)
                        reasons.Add($"Срок действия ФН истекает через {daysUntilExpiry:F0} дней");
                }
                if (kkt.FnDocsLeft.HasValue && kkt.FnDocsLeft.Value < 100000 && kkt.FnDocsLeft.Value >= 50000)
                    reasons.Add($"Остаточная ёмкость ФН: {kkt.FnDocsLeft.Value:N0}");
            }
            
            return reasons.Count > 0 ? string.Join("; ", reasons) : "Изменение состояния ККМ";
        }
        
        /// <summary>
        /// Получить детальное описание текущего статуса (для всплывающего окна)
        /// </summary>
        public string GetStateDetails(Kkt kkt)
        {
            var reasons = new List<string>();
            var now = DateTime.Now;
            string state = kkt.State ?? "UNKNOWN";
            
            if (state == "OK")
            {
                return "✅ Нет ошибок.\nККМ работает в штатном режиме.";
            }
            
            if (state == "WARNING")
            {
                reasons.Add("⚠️ ПРЕДУПРЕЖДЕНИЕ\n");
                
                if (kkt.BatteryVoltage.HasValue && kkt.BatteryVoltage.Value < 3)
                    reasons.Add($"• Напряжение батареи: {kkt.BatteryVoltage.Value:F1} В (норма: ≥ 3 В)");
                if (kkt.PowerSourceVoltage.HasValue && kkt.PowerSourceVoltage.Value < 24)
                    reasons.Add($"• Напряжение источника: {kkt.PowerSourceVoltage.Value:F1} В (норма: ≥ 24 В)");
                if (kkt.LastReceiptDate.HasValue)
                {
                    var daysSinceLastReceipt = (now - kkt.LastReceiptDate.Value).TotalDays;
                    if (daysSinceLastReceipt > 14)
                        reasons.Add($"• Давно не было чеков: {daysSinceLastReceipt:F0} дней");
                }
                if (kkt.FnExpiryDate.HasValue)
                {
                    var daysUntilExpiry = (kkt.FnExpiryDate.Value - now).TotalDays;
                    if (daysUntilExpiry > 10 && daysUntilExpiry <= 30)
                        reasons.Add($"• Срок действия ФН истекает через {daysUntilExpiry:F0} дней");
                }
                if (kkt.FnDocsLeft.HasValue && kkt.FnDocsLeft.Value < 100000 && kkt.FnDocsLeft.Value >= 50000)
                    reasons.Add($"• Остаточная ёмкость ФН: {kkt.FnDocsLeft.Value:N0} (норма: ≥ 100 000)");
            }
            
            if (state == "DANGER")
            {
                reasons.Add("🔴 КРИТИЧЕСКАЯ ОШИБКА\n");
                
                if (kkt.KktStatus == "OFFLINE")
                    reasons.Add($"• ККМ не отвечает по сети (статус: {kkt.KktStatus})");
                else if (kkt.KktStatus == "ERROR")
                    reasons.Add($"• Ошибка при опросе ККМ: {kkt.Error ?? "Неизвестная ошибка"}");
                else if (kkt.KktStatus == "TIMEOUT")
                    reasons.Add("• Превышен таймаут опроса ККМ");
                
                if (kkt.SdCardStatus.HasValue && kkt.SdCardStatus.Value != 0)
                    reasons.Add($"• Ошибка SD карты: код {kkt.SdCardStatus}");
                
                if (kkt.FnExpiryDate.HasValue)
                {
                    var daysUntilExpiry = (kkt.FnExpiryDate.Value - now).TotalDays;
                    if (daysUntilExpiry <= 10)
                        reasons.Add($"• СРОК ДЕЙСТВИЯ ФН ИСТЕКАЕТ через {daysUntilExpiry:F0} дней!");
                }
                
                if (kkt.FnDocsLeft.HasValue && kkt.FnDocsLeft.Value < 50000)
                    reasons.Add($"• Остаточная ёмкость ФН КРИТИЧЕСКИ МАЛА: {kkt.FnDocsLeft.Value:N0}");
                
                if (kkt.FnStatus == "ARCHIVE")
                    reasons.Add("• Фискальный накопитель закрыт (архив)");
            }
            
            if (reasons.Count == 1 && reasons[0].Contains("ПРЕДУПРЕЖДЕНИЕ") || reasons[0].Contains("КРИТИЧЕСКАЯ"))
            {
                reasons.Add("Причина не определена.");
            }
            
            return string.Join("\n", reasons);
        }
    }
}