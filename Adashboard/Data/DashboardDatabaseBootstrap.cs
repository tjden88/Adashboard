using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace Adashboard.Data;

/// <summary>
/// Общая часть инициализации базы данных dashboard: совместимость со старой базой и применение миграций.
/// </summary>
internal static class DashboardDatabaseBootstrap
{
    /// <summary>
    /// Применяет миграции, предварительно обеспечивая совместимость с базой, созданной ранее через EnsureCreated.
    /// </summary>
    public static async Task ApplyMigrationsAsync(
        DashboardDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await EnsureMigrationHistoryForLegacyDatabaseAsync(dbContext, logger, cancellationToken);

        var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        logger.LogInformation("Ожидающих миграций: {PendingCount}.", pendingMigrations.Count);

        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Миграции базы данных успешно применены.");
    }

    /// <summary>
    /// Добавляет запись о первой миграции для базы, созданной ранее через EnsureCreated.
    /// </summary>
    private static async Task EnsureMigrationHistoryForLegacyDatabaseAsync(
        DashboardDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        try
        {
            var hasHistoryTable = await TableExistsAsync(connection, "__EFMigrationsHistory", cancellationToken);
            var hasLegacyTables = await TableExistsAsync(connection, "DashboardLayouts", cancellationToken);
            if (!hasLegacyTables)
            {
                logger.LogDebug("Старая структура базы данных не обнаружена. Совместимость не требуется.");
                return;
            }

            var firstMigration = dbContext.Database.GetMigrations().OrderBy(x => x).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstMigration))
            {
                logger.LogWarning("Не найдены миграции приложения при проверке совместимости базы данных.");
                return;
            }

            if (hasHistoryTable)
            {
                var hasInitialMigrationRow = await MigrationExistsAsync(connection, firstMigration, cancellationToken);
                if (hasInitialMigrationRow)
                {
                    logger.LogDebug("Запись о первой миграции уже существует в истории.");
                    return;
                }
            }
            else
            {
                await using var createHistoryCommand = connection.CreateCommand();
                createHistoryCommand.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                        "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                        "ProductVersion" TEXT NOT NULL
                    );
                    """;
                await createHistoryCommand.ExecuteNonQueryAsync(cancellationToken);
                logger.LogInformation("Создана таблица истории миграций для существующей базы данных.");
            }

            await using var insertHistoryCommand = connection.CreateCommand();
            insertHistoryCommand.CommandText =
                "INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ($migrationId, $productVersion);";
            insertHistoryCommand.Parameters.Add(CreateParameter(insertHistoryCommand, "$migrationId", firstMigration));
            insertHistoryCommand.Parameters.Add(CreateParameter(insertHistoryCommand, "$productVersion", "10.0.12"));
            await insertHistoryCommand.ExecuteNonQueryAsync(cancellationToken);
            logger.LogInformation("Добавлена запись о первой миграции в историю базы данных.");
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Проверяет существование таблицы в SQLite.
    /// </summary>
    private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        command.Parameters.Add(CreateParameter(command, "$tableName", tableName));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    /// <summary>
    /// Проверяет наличие записи о миграции в таблице истории.
    /// </summary>
    private static async Task<bool> MigrationExistsAsync(DbConnection connection, string migrationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = $migrationId;";
        command.Parameters.Add(CreateParameter(command, "$migrationId", migrationId));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    /// <summary>
    /// Создаёт параметр команды для SQL-запроса.
    /// </summary>
    private static DbParameter CreateParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        return parameter;
    }
}
