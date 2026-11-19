using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace BorrowingSystem.Api.Data;

/// <summary>
/// Compatibility shim for XAMPP's MariaDB 10.4. The Oracle MySQL provider takes the migration lock with
/// GET_LOCK(name, -1) ("wait forever"); MariaDB before 10.5 answers that with NULL and MigrateAsync crashes.
/// This decorator keeps the provider's own history repository for everything else and only swaps the lock
/// for one with a finite timeout. Delete this file (and its line in Program.cs) once the server runs
/// MySQL 8 or MariaDB 10.5+.
/// </summary>
internal sealed class MariaDbHistoryRepository : IHistoryRepository
{
    private const string LockName = "__EFMigrationsLock";
    private const int TimeoutSeconds = 600;

    private readonly IHistoryRepository _inner;
    private readonly HistoryRepositoryDependencies _dependencies;

    public MariaDbHistoryRepository(HistoryRepositoryDependencies dependencies)
    {
        _dependencies = dependencies;
        // The provider's implementation is internal, so it is created by name.
        var providerType = Type.GetType(
            "MySql.EntityFrameworkCore.Migrations.Internal.MySQLHistoryRepository, MySql.EntityFrameworkCore",
            throwOnError: true)!;
        _inner = (IHistoryRepository)Activator.CreateInstance(providerType, dependencies)!;
    }

    public LockReleaseBehavior LockReleaseBehavior => LockReleaseBehavior.Connection;

    public IMigrationsDatabaseLock AcquireDatabaseLock()
    {
        EnsureAcquired(Command($"SELECT GET_LOCK('{LockName}', {TimeoutSeconds})").ExecuteScalar(Parameters()));
        return new DatabaseLock(this);
    }

    public async Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(CancellationToken cancellationToken = default)
    {
        EnsureAcquired(await Command($"SELECT GET_LOCK('{LockName}', {TimeoutSeconds})")
            .ExecuteScalarAsync(Parameters(), cancellationToken));
        return new DatabaseLock(this);
    }

    // ---- everything below is delegated unchanged ----
    public bool Exists() => _inner.Exists();
    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default) => _inner.ExistsAsync(cancellationToken);
    public void Create() => _inner.Create();
    public Task CreateAsync(CancellationToken cancellationToken = default) => _inner.CreateAsync(cancellationToken);
    public bool CreateIfNotExists() => _inner.CreateIfNotExists();
    public Task<bool> CreateIfNotExistsAsync(CancellationToken cancellationToken = default) =>
        _inner.CreateIfNotExistsAsync(cancellationToken);
    public IReadOnlyList<HistoryRow> GetAppliedMigrations() => _inner.GetAppliedMigrations();
    public Task<IReadOnlyList<HistoryRow>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default) =>
        _inner.GetAppliedMigrationsAsync(cancellationToken);
    public string GetCreateScript() => _inner.GetCreateScript();
    public string GetCreateIfNotExistsScript() => _inner.GetCreateIfNotExistsScript();
    public string GetInsertScript(HistoryRow row) => _inner.GetInsertScript(row);
    public string GetDeleteScript(string migrationId) => _inner.GetDeleteScript(migrationId);
    public string GetBeginIfNotExistsScript(string migrationId) => _inner.GetBeginIfNotExistsScript(migrationId);
    public string GetBeginIfExistsScript(string migrationId) => _inner.GetBeginIfExistsScript(migrationId);
    public string GetEndIfScript() => _inner.GetEndIfScript();

    private IRelationalCommand Command(string sql) => _dependencies.RawSqlCommandBuilder.Build(sql);

    private RelationalCommandParameterObject Parameters() => new(
        _dependencies.Connection, null, null, _dependencies.CurrentContext.Context, _dependencies.CommandLogger,
        CommandSource.Migrations);

    private static void EnsureAcquired(object? result)
    {
        if (result is null or DBNull || Convert.ToInt64(result) != 1)
            throw new TimeoutException("Could not acquire the database migration lock.");
    }

    private sealed class DatabaseLock(MariaDbHistoryRepository owner) : IMigrationsDatabaseLock
    {
        private const string ReleaseSql = $"SELECT RELEASE_LOCK('{LockName}')";

        public IHistoryRepository HistoryRepository => owner;

        public void Dispose() => owner.Command(ReleaseSql).ExecuteScalar(owner.Parameters());

        public async ValueTask DisposeAsync() =>
            await owner.Command(ReleaseSql).ExecuteScalarAsync(owner.Parameters());
    }
}
