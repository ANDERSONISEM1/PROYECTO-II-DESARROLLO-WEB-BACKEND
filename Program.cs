using Api.Data;
using Api.Hubs;
using Api.Models;
using Api.Services;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
// 👇 Importante para ClaimTypes.Name y ClaimTypes.Role
using System.Security.Claims; // ← NUEVO: necesario para NameClaimType y RoleClaimType

var builder = WebApplication.CreateBuilder(args);

// Swagger & SignalR
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Controllers
builder.Services.AddControllers();

// Repos (tus existentes)
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtCfg.Key)),

            // 👇 NUEVO: aseguramos que se usen los claim correctos
            NameClaimType = ClaimTypes.Name,   // ← indica cuál claim se usa como nombre de usuario
            RoleClaimType = ClaimTypes.Role    // ← indica cuál claim se usa para los roles
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

// CORS (dev)
const string CorsDev = "cors-dev";
builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CorsDev, p =>
        p.WithOrigins("http://localhost:4200")
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials());
});

// Seed roles
builder.Services.AddHostedService<RolesBootstrap>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsDev);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok", env = app.Environment.EnvironmentName }))
   .RequireAuthorization();

app.MapHub<MarcadorHub>("/hub/marcador");

// Controllers
app.MapControllers();

// Puerto dev
app.Urls.Add("http://localhost:5080");

app.Run();
