using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SampleConsole.Data;
using SampleConsole.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SampleConsole.Data
{
    static class DbContextConstrants
    {
        public const string RowLevelSecuritySettingKey = "app.current_tenant";
    }

    public class SampleConnection : IAsyncDisposable
    {
        public NpgsqlConnection Connection { get; private set; }
        public long Tenant { get; }

        private readonly ConnectionProvider _connectionProvider;

        public SampleConnection(ConnectionProvider connectionProvider, long tenant)
        {
            _connectionProvider = connectionProvider;
            Tenant = tenant;
        }

        public async Task GetConnectionAsync()
        {
            Connection = await _connectionProvider.GetConnectionAsync(Tenant);
        }
        public async Task GetConnectionAsync2()
        {
            Connection = await _connectionProvider.GetConnectionAsync2(Tenant);
        }
        public ValueTask<NpgsqlTransaction> BeginTransactionAsync(CancellationToken ct = default) => Connection.BeginTransactionAsync(ct);
        public ValueTask<NpgsqlTransaction> BeginTransactionAsync(IsolationLevel level, CancellationToken ct = default) => Connection.BeginTransactionAsync(level, ct);

        public async ValueTask DisposeAsync()
        {
            if (Connection != null && Connection.State == ConnectionState.Open)
            {
                await Connection.QueryAsync($"SET SESSION {DbContextConstrants.RowLevelSecuritySettingKey} = DEFAULT");
                await Connection.DisposeAsync();
            }
        }
    }
    public class ConnectionProvider
    {
        public IConfiguration _config;

        public ConnectionProvider(IConfiguration config)
        {
            _config = config;
        }

        public async Task<NpgsqlConnection> GetConnectionAsync(long tenant)
        {
            var conn = new NpgsqlConnection(_config.GetConnectionString("AppDbContext"));
            await conn.OpenAsync();
            await conn.QueryAsync($"SET SESSION {DbContextConstrants.RowLevelSecuritySettingKey} = '{tenant}'");
            var after = await conn.QueryAsync($"SHOW {DbContextConstrants.RowLevelSecuritySettingKey}");
            foreach (var item in after)
                Console.WriteLine($"{nameof(GetConnectionAsync)}: {item}");
            return conn;
        }
        public async Task<NpgsqlConnection> GetConnectionAsync2(long tenant)
        {
            var conn = new NpgsqlConnection(_config.GetConnectionString("AppDbContext"));
            await conn.OpenAsync();
            var before = await conn.QueryAsync($"SHOW {DbContextConstrants.RowLevelSecuritySettingKey}");
            foreach (var item in before)
                Console.WriteLine($"{nameof(GetConnectionAsync2)}: {item}");
            await conn.QueryAsync($"SET SESSION {DbContextConstrants.RowLevelSecuritySettingKey} = '{tenant}'");
            return conn;
        }
    }

    public class PostgresDbContext : DbContext
    {
        public PostgresDbContext(DbContextOptions<PostgresDbContext> options) : base(options)
        {
        }

        public DbSet<Condition> Conditions { get; set; }
    }

    /// <summary>
    /// MigrationOperationGenerator for Postgres.
    /// * Generate RowLevelSecurity SQL when Table has <see cref="RowLevelSecurityAttribute">
    /// </summary>
    public class PostgresMigrationOperationGenerator : CSharpMigrationOperationGenerator
    {
        private readonly Dictionary<string, RowLevelSecurityAttribute> rowLevelSecurities;

        public PostgresMigrationOperationGenerator(CSharpMigrationOperationGeneratorDependencies dependencies) : base(dependencies)
        {
            rowLevelSecurities = Assembly.GetExecutingAssembly().GetTypes()
                .Select(x => x.GetCustomAttribute<RowLevelSecurityAttribute>(true))
                .Where(x => x != null)
                .ToDictionary(kv => kv.TableName, kv => kv);
        }
        protected override void Generate(CreateTableOperation operation, IndentedStringBuilder builder)
        {
            base.Generate(operation, builder);

            if (rowLevelSecurities.TryGetValue(operation.Name, out var value))
            {
                builder.Append(";").AppendLine();
                GenerateRowLevelSecurity(value, builder);
            }
        }

        /// <summary>
        /// Auto execute create_hypertable query when Create Table executed.
        /// generated sample: migrationBuilder.Sql("SELECT create_hypertable('TABLE', 'time')");
        /// </summary>
        private void GenerateRowLevelSecurity(RowLevelSecurityAttribute attribute, IndentedStringBuilder builder)
        {
            Console.WriteLine($"Creating RowLevelSecurity migration query for table. table {attribute.TableName}, force {attribute.Force}");
            builder.AppendLine(@$"migrationBuilder.Sql(""ALTER TABLE {attribute.TableName} ENABLE ROW LEVEL SECURITY;"");");
            builder.AppendLine(@$"migrationBuilder.Sql(""CREATE POLICY {attribute.TableName}_isolation_policy ON {attribute.TableName} FOR ALL USING ({attribute.ColumnName} = current_setting('{DbContextConstrants.RowLevelSecuritySettingKey}')::BIGINT);"");");
            if (attribute.Force)
            {
                builder.Append(@$"migrationBuilder.Sql(""ALTER TABLE {attribute.TableName} FORCE ROW LEVEL SECURITY;"")");
            }
        }
    }
    /// <summary>
    /// Automatically discover via EF Core Reflection
    /// </summary>
    public class DesignTimeServices : IDesignTimeServices
    {
        public void ConfigureDesignTimeServices(IServiceCollection services)
            => services.AddSingleton<ICSharpMigrationOperationGenerator, PostgresMigrationOperationGenerator>();
    }

    /// <summary>
    /// for dotnet-ef migrations config initialization.
    /// </summary>
    public class MigrationDbContextFactory : IDesignTimeDbContextFactory<PostgresDbContext>
    {
        private IConfiguration _config;
        public MigrationDbContextFactory()
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddJsonFile("appsettings.json");
            configBuilder.AddJsonFile("appsettings.Production.json", true);
            configBuilder.AddJsonFile("appsettings.Development.json", true);
            _config = configBuilder.Build();
        }
        public PostgresDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PostgresDbContext>();
            optionsBuilder.UseNpgsql(_config.GetConnectionString("MigrationDbContext"));
            return new PostgresDbContext(optionsBuilder.Options);
        }
    }
}

namespace SampleConsole
{
    public static class IServiceCollectionExtensions
    {
        /// <summary>
        /// For EF Query
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<PostgresDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("AppDbContext"));
            });

            return services;
        }

        /// <summary>
        /// For Dapper Query
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddDbConnection(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<ConnectionProvider>(new ConnectionProvider(configuration));
            return services;
        }
    }
}