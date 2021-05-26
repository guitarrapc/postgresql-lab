using ConsoleAppFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SampleConsole.Data;
using SampleConsole.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ZLogger;

namespace SampleConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // args = "Runner migrate -count 100000".Split(" ");
            await Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) => services.AddDbConnection(hostContext.Configuration))
                .ConfigureServices((hostContext, services) => services.AddDbContext(hostContext.Configuration))
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddZLoggerConsole(true);
                })
                .RunConsoleAppFrameworkAsync(args);
        }
    }

    public class DDL : ConsoleAppBase
    {
        private readonly ConnectionProvider _connection;
        private readonly PostgresDbContext _dbContext;
        public DDL(ConnectionProvider connection, PostgresDbContext dbContext)
        {
            _connection = connection;
            _dbContext = dbContext;
        }

        [Command("test")]
        public async Task Test(long tenant = 1, string location = "test", double temperature = 1.0, double humidity = 1.0)
        {
            Console.WriteLine("test insert data");
            await using (var conn = await _connection.GetConnectionAsync(tenant))
            using (var transaction = await conn.BeginTransactionAsync(Context.CancellationToken))
            {
                await Condition.InsertBulkAsync(conn, transaction, new[] {
                    new Condition
                    {
                        TenantId = tenant,
                        Location = location,
                        Temperature = temperature,
                        Humidity = humidity,
                    }
                });
                await transaction.CommitAsync();
            }

            Console.WriteLine("test read from database");
            await using var connection = await _connection.GetConnectionAsync2(tenant);
            var conditions = await Condition.GetAsync(connection, tenant, location, 10);
            foreach (var condition in conditions.Take(10))
            {
                Console.WriteLine($"{condition.Id}, {condition.TenantId}, {condition.Location}, {condition.Temperature}, {condition.Humidity}");
            }
        }

        [Command("keep")]
        public async Task Keep(long tenant)
        {
            Console.WriteLine($"keep inserting data");
            await using var connection = await _connection.GetConnectionAsync(tenant);

            var current = 1;
            while (!Context.CancellationToken.IsCancellationRequested)
            {
                Console.Write($"{current} ");
                var time = DateTime.UtcNow;
                var data = Enumerable.Concat(
                    Condition.GenerateRandomOfficeData(1),
                    Condition.GenerateRandomHomeData(1)
                )
                .ToArray();
                using (var transaction = await connection.BeginTransactionAsync(Context.CancellationToken))
                {
                    await Condition.InsertBulkAsync(connection, transaction, data);
                    await transaction.CommitAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(1), Context.CancellationToken);
                current++;
            }
        }

        [Command("random")]
        public async Task Random(long tenant, int count = 1000)
        {
            var data = Enumerable.Concat(
                Condition.GenerateRandomOfficeData(count),
                Condition.GenerateRandomHomeData(count)
            )
            .ToArray();

            Console.WriteLine($"insert random {count} data");
            await using var connection = await _connection.GetConnectionAsync(tenant);
            using (var transaction = await connection.BeginTransactionAsync(Context.CancellationToken))
            {
                await connection.OpenAsync(Context.CancellationToken);
                await Condition.InsertBulkAsync(connection, transaction, data);
                await transaction.CommitAsync();
            }

            Console.WriteLine("read from database");
            var conditions = await Condition.GetAsync(connection, tenant, "オフィス", 10);
            foreach (var condition in conditions.Take(10))
            {
                Console.WriteLine($"{condition.Id}, {condition.TenantId}, {condition.Location}, {condition.Temperature}, {condition.Humidity}");
            }
        }
    }

    public class DML : ConsoleAppBase
    {
        private readonly ConnectionProvider _connection;
        private readonly PostgresDbContext _dbContext;
        public DML(ConnectionProvider connection, PostgresDbContext dbContext)
        {
            _connection = connection;
            _dbContext = dbContext;
        }

        [Command("migrate")]
        public async Task Migrate()
        {
            await _dbContext.Database.MigrateAsync(Context.CancellationToken);
        }

        [Command("seed")]
        public async Task Seed(int parallel = 100, int count = 10000)
        {
            Console.WriteLine($"Begin seed database. {count} rows, parallel {parallel}");
            var size = count;
            var initData = Condition.GenerateRandomOfficeData(size);
            var groups = initData.GroupBy(x => x.TenantId).ToArray();

            var gate = new object();
            var completed = 0;
            var tasks = new List<Task>();
            var ct = Context.CancellationToken;
            var sw = Stopwatch.StartNew();
            foreach (var group in groups)
            {
                var task = Task.Run(async () =>
                {
                    await using (var connection = await _connection.GetConnectionAsync(group.Key))
                    {
                        // 10000 will cause timeout
                        foreach (var data in group.Buffer(1000))
                        {
                            using (var transaction = await connection.BeginTransactionAsync(ct))
                            {
                                try
                                {
                                    var rows = await Condition.InsertBulkAsync(connection, transaction, data);
                                    await transaction.CommitAsync(ct);
                                    lock (gate)
                                    {
                                        completed += rows;
                                    }
                                    Console.WriteLine($"complete {completed}/{count}");
                                }
                                catch (Exception ex)
                                {
                                    Console.Error.WriteLine(ex);
                                    await transaction.RollbackAsync(ct);
                                }
                            }
                        }
                    }
                }, ct);
                tasks.Add(task);
            }
            await Task.WhenAll(tasks);
            Console.WriteLine($"Complete seed database. plan {count}, completed {completed}, duration {sw.Elapsed.TotalSeconds}sec");
        }

        [Command("seedcopy")]
        public async Task SeedCopy(int parallel = 100, int count = 10000)
        {
            Console.WriteLine($"Begin seed database (copy). {count} rows, parallel {parallel}");
            var size = count;
            var initData = Condition.GenerateRandomOfficeData(size);
            var groups = initData.GroupBy(x => x.TenantId).ToArray();

            var gate = new object();
            ulong completed = 0;
            var tasks = new List<Task>();
            var ct = Context.CancellationToken;
            var sw = Stopwatch.StartNew();
            foreach (var group in groups)
            {
                var task = Task.Run(async () =>
                {
                    await using (var connection = await _connection.GetConnectionAsync(group.Key))
                    {
                        foreach (var data in group.Buffer(10000))
                        {
                            using (var transaction = await connection.BeginTransactionAsync(ct))
                            {
                                try
                                {
                                    var rows = await Condition.CopyAsync(connection, data, ct);
                                    await transaction.CommitAsync(ct);
                                    lock (gate)
                                    {
                                        completed += rows;
                                    }
                                    Console.WriteLine($"complete {completed}/{count}");
                                }
                                catch (Exception ex)
                                {
                                    Console.Error.WriteLine(ex);
                                    await transaction.RollbackAsync(ct);
                                }
                            }
                        }
                    }
                }, ct);
                tasks.Add(task);
            }
            await Task.WhenAll(tasks);
            Console.WriteLine($"Complete seed database. plan {count}, completed {completed}, duration {sw.Elapsed.TotalSeconds}sec");
        }
    }
}
