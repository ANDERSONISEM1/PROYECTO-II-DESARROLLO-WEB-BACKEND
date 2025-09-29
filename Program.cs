using Api.Data;
using Api.Hubs;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Swagger & SignalR
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Controllers
builder.Services.AddControllers();

// Repos existentes
builder.Services.AddScoped<MarcadorRepo>();
builder.Services.AddScoped<JugadoresRepo>();
builder.Services.AddScoped<FaltasRepo>();
builder.Services.AddScoped<TiemposMuertosRepo>();
builder.Services.AddScoped<PartidosRepo>();
builder.Services.AddScoped<CronometroRepo>();
builder.Services.AddScoped<CuartosRepo>();
builder.Services.AddScoped<EquiposRepo>();
builder.Services.AddScoped<JugadorRepo>();
builder.Services.AddScoped<PartidosCrudRepo>();
builder.Services.AddScoped<HistorialRepo>();
builder.Services.AddScoped<InicioRepo>();
builder.Services.AddScoped<AjustesRepo>();

// DB wrapper
builder.Services.AddSingleton<Db>();

// === Auth / JWT ===
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtCfg = jwtSection.Get<JwtSettings>()!;
builder.Services.AddSingleton(jwtCfg);
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<AuthRepo>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtCfg.Issuer,
            ValidAudience = jwtCfg.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtCfg.Key)),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero
        };

        // Soporte para SignalR con token en querystring
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ===== CORS DEV (abre todo: útil ahora; endurecer en prod) =====
const string CorsDev = "cors-dev";
builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CorsDev, p =>
        p.SetIsOriginAllowed(_ => true)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials());
});

// Seeds al arrancar
builder.Services.AddHostedService<RolesBootstrap>();
builder.Services.AddHostedService<AdminUserBootstrap>();

// Kestrel: escuchar en todas las IPs (no localhost)
builder.WebHost.UseKestrel();
builder.WebHost.ConfigureKestrel(o => { o.ListenAnyIP(5080); });

var app = builder.Build();

// Swagger (ok en prod para ti ahora)
app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();

// CORS ANTES de Auth
app.UseCors(CorsDev);

app.UseAuthentication();
app.UseAuthorization();

// Healthcheck público
app.MapGet("/healthz", () => Results.Ok(new
{
    status = "ok",
    env = app.Environment.EnvironmentName,
    timestamp = DateTime.UtcNow
}));

app.MapHub<MarcadorHub>("/hub/marcador");

app.MapControllers();

// No fuerces localhost aquí (rompe en contenedor):
// app.Urls.Add("http://localhost:5080");

app.Run();
