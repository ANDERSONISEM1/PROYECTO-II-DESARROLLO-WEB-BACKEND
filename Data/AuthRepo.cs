using System.Data;
using Dapper;
using Api.Models.Auth;

namespace Api.Data;

public sealed class AuthRepo
{
    private readonly Db _db;
    public AuthRepo(Db db) => _db = db;

    public async Task<UserAuth?> GetUserByUsernameAsync(string username)
    {
        const string sql = @"
SELECT TOP 1
  id          AS Id,
  usuario     AS Usuario,
  correo      AS Correo,
  contrasenia_hash AS ContraseniaHash,
  algoritmo_hash   AS AlgoritmoHash,
  activo      AS Activo,
  ultimo_login AS UltimoLogin
FROM dbo.usuarios
WHERE usuario = @username;";
        using var conn = _db.Open();
        return await conn.QueryFirstOrDefaultAsync<UserAuth>(sql, new { username });
    }

    public async Task<UserAuth?> GetUserByIdAsync(long id)
    {
        const string sql = @"
SELECT TOP 1
  id          AS Id,
  usuario     AS Usuario,
  correo      AS Correo,
  contrasenia_hash AS ContraseniaHash,
  algoritmo_hash   AS AlgoritmoHash,
  activo      AS Activo,
  ultimo_login AS UltimoLogin
FROM dbo.usuarios
WHERE id = @id;";
        using var conn = _db.Open();
        return await conn.QueryFirstOrDefaultAsync<UserAuth>(sql, new { id });
    }

    public async Task<string[]> GetRolesByUserIdAsync(long userId)
    {
        const string sql = @"
SELECT r.nombre
FROM dbo.usuarios_roles ur
JOIN dbo.roles r ON r.id = ur.rol_id
WHERE ur.usuario_id = @userId;";
        using var conn = _db.Open();
        var rows = await conn.QueryAsync<string>(sql, new { userId });
        return rows.ToArray();
    }

    public async Task TouchLastLoginAsync(long userId, IDbTransaction? tx = null)
    {
        const string sql = @"UPDATE dbo.usuarios SET ultimo_login = SYSUTCDATETIME() WHERE id = @userId;";
        if (tx is not null) await tx.Connection!.ExecuteAsync(sql, new { userId }, tx);
        else { using var conn = _db.Open(); await conn.ExecuteAsync(sql, new { userId }); }
    }
}
