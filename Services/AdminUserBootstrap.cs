using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Api.Data;
using Api.Services;

namespace Api.Services
{
    /// <summary>
    /// Crea un usuario administrador por defecto en el arranque si no existe.
    /// Idempotente: no duplica ni re-escribe si ya existe.
    /// </summary>
    public sealed class AdminUserBootstrap : IHostedService
    {
        private readonly Db _db;
        private readonly ILogger<AdminUserBootstrap> _log;

        // === Datos solicitados ===
        private const string Username = "10021";
        private const string PasswordPlano = "Octubre2025..";
        private const string PrimerNombre = "Anderson";
        private const string SegundoNombre = "Abimael";
        private const string PrimerApellido = "Isem";
        private const string SegundoApellido = "Cac";
        private const string RolAdmin = "ADMINISTRADOR";

        public AdminUserBootstrap(Db db, ILogger<AdminUserBootstrap> log)
        {
            _db = db;
            _log = log;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var conn = _db.Open();

            // Asegurar rol ADMINISTRADOR (por si el otro bootstrap aún no lo creó)
            const string upsertRol = @"
IF NOT EXISTS (SELECT 1 FROM dbo.roles WHERE nombre = @nombre)
  INSERT INTO dbo.roles (nombre) VALUES (@nombre);";
            await conn.ExecuteAsync(upsertRol, new { nombre = RolAdmin });

            // ¿Existe ya el usuario?
            var exists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.usuarios WHERE usuario = @u",
                new { u = Username });

            long userId;

            if (exists == 0)
            {
                // Hash Argon2id siguiendo tu convención [salt||hash]
                var hash = PasswordHasher.CreateArgon2id(PasswordPlano, out var algoritmo);

                const string insertUserSql = @"
INSERT INTO dbo.usuarios
  (usuario, primer_nombre, segundo_nombre, primer_apellido, segundo_apellido, correo,
   contrasenia_hash, algoritmo_hash, activo)
OUTPUT INSERTED.id
VALUES
  (@usuario, @primer_nombre, @segundo_nombre, @primer_apellido, @segundo_apellido, @correo,
   @hash, @algoritmo, 1);";

                userId = await conn.ExecuteScalarAsync<long>(
                    insertUserSql,
                    new
                    {
                        usuario = Username,
                        primer_nombre = PrimerNombre,
                        segundo_nombre = (object?)SegundoNombre ?? DBNull.Value,
                        primer_apellido = PrimerApellido,
                        segundo_apellido = (object?)SegundoApellido ?? DBNull.Value,
                        correo = (string?)null,
                        hash,
                        algoritmo
                    });

                _log.LogInformation("Usuario admin por defecto creado: {Username} (id {UserId})", Username, userId);
            }
            else
            {
                userId = await conn.ExecuteScalarAsync<long>(
                    "SELECT id FROM dbo.usuarios WHERE usuario = @u",
                    new { u = Username });
                _log.LogInformation("Usuario admin por defecto ya existía: {Username} (id {UserId})", Username, userId);
            }

            // Obtener id del rol ADMINISTRADOR
            var rolId = await conn.ExecuteScalarAsync<int>(
                "SELECT id FROM dbo.roles WHERE nombre = @nombre",
                new { nombre = RolAdmin });

            // Vincular usuario-rol si no existe
            var rel = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.usuarios_roles WHERE usuario_id = @uid AND rol_id = @rid",
                new { uid = userId, rid = rolId });

            if (rel == 0)
            {
                await conn.ExecuteAsync(
                    "INSERT INTO dbo.usuarios_roles (usuario_id, rol_id) VALUES (@uid, @rid)",
                    new { uid = userId, rid = rolId });

                _log.LogInformation("Asignado rol {Rol} al usuario {Username}.", RolAdmin, Username);
            }
            else
            {
                _log.LogInformation("El usuario {Username} ya tenía el rol {Rol}.", Username, RolAdmin);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
