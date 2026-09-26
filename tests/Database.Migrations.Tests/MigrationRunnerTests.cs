using System.Buffers.Binary;
using Dapper;
using Npgsql;
using Xunit;

namespace Database.Migrations.Tests;

public sealed class MigrationRunnerTests
{
    private const string AdminConnectionString =
        "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=local-demo";

    private const string Original = "Database.Migrations.Tests.Migrations.Original.";
    private const string Changed = "Database.Migrations.Tests.Migrations.Changed.";
    private const string Missing = "Database.Migrations.Tests.Migrations.Missing.";

    [Fact]
    public async Task AppliesScriptOnlyOnce()
    {
        var connectionString = await CreateDatabaseAsync();
        var assembly = typeof(MigrationRunnerTests).Assembly;

        var firstRun = await MigrationRunner.ApplyAsync(connectionString, assembly, Original);
        var secondRun = await MigrationRunner.ApplyAsync(connectionString, assembly, Original);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await connection.ExecuteAsync("""
                                      INSERT INTO "Probe" ("Id", "Label") VALUES (1, 'ready')
                                      """);
        var probeCount = await connection.ExecuteScalarAsync<long>("""
                                                                   SELECT count(*) FROM "Probe" WHERE "Label" = 'ready'
                                                                   """);
        var migrationCount = await connection.ExecuteScalarAsync<long>("""
                                                                       SELECT count(*) FROM "__SchemaMigrations"
                                                                       """);

        Assert.Equal(["001_create_probe.sql"], firstRun);
        Assert.Empty(secondRun);
        Assert.Equal(1, probeCount);
        Assert.Equal(1, migrationCount);
    }

    [Fact]
    public async Task RejectsChangedAppliedScript()
    {
        var connectionString = await CreateDatabaseAsync();
        var assembly = typeof(MigrationRunnerTests).Assembly;
        await MigrationRunner.ApplyAsync(connectionString, assembly, Original);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MigrationRunner.ApplyAsync(connectionString, assembly, Changed));

        Assert.Contains("has changed", exception.Message);
    }

    [Fact]
    public async Task RejectsMissingAppliedScript()
    {
        var connectionString = await CreateDatabaseAsync();
        var assembly = typeof(MigrationRunnerTests).Assembly;
        await MigrationRunner.ApplyAsync(connectionString, assembly, Original);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MigrationRunner.ApplyAsync(connectionString, assembly, Missing));

        Assert.Contains("is missing", exception.Message);
    }

    [Fact]
    public async Task ConcurrentStartsApplyPendingScriptOnce()
    {
        var connectionString = await CreateDatabaseAsync();
        var assembly = typeof(MigrationRunnerTests).Assembly;
        // NOTE(aalhendi): Hold the runner's lock until both starts are waiting, so this test exercises contention.
        var lockKey = BinaryPrimitives.ReadInt64BigEndian("MIGRATOR"u8);

        await using var gate = new NpgsqlConnection(connectionString);
        await gate.OpenAsync(TestContext.Current.CancellationToken);
        await using var gateTransaction = await gate.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await gate.ExecuteAsync("SELECT pg_advisory_xact_lock(@lockKey)", new { lockKey }, gateTransaction);

        var first = MigrationRunner.ApplyAsync(connectionString, assembly, Original);
        var second = MigrationRunner.ApplyAsync(connectionString, assembly, Original);
        await WaitForAdvisoryWaitersAsync(gate);
        await gateTransaction.CommitAsync(TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(first, second);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var migrationCount = await connection.ExecuteScalarAsync<long>("""
                                                                       SELECT count(*) FROM "__SchemaMigrations"
                                                                       """);

        Assert.Equal([0, 1], results.Select(result => result.Count).Order());
        Assert.Equal(1, migrationCount);
    }

    private static async Task WaitForAdvisoryWaitersAsync(NpgsqlConnection connection)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var waiters = await connection.ExecuteScalarAsync<long>("""
                                                                    SELECT count(*) FROM pg_locks
                                                                    WHERE locktype = 'advisory' AND NOT granted
                                                                      AND database = (SELECT oid FROM pg_database WHERE datname = current_database())
                                                                    """);
            if (waiters == 2) return;
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("Both migration starts did not wait for the advisory lock");
    }

    private static async Task<string> CreateDatabaseAsync()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")
                                    ?? AdminConnectionString;
        var databaseName = $"test_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        // NOTE(aalhendi): Identifiers cannot be SQL parameters; this name contains only generated GUID characters.
        await connection.ExecuteAsync($"""
                                       CREATE DATABASE "{databaseName}"
                                       """);

        return new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName }.ConnectionString;
    }
}