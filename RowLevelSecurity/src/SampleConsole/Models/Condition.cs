using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SampleConsole.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SampleConsole.Models
{
    [RowLevelSecurity(tableName)]
    [Table(tableName)]
    [Index(nameof(TenantId), nameof(Location), Name = "conditions_tenant_id_location_idx")]
    public class Condition : ModelBase
    {
        private const string tableName = "conditions";
        private static string[] columns = AttributeEx.GetColumns<Condition>(false);
        private static string columnNames = string.Join(",", columns);

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id", Order = 0)]
        public long Id { get; init; }
        [Column("location")]
        public string Location { get; init; }
        [Column("temperature")]
        public double? Temperature { get; set; }
        [Column("humidity")]
        public double? Humidity { get; set; }

        public static async Task<int> InsertBulkAsync(IDbConnection connection, IDbTransaction transaction, IEnumerable<Condition> values, int timeoutSec = 60)
        {
            var rows = await connection.ExecuteAsync(
                @$"INSERT INTO {tableName} ({columnNames}) VALUES (@{nameof(Location)}, @{nameof(Temperature)}, @{nameof(Humidity)}, @{nameof(TenantId)});"
                , values, transaction, timeoutSec);
            return rows;
        }
        public static async Task<int> UpdateLocationAsync(SampleConnection connection, IDbTransaction transaction, string location, int timeoutSec = 60)
        {
            var rows = await connection.Connection.ExecuteAsync(
                @$"UPDATE {tableName} SET location = @location WHERE tenant_id = @tenantId;"
                , new { location = location, tenantId = connection.Tenant }, transaction, timeoutSec);
            return rows;
        }
        public static async Task<int> InsertPrepareAsync(SampleConnection connection, NpgsqlTransaction transaction, Condition value)
        {
            using (var cmd = new NpgsqlCommand(@$"INSERT INTO {tableName} ({columnNames}) VALUES (@{nameof(Location)}, @{nameof(Temperature)}, @{nameof(Humidity)}, @{nameof(TenantId)});", connection.Connection, transaction))
            {
                cmd.Parameters.Add(nameof(Location), NpgsqlTypes.NpgsqlDbType.Varchar);
                cmd.Parameters.Add(nameof(Temperature), NpgsqlTypes.NpgsqlDbType.Double);
                cmd.Parameters.Add(nameof(Humidity), NpgsqlTypes.NpgsqlDbType.Double);
                cmd.Parameters.Add(nameof(TenantId), NpgsqlTypes.NpgsqlDbType.Integer);
                await cmd.PrepareAsync();

                cmd.Parameters[0].Value = value.Location;
                cmd.Parameters[1].Value = value.Temperature;
                cmd.Parameters[2].Value = value.Humidity;
                cmd.Parameters[3].Value = value.TenantId;
                var rows = await cmd.ExecuteNonQueryAsync();
                return rows;
            }
        }

        public static async Task<IEnumerable<Condition>> GetAsync(IDbConnection connection, long tenantId, string location, int count, int timeoutSec = 60)
        {
            var conditions = await connection.QueryAsync<Condition>(
                @$"SELECT * FROM {tableName} WHERE tenant_id = @{nameof(TenantId)} AND location = @{nameof(Location)} LIMIT {count};"
                , new { TenantId = tenantId, Location = location }, commandTimeout: timeoutSec);
            return conditions;
        }

        public static async Task<ulong> CopyAsync(NpgsqlConnection connection, IEnumerable<Condition> values, CancellationToken ct)
        {
            // COPY not support Nullable<T>
            // https://github.com/npgsql/npgsql/issues/1965
            using var writer = connection.BeginBinaryImport($"COPY {tableName} ({columnNames}) FROM STDIN (FORMAT BINARY)");
            foreach (var value in values)
            {
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(value.TenantId, ct);
                await writer.WriteAsync(value.Location, ct);
                await writer.WriteAsync(value.Temperature.Value, ct);
                await writer.WriteAsync(value.Humidity.Value, ct);
            }
            var rows = await writer.CompleteAsync(ct);
            return rows;
        }

        public static Condition[] GenerateRandomOfficeData(int dataCount)
        {
            var random = new Random();
            var location = "オフィス";
            double temp = random.Next(10, 15);
            double hum = random.Next(30, 50);
            var conditions = Enumerable.Range(1, dataCount).Select(x => new Condition
            {
                TenantId = random.Next(1, 10),
                Location = location,
                Temperature = temp + Math.Min(Math.Cos(x % 1000.0 * x) - Math.Sin(x % 100), 30 - temp),
                Humidity = hum + Math.Max(Math.Min(Math.Tan(x % 1000 * x) / Math.Sin(x % 1000), 100 - hum), 0 - hum),
            })
            .ToArray();
            return conditions;
        }

        public static Condition[] GenerateRandomHomeData(int dataCount)
        {
            var random = new Random();
            var location = "家";
            double temp = random.Next(5, 10);
            double hum = random.Next(40, 60);
            var conditions = Enumerable.Range(1, dataCount).Select(x => new Condition
            {
                TenantId = random.Next(1, 10),
                Location = location,
                Temperature = temp + Math.Min(Math.Cos(x % 5000.0 * x) - Math.Sin(x % 200), 20 - temp),
                Humidity = hum + Math.Max(Math.Min(Math.Tan(x % 5000 * x) / Math.Sin(x % 2000), 100 - hum), 0 - hum),
            })
            .ToArray();
            return conditions;
        }
    }
}
