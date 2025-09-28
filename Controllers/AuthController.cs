using Api.Data;
using Api.Models.Auth;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthRepo _repo;
    private readonly JwtTokenService _jwt;
    private readonly Db _db;

    private const int RefreshDays = 30;

    public AuthController(AuthRepo repo, JwtTokenService jwt, Db db)
    {
        _repo = repo;
        _jwt = jwt;
        _db = db;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrWhiteSpace(body.Password))
            return BadRequest("Credenciales inválidas.");

        var user = await _repo.GetUserByUsernameAsync(body.Username);
        if (user is null || !user.Activo) return Unauthorized("Usuario o contraseña inválidos.");

        var ok = PasswordHasher.VerifyArgon2id(body.Password, user.ContraseniaHash);
        if (!ok) return Unauthorized("Usuario o contraseña inválidos.");

        var roles = await _repo.GetRolesByUserIdAsync(user.Id);
        var (accessToken, accessExpires) = _jwt.CreateAccessToken(user.Id, user.Usuario, roles);

        await _repo.TouchLastLoginAsync(user.Id);

        // ===== Refresh token =====
        var refreshToken = GenerateSecureToken(64);
        var refreshExpires = DateTime.UtcNow.AddDays(RefreshDays);
        var tokenHash = Sha256(refreshToken);

        var ip = HttpContext.Connection?.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();

        using (var conn = _db.Open())
        using (var tx = conn.BeginTransaction())
        {
            const string insertSql = @"
INSERT INTO dbo.tokens_refresco (usuario_id, token_hash, expira_en, ip_origen, agente_usuario)
VALUES (@uid, @th, @exp, @ip, @ua);";
            await conn.ExecuteAsync(insertSql, new
            {
                uid = user.Id,
                th = tokenHash,
                exp = refreshExpires,
                ip,
                ua
            }, tx);

            tx.Commit();
        }

        // Cookie para el navegador (no influye en el insert)
        AppendRefreshCookie(refreshToken, refreshExpires);

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            ExpiresAtUtc = accessExpires,
            Username = user.Usuario,
            Roles = roles
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<LoginResponse>> Me()
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name)
                     ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(uidStr)) return Unauthorized();
        if (!long.TryParse(uidStr, out var userId)) return Unauthorized();

        var user = await _repo.GetUserByIdAsync(userId);
        if (user is null || !user.Activo) return Unauthorized();

        var roles = await _repo.GetRolesByUserIdAsync(user.Id);

        return Ok(new LoginResponse
        {
            AccessToken = "",
            ExpiresAtUtc = DateTime.UtcNow,
            Username = user.Usuario,
            Roles = roles
        });
    }

    // ========= Helpers =========
    private static string GenerateSecureToken(int bytesLen)
    {
        var bytes = RandomNumberGenerator.GetBytes(bytesLen);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static byte[] Sha256(string value)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(value));
    }

    private void AppendRefreshCookie(string refreshToken, DateTime expiresUtc)
    {
        var opts = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,               // en HTTP local puedes poner false, pero en prod debe ser true
            SameSite = SameSiteMode.None, // << clave si el front está en otro puerto/origen
            Expires = expiresUtc,
            IsEssential = true,
            Path = "/"
        };
        Response.Cookies.Append("refresh_token", refreshToken, opts);
    }
}
