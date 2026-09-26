using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Npgsql;

namespace Database.Migrations;

public static class MigrationRunner
{
    // NOTE(aalhendi): Advisory locks are scoped to a database; pods sharing one serialize on this key.
    private static readonly long MigrationAdvisoryLockKey = BinaryPrimitives.ReadInt64BigEndian("MIGRATOR"u8);

    public static async Task<IReadOnlyList<string>> ApplyAsync(
        string connectionString, Assembly migrationsAssembly, string resourcePrefix)
    {
        var scripts = ReadScripts(migrationsAssembly, resourcePrefix);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await connection.ExecuteAsync("SELECT pg_advisory_xact_lock(@key)",
            new { key = MigrationAdvisoryLockKey }, transaction);

        await connection.ExecuteAsync("""
                                      CREATE TABLE IF NOT EXISTS "__SchemaMigrations" (
                                          "Version" bigint PRIMARY KEY,
                                          "Name" text NOT NULL,
                                          "Checksum" varchar(64) NOT NULL,
                                          "AppliedAt" timestamp with time zone NOT NULL DEFAULT now()
                                      )
                                      """, transaction: transaction);

        var applied = (await connection.QueryAsync<AppliedMigration>("""
                                                                     SELECT "Version", "Name", "Checksum"
                                                                     FROM "__SchemaMigrations"
                                                                     """, transaction: transaction))
            .ToDictionary(
                keySelector: row => row.Version,
                elementSelector: row => (row.Name, row.Checksum));

        foreach (var (version, stored) in applied)
        {
            var script = scripts.SingleOrDefault(candidate => candidate.Version == version)
                         ?? throw new InvalidOperationException(
                             $"Applied migration {version} is missing from the application");
            if (script.Name != stored.Name || script.Checksum != stored.Checksum)
                throw new InvalidOperationException($"Applied migration {script.Name} has changed");
        }

        var latestApplied = applied.Count == 0 ? 0 : applied.Keys.Max();
        var newlyApplied = new List<string>();
        foreach (var script in scripts.Where(candidate => !applied.ContainsKey(candidate.Version)))
        {
            if (script.Version <= latestApplied)
                throw new InvalidOperationException($"Migration {script.Name} was added before an applied migration");

            await connection.ExecuteAsync(script.Sql, transaction: transaction);
            await connection.ExecuteAsync("""
                                          INSERT INTO "__SchemaMigrations" ("Version", "Name", "Checksum")
                                          VALUES (@Version, @Name, @Checksum)
                                          """, new { script.Version, script.Name, script.Checksum }, transaction);

            newlyApplied.Add(script.Name);
        }

        await transaction.CommitAsync();
        return newlyApplied;
    }

    private static List<Script> ReadScripts(Assembly assembly, string resourcePrefix)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrEmpty(resourcePrefix);
        var scripts = new List<Script>();
        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal) &&
                                    name.EndsWith(".sql", StringComparison.Ordinal)))
        {
            var fileName = resource[resourcePrefix.Length..];
            var stem = Path.GetFileNameWithoutExtension(fileName);
            var parts = stem.Split('_', 2);

            if (parts.Length != 2 || parts[1].Length == 0)
                throw new InvalidOperationException($"Invalid SQL migration name: {fileName}");

            if (!long.TryParse(parts[0], out var version) || version <= 0)
                throw new InvalidOperationException($"Invalid SQL migration number: {fileName}");

            using var stream = assembly.GetManifestResourceStream(resource)
                               ?? throw new InvalidOperationException($"Missing SQL migration resource: {resource}");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            var sql = new UTF8Encoding(false, true).GetString(bytes);
            if (sql.StartsWith('\uFEFF'))
                throw new InvalidOperationException($"SQL migration {fileName} must be UTF-8 without a BOM");
            scripts.Add(new Script(version, fileName, sql, Convert.ToHexString(SHA256.HashData(bytes))));
        }

        if (scripts.Count == 0) throw new InvalidOperationException("No SQL migrations were embedded");
        if (scripts.Select(script => script.Version).Distinct().Count() != scripts.Count)
            throw new InvalidOperationException("Duplicate SQL migration version");
        return scripts.OrderBy(script => script.Version).ToList();
    }

    private sealed record Script(long Version, string Name, string Sql, string Checksum);

    private sealed record AppliedMigration(long Version, string Name, string Checksum);
}