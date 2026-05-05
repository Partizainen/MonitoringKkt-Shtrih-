using Dapper;
using KKTMonitor.Models;
using System.Data;

namespace KKTMonitor.Services
{
    public class LegalEntityService
    {
        private readonly DbContext _db;

        public LegalEntityService(DbContext db)
        {
            _db = db;
        }

        private IDbConnection CreateConnection() => _db.CreateConnection();

        public async Task<IEnumerable<LegalEntity>> GetAll()
        {
            using var conn = CreateConnection();
            return await conn.QueryAsync<LegalEntity>("SELECT * FROM legal_entities ORDER BY name");
        }

        public async Task<LegalEntity?> GetById(int id)
        {
            using var conn = CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<LegalEntity>(
                "SELECT * FROM legal_entities WHERE id = @id", new { id });
        }

        public async Task<LegalEntity?> GetByInn(string inn)
        {
            if (string.IsNullOrEmpty(inn)) return null;
            
            using var conn = CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<LegalEntity>(
                "SELECT * FROM legal_entities WHERE inn = @inn", new { inn });
        }

        public async Task<LegalEntity?> MatchByKktInn(string kktInn)
        {
            if (string.IsNullOrEmpty(kktInn)) return null;
            
            string normalizedInn = kktInn.TrimStart('0');
            if (string.IsNullOrEmpty(normalizedInn)) return null;
            
            using var conn = CreateConnection();
            
            var result = await conn.QueryFirstOrDefaultAsync<LegalEntity>(
                "SELECT * FROM legal_entities WHERE inn = @normalizedInn",
                new { normalizedInn });
            
            if (result != null)
            {
                Console.WriteLine($"[DEBUG] MatchByKktInn: KKT INN={kktInn} -> normalized={normalizedInn} -> found {result.Name}");
            }
            
            return result;
        }

        public async Task<int> Create(LegalEntity entity)
        {
            using var conn = CreateConnection();
            const string sql = @"
                INSERT INTO legal_entities (name, inn)
                VALUES (@Name, @INN);
                SELECT LAST_INSERT_ID();";
            return await conn.ExecuteScalarAsync<int>(sql, entity);
        }

        public async Task<bool> Update(LegalEntity entity)
        {
            using var conn = CreateConnection();
            const string sql = @"
                UPDATE legal_entities
                SET name = @Name, inn = @INN
                WHERE id = @Id";
            int affected = await conn.ExecuteAsync(sql, entity);
            return affected > 0;
        }

        public async Task<bool> Delete(int id)
        {
            using var conn = CreateConnection();
            await conn.ExecuteAsync("UPDATE kkt SET legal_entity_id = NULL WHERE legal_entity_id = @id", new { id });
            int affected = await conn.ExecuteAsync("DELETE FROM legal_entities WHERE id = @id", new { id });
            return affected > 0;
        }
    }
}