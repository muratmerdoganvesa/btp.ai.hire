using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace HireLens.Infrastructure.Persistence;

/// <summary>
/// Ensures EF application tables exist. <see cref="DatabaseFacade.EnsureCreatedAsync"/> is unreliable
/// on SAP HANA Cloud when connecting as DBADMIN: the schema already has system tables, so EF skips
/// CreateTables. A partial CreateTables run can also leave Positions without PositionCriteria.
/// </summary>
public static class SchemaBootstrap
{
    // Uppercase HANA catalog names for the core recruiting path (demo-critical).
    private static readonly string[] RequiredTables =
    [
        "POSITIONS",
        "POSITIONCRITERIA"
    ];

    public static async Task EnsureApplicationTablesAsync(
        HireLensDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.IsInMemory())
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
            logger.LogInformation("InMemory schema ensured.");
            return;
        }

        var missing = new List<string>();
        foreach (var table in RequiredTables)
        {
            if (!await TableExistsAsync(db, table, cancellationToken))
            {
                missing.Add(table);
            }
        }

        if (missing.Count == 0)
        {
            logger.LogInformation("HireLens schema already present ({Tables}).", string.Join(", ", RequiredTables));
            return;
        }

        logger.LogWarning("Missing HANA tables: {Missing}. Rebuilding application schema.", string.Join(", ", missing));

        // Partial schema: drop known app tables then create the full model set.
        foreach (var table in RequiredTables.Reverse())
        {
            await DropTableIfExistsAsync(db, table, logger, cancellationToken);
        }

        try
        {
            var creator = db.Database.GetService<IRelationalDatabaseCreator>();
            await creator.CreateTablesAsync(cancellationToken);
            logger.LogInformation("HireLens schema CreateTables completed.");
        }
        catch (Exception ex)
        {
            // CreateTables is all-or-nothing in theory; on HANA a prior partial run can leave
            // "table already exists" mid-script. Apply generate-script statement-by-statement.
            logger.LogWarning(ex, "CreateTables failed; applying GenerateCreateScript statements.");
            await ApplyCreateScriptIgnoringExistsAsync(db, logger, cancellationToken);
        }

        foreach (var table in RequiredTables)
        {
            if (!await TableExistsAsync(db, table, cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Schema bootstrap failed: table '{table}' still missing in CURRENT_SCHEMA after CreateTables.");
            }
        }
    }

    private static async Task ApplyCreateScriptIgnoringExistsAsync(
        HireLensDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var script = db.Database.GenerateCreateScript();
        foreach (var statement in SplitSqlStatements(script))
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(statement, cancellationToken);
            }
            catch (Exception ex) when (LooksLikeAlreadyExists(ex))
            {
                logger.LogInformation("Skip existing object: {Message}", Truncate(ex.Message, 200));
            }
        }
    }

    private static IEnumerable<string> SplitSqlStatements(string script)
    {
        foreach (var part in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Length == 0 || part.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            yield return part;
        }
    }

    private static bool LooksLikeAlreadyExists(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        return message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
               || message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
               || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private static async Task<bool> TableExistsAsync(
        HireLensDbContext db,
        string upperTableName,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT COUNT(*)
                FROM TABLES
                WHERE SCHEMA_NAME = CURRENT_SCHEMA
                  AND UPPER(TABLE_NAME) = :name
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "name";
            parameter.Value = upperTableName;
            command.Parameters.Add(parameter);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result) > 0;
        }
        catch (Exception)
        {
            // Fallback without bind parameter (some HANA ADO builds are picky).
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"""
                    SELECT COUNT(*)
                    FROM TABLES
                    WHERE SCHEMA_NAME = CURRENT_SCHEMA
                      AND UPPER(TABLE_NAME) = '{upperTableName.Replace("'", "''", StringComparison.Ordinal)}'
                    """;
                var result = await command.ExecuteScalarAsync(cancellationToken);
                return Convert.ToInt64(result) > 0;
            }
            catch
            {
                return false;
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task DropTableIfExistsAsync(
        HireLensDbContext db,
        string upperTableName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(db, upperTableName, cancellationToken))
        {
            return;
        }

        // Prefer quoted PascalCase names used by EF; also try uppercase / with CASCADE.
        foreach (var name in new[] { ToPascal(upperTableName), upperTableName })
        {
            foreach (var sql in new[]
                     {
                         $"""DROP TABLE "{name}" CASCADE""",
                         $"""DROP TABLE "{name}" """
                     })
            {
                try
                {
                    await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                    logger.LogInformation("Dropped leftover table {Table}.", name);
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "DROP TABLE {Table} skipped ({Sql}).", name, sql);
                }
            }
        }
    }

    private static string ToPascal(string upper) =>
        upper switch
        {
            "POSITIONS" => "Positions",
            "POSITIONCRITERIA" => "PositionCriteria",
            _ => upper
        };
}
