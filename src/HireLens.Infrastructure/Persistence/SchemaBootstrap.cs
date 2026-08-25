using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace HireLens.Infrastructure.Persistence;

/// <summary>
/// Ensures EF application tables exist. <see cref="DatabaseFacade.EnsureCreatedAsync"/> is unreliable
/// on SAP HANA Cloud when connecting as DBADMIN: the schema already has system tables, so EF skips
/// CreateTables and leaves app tables missing.
/// </summary>
public static class SchemaBootstrap
{
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

        if (await PositionsTableExistsAsync(db, cancellationToken))
        {
            logger.LogInformation("HireLens schema already present (Positions found).");
            return;
        }

        logger.LogWarning("Positions table missing — creating EF application tables.");
        var creator = db.Database.GetService<IRelationalDatabaseCreator>();
        await creator.CreateTablesAsync(cancellationToken);
        logger.LogInformation("HireLens schema CreateTables completed.");
    }

    private static async Task<bool> PositionsTableExistsAsync(
        HireLensDbContext db,
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
                  AND UPPER(TABLE_NAME) = 'POSITIONS'
                """;
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result) > 0;
        }
        catch (Exception)
        {
            // Non-HANA or restricted catalog: assume missing so CreateTables can run.
            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
