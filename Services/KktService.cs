using Dapper;
using KKTMonitor.Models;
using System.Data;

namespace KKTMonitor.Services
{
    public class KktService
    {
        private readonly DbContext _db;

        public KktService(DbContext db)
        {
            _db = db;
        }

        public IDbConnection CreateConnection() => _db.CreateConnection();

        public async Task UpsertKkt(string ip, string source)
        {
            using var conn = CreateConnection();
            var existing = await conn.QueryFirstOrDefaultAsync<Kkt>(
                "SELECT * FROM kkt WHERE ip = @ip", new { ip });

            var now = DateTime.Now;
            if (existing == null)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO kkt (ip, source, last_seen, is_active)
                    VALUES (@ip, @source, @now, 1)",
                    new { ip, source, now });
                Console.WriteLine("[DEBUG] UpsertKkt: INSERT new KKT " + ip + " with source " + source);
            }
            else
            {
                await conn.ExecuteAsync(@"
                    UPDATE kkt
                    SET last_seen = @now, source = @source
                    WHERE ip = @ip",
                    new { ip, source, now });
                Console.WriteLine("[DEBUG] UpsertKkt: UPDATE KKT " + ip + " with source " + source);
            }
        }

        public async Task<IEnumerable<dynamic>> GetAllWithLegalName(bool includeInactive = false)
        {
            using var conn = CreateConnection();
            string sql = @"
                SELECT 
                    k.id,
                    k.serial_number AS SerialNumber,
                    k.fn_number AS FnNumber,
                    k.nickname AS Nickname,
                    k.ip,
                    k.source,
                    k.last_seen AS LastSeen,
                    k.last_check AS LastCheck,
                    k.last_full_poll AS LastFullPoll,
                    k.fn_status AS FnStatus,
                    k.ofd_status AS OfdStatus,
                    k.kkt_status AS KktStatus,
                    k.error AS Error,
                    k.state AS State,
                    k.inn AS INN,
                    k.last_buy_receipt_number AS LastBuyReceiptNumber,
                    k.last_receipt_number AS LastReceiptNumber,
                    k.last_receipt_date AS LastReceiptDate,
                    k.first_receipt_date AS FirstReceiptDate,
                    k.software_version AS SoftwareVersion,
                    k.software_build AS SoftwareBuild,
                    k.ofd_server AS OfdServer,
                    k.shift_state AS ShiftState,
                    k.fn_docs_left AS FnDocsLeft,
                    k.fn_detailed_status AS FnDetailedStatus,
                    k.ffd_version AS FfdVersion,
                    k.is_polling_stopped AS IsPollingStopped,
                    k.is_active AS IsActive,
                    k.deleted_at AS DeletedAt,
                    k.ofd_url AS OfdUrl,
                    k.ofd_name AS OfdName,
                    k.ofd_inn AS OfdInn,
                    k.tax_office_url AS TaxOfficeUrl,
                    k.user_name AS UserName,
                    k.operator_name AS OperatorName,
                    k.address AS Address,
                    k.place_of_settlement AS PlaceOfSettlement,
                    k.sender_email AS SenderEmail,
                    k.rnm AS Rnm,
                    k.tax_system AS TaxSystem,
                    k.ecr_mode AS EcrMode,
                    k.ecr_mode_description AS EcrModeDescription,
                    k.ecr_advanced_mode AS EcrAdvancedMode,
                    k.ecr_advanced_mode_description AS EcrAdvancedModeDescription,
                    k.ecr_mode_status AS EcrModeStatus,
                    k.ecr_mode_status_description AS EcrModeStatusDescription,
                    k.battery_voltage AS BatteryVoltage,
                    k.power_source_voltage AS PowerSourceVoltage,
                    k.sd_card_status AS SdCardStatus,
                    k.free_registration AS FreeRegistration,
                    k.registration_number AS RegistrationNumber,
                    k.fn_expiry_date AS FnExpiryDate,
                    k.fn_life_state AS FnLifeState,
                    k.fn_life_state_description AS FnLifeStateDescription,
                    le.name AS LegalName
                FROM kkt k
                LEFT JOIN legal_entities le ON k.legal_entity_id = le.id";
            if (!includeInactive) sql += " WHERE k.is_active = 1";
            sql += " ORDER BY k.id";
            
            var result = await conn.QueryAsync<dynamic>(sql);
            
            var camelCaseResult = result.Select(r => new
            {
                id = r.id,
                serialNumber = r.SerialNumber,
                fnNumber = r.FnNumber,
                nickname = r.Nickname,
                ip = r.ip,
                source = r.source,
                lastSeen = r.LastSeen,
                lastCheck = r.LastCheck,
                lastFullPoll = r.LastFullPoll,
                fnStatus = r.FnStatus,
                ofdStatus = r.OfdStatus,
                kktStatus = r.KktStatus,
                error = r.Error,
                state = r.State,
                inn = r.INN,
                lastBuyReceiptNumber = r.LastBuyReceiptNumber,
                lastReceiptNumber = r.LastReceiptNumber,
                lastReceiptDate = r.LastReceiptDate,
                firstReceiptDate = r.FirstReceiptDate,
                softwareVersion = r.SoftwareVersion,
                softwareBuild = r.SoftwareBuild,
                ofdServer = r.OfdServer,
                shiftState = r.ShiftState,
                fnDocsLeft = r.FnDocsLeft,
                fnDetailedStatus = r.FnDetailedStatus,
                ffdVersion = r.FfdVersion,
                isPollingStopped = r.IsPollingStopped,
                isActive = r.IsActive,
                deletedAt = r.DeletedAt,
                ofdUrl = r.OfdUrl,
                ofdName = r.OfdName,
                ofdInn = r.OfdInn,
                taxOfficeUrl = r.TaxOfficeUrl,
                userName = r.UserName,
                operatorName = r.OperatorName,
                address = r.Address,
                placeOfSettlement = r.PlaceOfSettlement,
                senderEmail = r.SenderEmail,
                rnm = r.Rnm,
                taxSystem = r.TaxSystem,
                ecrMode = r.EcrMode,
                ecrModeDescription = r.EcrModeDescription,
                ecrAdvancedMode = r.EcrAdvancedMode,
                ecrAdvancedModeDescription = r.EcrAdvancedModeDescription,
                ecrModeStatus = r.EcrModeStatus,
                ecrModeStatusDescription = r.EcrModeStatusDescription,
                batteryVoltage = r.BatteryVoltage,
                powerSourceVoltage = r.PowerSourceVoltage,
                sdCardStatus = r.SdCardStatus,
                freeRegistration = r.FreeRegistration,
                registrationNumber = r.RegistrationNumber,
                fnExpiryDate = r.FnExpiryDate,
                fnLifeState = r.FnLifeState,
                fnLifeStateDescription = r.FnLifeStateDescription,
                legalName = r.LegalName
            });
            
            return camelCaseResult;
        }

        public async Task<IEnumerable<Kkt>> GetAllRaw(bool includeInactive = false)
        {
            using var conn = CreateConnection();
            string sql = @"
                SELECT 
                    id,
                    serial_number AS SerialNumber,
                    fn_number AS FnNumber,
                    nickname AS Nickname,
                    ip,
                    source,
                    last_seen AS LastSeen,
                    last_check AS LastCheck,
                    last_full_poll AS LastFullPoll,
                    fn_status AS FnStatus,
                    ofd_status AS OfdStatus,
                    kkt_status AS KktStatus,
                    error AS Error,
                    state AS State,
                    fn_expiry_date AS FnExpiryDate,
                    shift_state AS ShiftState,
                    fn_docs_left AS FnDocsLeft,
                    inn AS INN,
                    legal_entity_id AS LegalEntityId,
                    last_buy_receipt_number AS LastBuyReceiptNumber,
                    last_receipt_number AS LastReceiptNumber,
                    last_receipt_date AS LastReceiptDate,
                    first_receipt_date AS FirstReceiptDate,
                    software_version AS SoftwareVersion,
                    software_build AS SoftwareBuild,
                    ofd_server AS OfdServer,
                    fn_detailed_status AS FnDetailedStatus,
                    ffd_version AS FfdVersion,
                    is_active AS IsActive,
                    deleted_at AS DeletedAt,
                    is_polling_stopped AS IsPollingStopped,
                    ofd_url AS OfdUrl,
                    ofd_name AS OfdName,
                    ofd_inn AS OfdInn,
                    tax_office_url AS TaxOfficeUrl,
                    user_name AS UserName,
                    operator_name AS OperatorName,
                    address AS Address,
                    place_of_settlement AS PlaceOfSettlement,
                    sender_email AS SenderEmail,
                    rnm AS Rnm,
                    tax_system AS TaxSystem,
                    ecr_mode AS EcrMode,
                    ecr_mode_description AS EcrModeDescription,
                    ecr_advanced_mode AS EcrAdvancedMode,
                    ecr_advanced_mode_description AS EcrAdvancedModeDescription,
                    ecr_mode_status AS EcrModeStatus,
                    ecr_mode_status_description AS EcrModeStatusDescription,
                    battery_voltage AS BatteryVoltage,
                    power_source_voltage AS PowerSourceVoltage,
                    sd_card_status AS SdCardStatus,
                    sd_card_cluster_size AS SdCardClusterSize,
                    sd_card_total_sectors AS SdCardTotalSectors,
                    sd_card_free_sectors AS SdCardFreeSectors,
                    sd_card_io_errors AS SdCardIoErrors,
                    sd_card_retry_count AS SdCardRetryCount,
                    free_registration AS FreeRegistration,
                    registration_number AS RegistrationNumber,
                    fn_life_state AS FnLifeState,
                    fn_life_state_description AS FnLifeStateDescription,
                    shift_open_time AS ShiftOpenTime
                FROM kkt";
            if (!includeInactive) sql += " WHERE is_active = 1";
            return await conn.QueryAsync<Kkt>(sql);
        }

        public async Task<dynamic?> GetById(int id)
        {
            using var conn = CreateConnection();
            var result = await conn.QueryFirstOrDefaultAsync(@"
                SELECT 
                    k.id,
                    k.serial_number AS SerialNumber,
                    k.fn_number AS FnNumber,
                    k.nickname AS Nickname,
                    k.ip,
                    k.source,
                    k.last_seen AS LastSeen,
                    k.last_check AS LastCheck,
                    k.last_full_poll AS LastFullPoll,
                    k.fn_status AS FnStatus,
                    k.ofd_status AS OfdStatus,
                    k.kkt_status AS KktStatus,
                    k.error AS Error,
                    k.state AS State,
                    k.inn AS INN,
                    k.last_buy_receipt_number AS LastBuyReceiptNumber,
                    k.last_receipt_number AS LastReceiptNumber,
                    k.last_receipt_date AS LastReceiptDate,
                    k.first_receipt_date AS FirstReceiptDate,
                    k.software_version AS SoftwareVersion,
                    k.software_build AS SoftwareBuild,
                    k.ofd_server AS OfdServer,
                    k.shift_state AS ShiftState,
                    k.fn_docs_left AS FnDocsLeft,
                    k.fn_detailed_status AS FnDetailedStatus,
                    k.ffd_version AS FfdVersion,
                    k.is_active AS IsActive,
                    k.deleted_at AS DeletedAt,
                    k.is_polling_stopped AS IsPollingStopped,
                    k.ofd_url AS OfdUrl,
                    k.ofd_name AS OfdName,
                    k.ofd_inn AS OfdInn,
                    k.tax_office_url AS TaxOfficeUrl,
                    k.user_name AS UserName,
                    k.operator_name AS OperatorName,
                    k.address AS Address,
                    k.place_of_settlement AS PlaceOfSettlement,
                    k.sender_email AS SenderEmail,
                    k.rnm AS Rnm,
                    k.tax_system AS TaxSystem,
                    k.ecr_mode AS EcrMode,
                    k.ecr_mode_description AS EcrModeDescription,
                    k.ecr_advanced_mode AS EcrAdvancedMode,
                    k.ecr_advanced_mode_description AS EcrAdvancedModeDescription,
                    k.ecr_mode_status AS EcrModeStatus,
                    k.ecr_mode_status_description AS EcrModeStatusDescription,
                    k.battery_voltage AS BatteryVoltage,
                    k.power_source_voltage AS PowerSourceVoltage,
                    k.sd_card_status AS SdCardStatus,
                    k.sd_card_cluster_size AS SdCardClusterSize,
                    k.sd_card_total_sectors AS SdCardTotalSectors,
                    k.sd_card_free_sectors AS SdCardFreeSectors,
                    k.sd_card_io_errors AS SdCardIoErrors,
                    k.sd_card_retry_count AS SdCardRetryCount,
                    k.free_registration AS FreeRegistration,
                    k.registration_number AS RegistrationNumber,
                    k.fn_expiry_date AS FnExpiryDate,
                    k.fn_life_state AS FnLifeState,
                    k.fn_life_state_description AS FnLifeStateDescription,
                    le.name AS LegalName
                FROM kkt k
                LEFT JOIN legal_entities le ON k.legal_entity_id = le.id
                WHERE k.id = @id", new { id });
            
            if (result != null)
            {
                Console.WriteLine("[DB] GetById " + id + ": SerialNumber=" + result.SerialNumber + ", IsPollingStopped=" + result.IsPollingStopped);
            }
            
            return result;
        }

        public async Task<Kkt?> GetKktById(int id)
        {
            using var conn = CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Kkt>(@"
                SELECT 
                    id,
                    serial_number AS SerialNumber,
                    fn_number AS FnNumber,
                    nickname AS Nickname,
                    ip,
                    source,
                    last_seen AS LastSeen,
                    last_check AS LastCheck,
                    last_full_poll AS LastFullPoll,
                    fn_status AS FnStatus,
                    ofd_status AS OfdStatus,
                    kkt_status AS KktStatus,
                    error AS Error,
                    state AS State,
                    fn_expiry_date AS FnExpiryDate,
                    shift_state AS ShiftState,
                    fn_docs_left AS FnDocsLeft,
                    inn AS INN,
                    legal_entity_id AS LegalEntityId,
                    last_buy_receipt_number AS LastBuyReceiptNumber,
                    last_receipt_number AS LastReceiptNumber,
                    last_receipt_date AS LastReceiptDate,
                    first_receipt_date AS FirstReceiptDate,
                    software_version AS SoftwareVersion,
                    software_build AS SoftwareBuild,
                    ofd_server AS OfdServer,
                    fn_detailed_status AS FnDetailedStatus,
                    ffd_version AS FfdVersion,
                    is_active AS IsActive,
                    deleted_at AS DeletedAt,
                    is_polling_stopped AS IsPollingStopped,
                    ofd_url AS OfdUrl,
                    ofd_name AS OfdName,
                    ofd_inn AS OfdInn,
                    tax_office_url AS TaxOfficeUrl,
                    user_name AS UserName,
                    operator_name AS OperatorName,
                    address AS Address,
                    place_of_settlement AS PlaceOfSettlement,
                    sender_email AS SenderEmail,
                    rnm AS Rnm,
                    tax_system AS TaxSystem,
                    ecr_mode AS EcrMode,
                    ecr_mode_description AS EcrModeDescription,
                    ecr_advanced_mode AS EcrAdvancedMode,
                    ecr_advanced_mode_description AS EcrAdvancedModeDescription,
                    ecr_mode_status AS EcrModeStatus,
                    ecr_mode_status_description AS EcrModeStatusDescription,
                    battery_voltage AS BatteryVoltage,
                    power_source_voltage AS PowerSourceVoltage,
                    sd_card_status AS SdCardStatus,
                    sd_card_cluster_size AS SdCardClusterSize,
                    sd_card_total_sectors AS SdCardTotalSectors,
                    sd_card_free_sectors AS SdCardFreeSectors,
                    sd_card_io_errors AS SdCardIoErrors,
                    sd_card_retry_count AS SdCardRetryCount,
                    free_registration AS FreeRegistration,
                    registration_number AS RegistrationNumber,
                    fn_life_state AS FnLifeState,
                    fn_life_state_description AS FnLifeStateDescription,
                    shift_open_time AS ShiftOpenTime
                FROM kkt
                WHERE id = @id", new { id });
        }

        public async Task UpdateKktData(Kkt kkt)
        {
            using var conn = CreateConnection();
            
            var result = await conn.ExecuteAsync(@"
                UPDATE kkt SET
                    serial_number = @SerialNumber,
                    fn_number = @FnNumber,
                    nickname = @Nickname,
                    last_seen = @LastSeen,
                    last_check = @LastCheck,
                    last_full_poll = @LastFullPoll,
                    fn_status = @FnStatus,
                    ofd_status = @OfdStatus,
                    kkt_status = @KktStatus,
                    error = @Error,
                    state = @State,
                    fn_expiry_date = @FnExpiryDate,
                    shift_state = @ShiftState,
                    fn_docs_left = @FnDocsLeft,
                    inn = @INN,
                    legal_entity_id = @LegalEntityId,
                    last_buy_receipt_number = @LastBuyReceiptNumber,
                    last_receipt_number = @LastReceiptNumber,
                    last_receipt_date = @LastReceiptDate,
                    first_receipt_date = @FirstReceiptDate,
                    software_version = @SoftwareVersion,
                    software_build = @SoftwareBuild,
                    ofd_server = @OfdServer,
                    fn_detailed_status = @FnDetailedStatus,
                    ffd_version = @FfdVersion,
                    is_polling_stopped = @IsPollingStopped,
                    ofd_url = @OfdUrl,
                    ofd_name = @OfdName,
                    ofd_inn = @OfdInn,
                    tax_office_url = @TaxOfficeUrl,
                    user_name = @UserName,
                    operator_name = @OperatorName,
                    address = @Address,
                    place_of_settlement = @PlaceOfSettlement,
                    sender_email = @SenderEmail,
                    rnm = @Rnm,
                    tax_system = @TaxSystem,
                    ecr_mode = @EcrMode,
                    ecr_mode_description = @EcrModeDescription,
                    ecr_advanced_mode = @EcrAdvancedMode,
                    ecr_advanced_mode_description = @EcrAdvancedModeDescription,
                    ecr_mode_status = @EcrModeStatus,
                    ecr_mode_status_description = @EcrModeStatusDescription,
                    battery_voltage = @BatteryVoltage,
                    power_source_voltage = @PowerSourceVoltage,
                    sd_card_status = @SdCardStatus,
                    sd_card_cluster_size = @SdCardClusterSize,
                    sd_card_total_sectors = @SdCardTotalSectors,
                    sd_card_free_sectors = @SdCardFreeSectors,
                    sd_card_io_errors = @SdCardIoErrors,
                    sd_card_retry_count = @SdCardRetryCount,
                    free_registration = @FreeRegistration,
                    registration_number = @RegistrationNumber,
                    fn_life_state = @FnLifeState,
                    fn_life_state_description = @FnLifeStateDescription
                WHERE ip = @Ip", kkt);
            
            Console.WriteLine("[DEBUG] UpdateKktData for " + kkt.Ip + ": " +
                "serial=" + kkt.SerialNumber + ", " +
                "state=" + kkt.State + ", " +
                "fnLifeState=" + kkt.FnLifeState + ", " +
                "fnExpiryDate=" + kkt.FnExpiryDate + ", " +
                "isPollingStopped=" + kkt.IsPollingStopped + ", " +
                "rows=" + result);
        }

        public async Task UpdateKktStatus(string ip, string kktStatus, string state)
        {
            using var conn = CreateConnection();
            
            var result = await conn.ExecuteAsync(@"
                UPDATE kkt SET
                    kkt_status = @kktStatus,
                    state = @state,
                    last_check = @now
                WHERE ip = @ip",
                new { ip, kktStatus, state, now = DateTime.Now });
            
            Console.WriteLine("[DEBUG] UpdateKktStatus for " + ip + ": status=" + kktStatus + ", state=" + state + ", rows=" + result);
        }

        public async Task UpdateKktState(Kkt kkt, string newState)
        {
            using var conn = CreateConnection();
            
            var result = await conn.ExecuteAsync(@"
                UPDATE kkt SET
                    state = @newState,
                    kkt_status = @kktStatus,
                    last_check = @now
                WHERE id = @id",
                new { 
                    newState, 
                    kktStatus = kkt.KktStatus ?? "ONLINE",
                    now = DateTime.Now,
                    id = kkt.Id 
                });
            
            Console.WriteLine("[DEBUG] UpdateKktState for " + kkt.Ip + ": state=" + newState + ", rows=" + result);
        }

        public async Task UpdateNickname(int id, string nickname)
        {
            using var conn = CreateConnection();
            await conn.ExecuteAsync("UPDATE kkt SET nickname = @nickname WHERE id = @id", new { id, nickname });
            Console.WriteLine("[DEBUG] UpdateNickname: id=" + id + ", nickname=" + nickname);
        }

        public async Task UpdateLegalEntity(int id, int? legalEntityId)
        {
            using var conn = CreateConnection();
            await conn.ExecuteAsync("UPDATE kkt SET legal_entity_id = @legalEntityId WHERE id = @id", new { id, legalEntityId });
            Console.WriteLine("[DEBUG] UpdateLegalEntity: id=" + id + ", legalEntityId=" + legalEntityId);
        }

        public async Task UpdatePollingStatus(int id, bool isStopped)
        {
            using var conn = CreateConnection();
            var result = await conn.ExecuteAsync(@"
                UPDATE kkt SET is_polling_stopped = @isStopped
                WHERE id = @id", new { id, isStopped });
            
            Console.WriteLine("[DEBUG] UpdatePollingStatus: id=" + id + ", isStopped=" + isStopped + ", rows=" + result);
        }

        public async Task<Kkt?> GetByIp(string ip)
        {
            using var conn = CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Kkt>(@"
                SELECT 
                    id,
                    serial_number AS SerialNumber,
                    fn_number AS FnNumber,
                    nickname AS Nickname,
                    ip,
                    source,
                    last_seen AS LastSeen,
                    last_check AS LastCheck,
                    last_full_poll AS LastFullPoll,
                    fn_status AS FnStatus,
                    ofd_status AS OfdStatus,
                    kkt_status AS KktStatus,
                    error AS Error,
                    state AS State,
                    fn_expiry_date AS FnExpiryDate,
                    shift_state AS ShiftState,
                    fn_docs_left AS FnDocsLeft,
                    inn AS INN,
                    legal_entity_id AS LegalEntityId,
                    last_buy_receipt_number AS LastBuyReceiptNumber,
                    last_receipt_number AS LastReceiptNumber,
                    last_receipt_date AS LastReceiptDate,
                    first_receipt_date AS FirstReceiptDate,
                    software_version AS SoftwareVersion,
                    software_build AS SoftwareBuild,
                    ofd_server AS OfdServer,
                    fn_detailed_status AS FnDetailedStatus,
                    ffd_version AS FfdVersion,
                    is_active AS IsActive,
                    deleted_at AS DeletedAt,
                    is_polling_stopped AS IsPollingStopped
                FROM kkt
                WHERE ip = @ip", new { ip });
        }

        public async Task<List<string>> GetAllActiveIps()
        {
            using var conn = CreateConnection();
            var ips = await conn.QueryAsync<string>(
                "SELECT ip FROM kkt WHERE is_active = 1 AND ip IS NOT NULL");
            return ips.ToList();
        }

        public async Task MarkAsInactive(string ip)
        {
            using var conn = CreateConnection();
            await conn.ExecuteAsync(@"
                UPDATE kkt 
                SET is_active = 0, deleted_at = @now
                WHERE ip = @ip AND is_active = 1",
                new { ip, now = DateTime.Now });
            Console.WriteLine("[DEBUG] Marked " + ip + " as inactive");
        }

        public async Task<bool> SoftDelete(int id)
        {
            using var conn = CreateConnection();
            int affected = await conn.ExecuteAsync(@"
                UPDATE kkt SET is_active = 0, deleted_at = @now
                WHERE id = @id AND is_active = 1", new { id, now = DateTime.Now });
            return affected > 0;
        }

        public async Task<bool> Restore(int id)
        {
            using var conn = CreateConnection();
            int affected = await conn.ExecuteAsync(@"
                UPDATE kkt SET is_active = 1, deleted_at = NULL
                WHERE id = @id AND is_active = 0", new { id });
            return affected > 0;
        }

        public async Task<bool> DeletePermanently(int id)
        {
            using var conn = CreateConnection();
            int affected = await conn.ExecuteAsync("DELETE FROM kkt WHERE id = @id", new { id });
            return affected > 0;
        }

        public async Task<int> SyncLegalEntities(LegalEntityService legalService)
        {
            var allKkt = await GetAllRaw(true);
            int updated = 0;
            
            foreach (var kkt in allKkt)
            {
                if (!string.IsNullOrEmpty(kkt.INN))
                {
                    var legal = await legalService.MatchByKktInn(kkt.INN);
                    if (legal != null && kkt.LegalEntityId != legal.Id)
                    {
                        await UpdateLegalEntity(kkt.Id, legal.Id);
                        updated++;
                        Console.WriteLine("[DEBUG] SyncLegalEntities: Matched KKT " + kkt.Ip + " (INN=" + kkt.INN + ") with legal entity " + legal.Name);
                    }
                }
            }
            
            Console.WriteLine("[DEBUG] SyncLegalEntities: Total updated=" + updated);
            return updated;
        }

        public async Task<bool> ForceMatchLegalEntity(int kktId, int legalEntityId)
        {
            using var conn = CreateConnection();
            int affected = await conn.ExecuteAsync(@"
                UPDATE kkt SET legal_entity_id = @legalEntityId
                WHERE id = @kktId", new { kktId, legalEntityId });
            return affected > 0;
        }

        public async Task<object> GetStats()
        {
            using var conn = CreateConnection();
            var stats = await conn.QueryFirstOrDefaultAsync(@"
                SELECT 
                    COUNT(*) AS Total,
                    SUM(CASE WHEN state = 'OK' THEN 1 ELSE 0 END) AS OkCount,
                    SUM(CASE WHEN state = 'WARNING' THEN 1 ELSE 0 END) AS WarningCount,
                    SUM(CASE WHEN state = 'DANGER' THEN 1 ELSE 0 END) AS DangerCount,
                    SUM(CASE WHEN is_active = 1 THEN 1 ELSE 0 END) AS ActiveCount
                FROM kkt");
            return stats!;
        }
    }
}