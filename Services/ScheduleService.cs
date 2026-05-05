using Dapper;
using KKTMonitor.Models;
using System.Data;

namespace KKTMonitor.Services
{
    public class ScheduleService
    {
        private readonly DbContext _db;

        public ScheduleService(DbContext db)
        {
            _db = db;
        }

        private IDbConnection CreateConnection() => _db.CreateConnection();

        public async Task<IEnumerable<ScheduleSettings>> GetAll()
        {
            using var conn = CreateConnection();
            var result = await conn.QueryAsync<ScheduleSettings>(@"
                SELECT 
                    id AS Id,
                    schedule_time AS ScheduleTime,
                    is_active AS IsActive,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt
                FROM schedule_settings 
                ORDER BY schedule_time");
            
            return result;
        }

        public async Task<ScheduleSettings?> GetById(int id)
        {
            using var conn = CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<ScheduleSettings>(@"
                SELECT 
                    id AS Id,
                    schedule_time AS ScheduleTime,
                    is_active AS IsActive,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt
                FROM schedule_settings 
                WHERE id = @id", new { id });
        }

        public async Task<int> Create(ScheduleSettings schedule)
        {
            using var conn = CreateConnection();
            const string sql = @"
                INSERT INTO schedule_settings (schedule_time, is_active, created_at, updated_at)
                VALUES (@ScheduleTime, @IsActive, @CreatedAt, @UpdatedAt);
                SELECT LAST_INSERT_ID();";
            return await conn.ExecuteScalarAsync<int>(sql, schedule);
        }

        public async Task<bool> Update(ScheduleSettings schedule)
        {
            using var conn = CreateConnection();
            const string sql = @"
                UPDATE schedule_settings
                SET schedule_time = @ScheduleTime, is_active = @IsActive, updated_at = @UpdatedAt
                WHERE id = @Id";
            int affected = await conn.ExecuteAsync(sql, schedule);
            return affected > 0;
        }

        public async Task<bool> Delete(int id)
        {
            using var conn = CreateConnection();
            int affected = await conn.ExecuteAsync("DELETE FROM schedule_settings WHERE id = @id", new { id });
            return affected > 0;
        }

        public async Task<bool> ToggleActive(int id, bool isActive)
        {
            using var conn = CreateConnection();
            int affected = await conn.ExecuteAsync(@"
                UPDATE schedule_settings 
                SET is_active = @isActive, updated_at = @now
                WHERE id = @id", 
                new { id, isActive, now = DateTime.Now });
            return affected > 0;
        }

        public async Task<List<string>> GetActiveScheduleTimes()
        {
            using var conn = CreateConnection();
            var times = await conn.QueryAsync<string>(@"
                SELECT schedule_time 
                FROM schedule_settings 
                WHERE is_active = 1 
                ORDER BY schedule_time");
            return times.ToList();
        }
    }
}