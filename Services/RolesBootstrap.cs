using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Api.Data;

namespace Api.Services
{
    public sealed class RolesBootstrap : IHostedService
    {
        private readonly Db _db;
        private readonly ILogger<RolesBootstrap> _log;

        public RolesBootstrap(Db db, ILogger<RolesBootstrap> log)
        {
            _db = db;
            _log = log;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var conn = _db.Open();

            await conn.ExecuteAsync(@"
IF OBJECT_ID('dbo.roles','U') IS NULL
BEGIN
  CREATE TABLE dbo.roles (
    id          INT IDENTITY(1,1) CONSTRAINT PK_roles PRIMARY KEY,
    nombre      NVARCHAR(40)  NOT NULL CONSTRAINT UQ_roles_nombre UNIQUE
  );
END");

            const string upsert = @"
IF NOT EXISTS (SELECT 1 FROM dbo.roles WHERE nombre = @nombre)
  INSERT INTO dbo.roles (nombre) VALUES (@nombre);";

            await conn.ExecuteAsync(upsert, new { nombre = "ADMINISTRADOR" });
            await conn.ExecuteAsync(upsert, new { nombre = "USUARIO" });

            _log.LogInformation("Roles listos: ADMINISTRADOR, USUARIO");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
