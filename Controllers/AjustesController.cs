using Microsoft.AspNetCore.Mvc;
using Api.Data;
using Api.Models;
using Api.Services; // PasswordHasher
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/ajustes")]
    public sealed class AjustesController : ControllerBase
    {
        private readonly AjustesRepo _repo;
        public AjustesController(AjustesRepo repo) => _repo = repo;

        [HttpGet("roles")]
        public async Task<ActionResult<IEnumerable<RolDto>>> GetRoles()
            => Ok(await _repo.GetRolesAsync());

        [HttpGet("usuarios")]
        public async Task<ActionResult<IEnumerable<UsuarioDto>>> GetUsuarios()
            => Ok(await _repo.GetUsuariosAsync());

        [HttpPost("usuarios")]
        public async Task<ActionResult> CrearUsuario([FromBody] CrearUsuarioRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.Usuario) ||
                string.IsNullOrWhiteSpace(body.Password) ||
                string.IsNullOrWhiteSpace(body.PrimerNombre) ||
                string.IsNullOrWhiteSpace(body.PrimerApellido))
                return BadRequest("Campos obligatorios faltantes");

            var hash = PasswordHasher.CreateArgon2id(body.Password, out var algoritmo);
            var id = await _repo.CrearUsuarioAsync(body, hash, algoritmo);
            return Created($"/api/ajustes/usuarios/{id}", new { id });
        }

        [HttpPut("usuarios/{id:long}")]
        public async Task<ActionResult> EditarUsuario(long id, [FromBody] EditarUsuarioRequest body)
        {
            var n = await _repo.EditarUsuarioAsync(id, body);
            return n == 0 ? NotFound() : NoContent();
        }

        [HttpPatch("usuarios/{id:long}/activo")]
        public async Task<ActionResult> ToggleActivo(long id, [FromBody] ToggleActivoRequest body)
        {
            var n = await _repo.ToggleActivoAsync(id, body.Activo);
            return n == 0 ? NotFound() : NoContent();
        }

        [HttpDelete("usuarios/{id:long}")]
        public async Task<ActionResult> Eliminar(long id)
        {
            var n = await _repo.EliminarUsuarioAsync(id);
            return n == 0 ? NotFound() : NoContent();
        }

        [HttpPost("usuarios/reset-password")]
        public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest body)
        {
            if (body.UserId <= 0 || string.IsNullOrWhiteSpace(body.Password))
                return BadRequest("Datos inválidos");

            var hash = PasswordHasher.CreateArgon2id(body.Password, out var algoritmo);
            var n = await _repo.ResetPasswordAsync(body.UserId, hash, algoritmo);
            return n == 0 ? NotFound() : NoContent();
        }
    }
}
