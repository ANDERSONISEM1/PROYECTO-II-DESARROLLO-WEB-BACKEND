using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

using Api.Data;       // Db wrapper
using Api.Services;   // PasswordHasher

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class UsersController : ControllerBase
{
    private readonly Db _db;
    public UsersController(Db db) => _db = db;

    /// <summary>
    /// Dev-only: crea usuario con hash Argon2id y asigna roles. Protege o elimina en prod.
    /// </summary>
    [HttpPost("users")]
    public async Task<ActionResult> CreateUser([FromBody] CreateUserRequest req)
    {
        // Validaciones mínimas
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("username y password obligatorios");

        if (string.IsNullOrWhiteSpace(req.PrimerNombre) || string.IsNullOrWhiteSpace(req.PrimerApellido))
            return BadRequest("primer_nombre y primer_apellido son obligatorios");

        // Hash Argon2id [salt||hash] + descriptor del algoritmo
        var hash = PasswordHasher.CreateArgon2id(req.Password, out var algoritmo);

        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            // 1) Evitar duplicados
            var exists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.usuarios WHERE usuario = @u",
                new { u = req.Username }, tx);
            if (exists > 0)
            {
                tx.Rollback();
                return Conflict("Usuario ya existe");
            }

            // 2) Insert usuario (incluyendo nombres y apellidos) y obtener id
            const string insertUserSql = @"
INSERT INTO dbo.usuarios
  (usuario, primer_nombre, segundo_nombre, primer_apellido, segundo_apellido, correo, contrasenia_hash, algoritmo_hash, activo)
OUTPUT INSERTED.id
VALUES
  (@usuario, @primer_nombre, @segundo_nombre, @primer_apellido, @segundo_apellido, @correo, @hash, @algoritmo, 1);";

            var userId = await conn.ExecuteScalarAsync<long>(
                insertUserSql,
                new
                {
                    usuario         = req.Username,
                    primer_nombre   = req.PrimerNombre,
                    segundo_nombre  = req.SegundoNombre,   // puede ser null
                    primer_apellido = req.PrimerApellido,
                    segundo_apellido= req.SegundoApellido, // puede ser null
                    correo          = req.Correo,          // puede ser null
                    hash            = hash,
                    algoritmo       = algoritmo
                },
                tx);

            // 3) Crear/obtener roles y asignar
            if (req.Roles is { Length: > 0 })
            {
                foreach (var rol in req.Roles)
                {
                    var rolId = await conn.ExecuteScalarAsync<int?>(
                        "SELECT id FROM dbo.roles WHERE nombre = @rol",
                        new { rol }, tx);

                    if (rolId is null)
                    {
                       // ✅ Sin 'descripcion' ni valor extra
const string createRolSql =
    "INSERT INTO dbo.roles (nombre) OUTPUT INSERTED.id VALUES (@nombre)";
rolId = await conn.ExecuteScalarAsync<int>(
    createRolSql, new { nombre = rol }, tx);
                    }

                    var existsRel = await conn.ExecuteScalarAsync<int>(
                        "SELECT COUNT(1) FROM dbo.usuarios_roles WHERE usuario_id = @uid AND rol_id = @rid",
                        new { uid = userId, rid = rolId }, tx);

                    if (existsRel == 0)
                    {
                        await conn.ExecuteAsync(
                            "INSERT INTO dbo.usuarios_roles (usuario_id, rol_id) VALUES (@uid, @rid)",
                            new { uid = userId, rid = rolId }, tx);
                    }
                }
            }

            tx.Commit();
            return Created($"/api/auth/users/{userId}", new { id = userId });
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return StatusCode(500, ex.Message);
        }
    }
}

/// <summary>
/// DTO para creación de usuario (dev)
/// </summary>

public sealed class CreateUserRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("correo")]
    public string? Correo { get; set; }

    // Nuevos campos (obligatorios/opcionales) con nombres EXACTOS del JSON:
    [JsonPropertyName("primerNombre")]
    public string PrimerNombre { get; set; } = "";        // requerido

    [JsonPropertyName("segundoNombre")]
    public string? SegundoNombre { get; set; }            // opcional

    [JsonPropertyName("primerApellido")]
    public string PrimerApellido { get; set; } = "";      // requerido

    [JsonPropertyName("segundoApellido")]
    public string? SegundoApellido { get; set; }          // opcional

    [JsonPropertyName("roles")]
    public string[]? Roles { get; set; }                  // ej: ["ADMIN","VISOR"]
}
