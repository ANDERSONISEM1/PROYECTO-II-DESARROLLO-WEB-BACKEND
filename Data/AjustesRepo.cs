using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Api.Models;

namespace Api.Data
{
    public sealed class AjustesRepo
    {
        private readonly Db _db;
        public AjustesRepo(Db db) => _db = db;

        public async Task<IEnumerable<RolDto>> GetRolesAsync()
        {
            using var conn = _db.Open();
            const string sql = @"SELECT id AS Id, nombre AS Nombre
                                 FROM dbo.roles ORDER BY nombre";
            return await conn.QueryAsync<RolDto>(sql);
        }

        public async Task<IEnumerable<UsuarioDto>> GetUsuariosAsync()
        {
            using var conn = _db.Open();
            const string sql = @"
SELECT  u.id               AS Id,
        u.usuario         AS Usuario,
        u.primer_nombre   AS PrimerNombre,
        u.segundo_nombre  AS SegundoNombre,
        u.primer_apellido AS PrimerApellido,
        u.segundo_apellido AS SegundoApellido,
        u.correo          AS Correo,
        u.activo          AS Activo,
        ur.rol_id         AS RolId,
        r.nombre          AS RolNombre
FROM dbo.usuarios u
LEFT JOIN dbo.usuarios_roles ur ON ur.usuario_id = u.id
LEFT JOIN dbo.roles r ON r.id = ur.rol_id
ORDER BY u.usuario;";
            return await conn.QueryAsync<UsuarioDto>(sql);
        }

        public async Task<long> CrearUsuarioAsync(CrearUsuarioRequest req, byte[] hash, string algoritmo)
        {
            using var conn = _db.Open();
            using var tx = conn.BeginTransaction();

            // usuario único
            var dup = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.usuarios WHERE usuario=@u",
                new { u = req.Usuario }, tx);
            if (dup > 0) { tx.Rollback(); throw new System.InvalidOperationException("Usuario ya existe"); }

            const string ins = @"
INSERT INTO dbo.usuarios
 (usuario, primer_nombre, segundo_nombre, primer_apellido, segundo_apellido, correo, contrasenia_hash, algoritmo_hash, activo)
OUTPUT INSERTED.id
VALUES
 (@usuario,@pn,@sn,@pa,@sa,@correo,@hash,@alg,1);";
            var id = await conn.ExecuteScalarAsync<long>(ins, new {
                usuario = req.Usuario, pn = req.PrimerNombre, sn = req.SegundoNombre,
                pa = req.PrimerApellido, sa = req.SegundoApellido, correo = req.Correo,
                hash = hash, alg = algoritmo
            }, tx);

            // asignar rol
            await conn.ExecuteAsync(
                "DELETE FROM dbo.usuarios_roles WHERE usuario_id=@id",
                new { id }, tx);
            await conn.ExecuteAsync(
                "INSERT INTO dbo.usuarios_roles (usuario_id, rol_id) VALUES (@id,@rid)",
                new { id, rid = req.RolId }, tx);

            tx.Commit();
            return id;
        }

        public async Task<int> EditarUsuarioAsync(long id, EditarUsuarioRequest req)
        {
            using var conn = _db.Open();
            using var tx = conn.BeginTransaction();

            const string upd = @"
UPDATE dbo.usuarios SET
  usuario=@usuario, primer_nombre=@pn, segundo_nombre=@sn,
  primer_apellido=@pa, segundo_apellido=@sa, correo=@correo
WHERE id=@id;";
            var n1 = await conn.ExecuteAsync(upd, new {
                id, usuario=req.Usuario, pn=req.PrimerNombre, sn=req.SegundoNombre,
                pa=req.PrimerApellido, sa=req.SegundoApellido, correo=req.Correo
            }, tx);

            await conn.ExecuteAsync("DELETE FROM dbo.usuarios_roles WHERE usuario_id=@id", new { id }, tx);
            await conn.ExecuteAsync("INSERT INTO dbo.usuarios_roles (usuario_id, rol_id) VALUES (@id,@rid)",
                new { id, rid=req.RolId }, tx);

            tx.Commit();
            return n1;
        }

        public async Task<int> ToggleActivoAsync(long id, bool activo)
        {
            using var conn = _db.Open();
            return await conn.ExecuteAsync(
                "UPDATE dbo.usuarios SET activo=@a WHERE id=@id",
                new { id, a = activo });
        }

        public async Task<int> EliminarUsuarioAsync(long id)
        {
            using var conn = _db.Open();
            using var tx = conn.BeginTransaction();

            await conn.ExecuteAsync("DELETE FROM dbo.usuarios_roles WHERE usuario_id=@id", new { id }, tx);
            var n = await conn.ExecuteAsync("DELETE FROM dbo.usuarios WHERE id=@id", new { id }, tx);

            tx.Commit();
            return n;
        }

        public async Task<int> ResetPasswordAsync(long id, byte[] hash, string algoritmo)
        {
            using var conn = _db.Open();
            // (opcional) rotar sesiones: revocar tokens
            await conn.ExecuteAsync(
                "UPDATE dbo.tokens_refresco SET revocado_en=SYSUTCDATETIME() WHERE usuario_id=@id AND revocado_en IS NULL",
                new { id });

            return await conn.ExecuteAsync(
                "UPDATE dbo.usuarios SET contrasenia_hash=@h, algoritmo_hash=@a WHERE id=@id",
                new { id, h = hash, a = algoritmo });
        }
    }
}
