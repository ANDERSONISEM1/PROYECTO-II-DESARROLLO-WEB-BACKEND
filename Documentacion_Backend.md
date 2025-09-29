# 📑 Documentación del Backend – Proyecto II Desarrollo Web

**Integrantes**

- Anderson Abimael Isem Cac - 7690-22-9604
- Jose Leonel Salazar Tejeda - 7690-22-8974
- Marvin Geobany Reyna Ortega - 7690-22-8291  

---

## 📋 Índice

- [📑 Documentación del Backend – Proyecto II Desarrollo Web](#-documentación-del-backend--proyecto-ii-desarrollo-web)
  - [📋 Índice](#-índice)
  - [📖 Introducción](#-introducción)
  - [🛠 Tecnologías y Stack](#-tecnologías-y-stack)
  - [🏗 Arquitectura y Estructura del Proyecto](#-arquitectura-y-estructura-del-proyecto)
  - [🗃 Modelos / Entidades de Datos](#-modelos--entidades-de-datos)
    - [👥 Usuario y Roles (AjustesDtos.cs)](#-usuario-y-roles-ajustesdtoscs)
    - [⏱ Cronometro (CronometroDtos.cs)](#-cronometro-cronometrodtoscs)
    - [🕓 Cuartos (CuartosDtos.cs)](#-cuartos-cuartosdtoscs)
    - [🏀 Equipo (EquipoDtos.cs)](#-equipo-equipodtoscs)
    - [🚨 Faltas (FaltasDtos.cs)](#-faltas-faltasdtoscs)
    - [📜 Historial (HistorialDtos.cs)](#-historial-historialdtoscs)
    - [🏠 Inicio (InicioDtos.cs)](#-inicio-iniciodtoscs)
    - [👥 Jugadores (jugadoresModels.cs)](#-jugadores-jugadoresmodelscs)
    - [🔑 JWT Configuración (JwtSettings.cs)](#-jwt-configuración-jwtsettingscs)
    - [🔐 Autenticación](#-autenticación)
    - [📊 Marcador (MarcadorDtos.cs)](#-marcador-marcadordtoscs)
    - [📅 Partidos](#-partidos)
    - [⏸ Tiempos Muertos (TiemposMuertosDtos.cs)](#-tiempos-muertos-tiemposmuertosdtoscs)
  - [⚙ Servicios / Lógica de Negocio](#-servicios--lógica-de-negocio)
  - [🌐 Controladores / Endpoints de la API](#-controladores--endpoints-de-la-api)
    - [🔑 AuthController](#-authcontroller)
    - [👤 UsersController](#-userscontroller)
    - [🏀 Equipos](#-equipos)
    - [👥 Jugadores](#-jugadores)
    - [📅 Partidos](#-partidos-1)
    - [⏱ CronometroController](#-cronometrocontroller)
    - [⏸ TIemposMuertosController](#-tiemposmuertoscontroller)
    - [🚨 FaltasController](#-faltascontroller)
    - [🕓 CuatrosController](#-cuatroscontroller)
    - [📊 MarcadorControlller](#-marcadorcontrolller)
    - [📜 HistorialController](#-historialcontroller)
    - [⚙ AjustesController](#-ajustescontroller)
    - [🏠 InicioController](#-iniciocontroller)
    - [📡 PingController](#-pingcontroller)
  - [🔴 Comunicación en Tiempo Real (Hubs)](#-comunicación-en-tiempo-real-hubs)
  - [🔐 Autenticación y Seguridad](#-autenticación-y-seguridad)
  - [⚙ Configuración y Variables de Entorno](#-configuración-y-variables-de-entorno)
  - [💻 Instalación y Ejecución](#-instalación-y-ejecución)
    - [Requisitos](#requisitos)
    - [Pasos](#pasos)
  - [🧪 Pruebas y Calidad](#-pruebas-y-calidad)
  - [🚀 Mejoras Futuras / Roadmap](#-mejoras-futuras--roadmap)
  - [👨‍💻 Créditos y Licencia](#-créditos-y-licencia)

---

## 📖 Introducción

El presente backend corresponde al **Proyecto II de Desarrollo Web**.  
Su propósito es exponer una **API RESTful** para gestionar los datos y operaciones del sistema, incluyendo autenticación de usuarios, operaciones CRUD sobre entidades, y funcionalidades en tiempo real mediante **SignalR**.

Este backend funciona como **capa de negocio y persistencia**, consumido por el frontend desarrollado en paralelo.

---

## 🛠 Tecnologías y Stack

- **Lenguaje / Plataforma**: C# con .NET 8.0  
- **Framework**: ASP.NET Core Web API  
- **Base de datos**: SQL Server 2022  
- **ORM**: Entity Framework Core  
- **Comunicación en tiempo real**: SignalR  
- **Autenticación**: JWT (JSON Web Tokens)  
- **Contenedores / despliegue**: Docker y Docker Compose  
- **Otras librerías utilizadas**:  
  - Microsoft.AspNetCore.Identity  
  - Microsoft.EntityFrameworkCore.SqlServer  
  - Microsoft.AspNetCore.Authentication.JwtBearer  

---

## 🏗 Arquitectura y Estructura del Proyecto

Estructura principal del código fuente:

```
/Controllers        -> Controladores de la API
/Data               -> Contexto de la base de datos y configuración EF Core
/Models             -> Entidades y DTOs
/Services           -> Lógica de negocio y servicios auxiliares
/Hubs               -> Comunicación en tiempo real (SignalR)
/Properties         -> Configuración del proyecto
Program.cs          -> Punto de entrada de la aplicación
appsettings.json    -> Configuración general (DB, JWT, etc.)
```

**Flujo de datos:**

```
Cliente (Frontend) → Controladores (API) → Servicios → Data/EF Core → Base de Datos
                                 ↑
                                 └── SignalR (Hubs en tiempo real)
```

---

## 🗃 Modelos / Entidades de Datos

### 👥 Usuario y Roles (AjustesDtos.cs)

**RolDto**

| Propiedad | Tipo | Descripción |
|-----------|------|-------------|
| Id | int | Identificador del rol |
| Nombre | string | Nombre del rol |

**UsuarioDto**

| Propiedad | Tipo | Descripción |
|-----------|------|-------------|
| Id | long | Identificador del usuario |
| Usuario | string | Nombre de usuario |
| PrimerNombre | string | Nombre principal |
| SegundoNombre | string | Segundo nombre (opcional) |
| PrimerApellido | string | Apellido principal |
| SegundoApellido | string | Apellido secundario (opcional) |
| Correo | string | Email del usuario |
| Activo | bool | Estado del usuario |
| RolId | int | Rol asignado |
| RolNombre | string | Nombre del rol asociado |

**CrearUsuarioRequest / EditarUsuarioRequest**
Campos para crear/editar usuario:

- Usuario  
- Password (solo en creación)  
- PrimerNombre, SegundoNombre  
- PrimerApellido, SegundoApellido  
- Correo  
- RolId  

**ResetPasswordRequest**

- UserId (long)  
- Password (string)  
- RotarSesion (bool)  

**ToggleActivoRequest**

- Activo (bool)  

### ⏱ Cronometro (CronometroDtos.cs)

**CronometroEventRequest**

| Propiedad | Tipo | Descripción |
|-----------|------|-------------|
| Tipo | string | Tipo de evento (inicio, pausa, reanudar, fin, prórroga, descanso, medio, reiniciar) |
| SegundosRestantes | int | Tiempo restante al registrar evento |
| NumeroCuarto | int | Número de cuarto si aplica |
| EsProrroga | bool | True si es prórroga |
| CuartoId | int | Referencia al cuarto |

**CronometroEventResponse**

- EventoId (long)  
- PartidoId (int)  
- CuartoId (int)  
- Tipo (string)  
- SegundosRestantes (int?)  
- CreadoEn (DateTime)  

### 🕓 Cuartos (CuartosDtos.cs)

**PeriodStateDto**

- Numero (int)  
- Total (int)  
- EsProrroga (bool)  
- Rotulo (string?, ej: "Descanso", "Medio tiempo")  

**SetCuartoRequest**

- Numero (int)  

**RotuloRequest**

- Rotulo (string, ej: "Descanso", "Medio tiempo")  

### 🏀 Equipo (EquipoDtos.cs)

- Id (int)  
- Nombre (string)  
- Ciudad (string)  

### 🚨 Faltas (FaltasDtos.cs)

- Id (int)  
- JugadorId (int)  
- PartidoId (int)  
- Cantidad (int)  

### 📜 Historial (HistorialDtos.cs)

- Id (int)  
- PartidoId (int)  
- Fecha (DateTime)  
- Resultado (string)  

### 🏠 Inicio (InicioDtos.cs)

- MensajeBienvenida (string)  
- VersionSistema (string)  

### 👥 Jugadores (jugadoresModels.cs)

- Id (int)  
- Nombre (string)  
- Edad (int)  
- EquipoId (int)  

**JugadorMini.cs**

- Id (int)  
- Nombre (string)  

### 🔑 JWT Configuración (JwtSettings.cs)

- Key (string)  
- Issuer (string)  
- Audience (string)  
- ExpiresInMinutes (int)  

### 🔐 Autenticación

**LoginRequest.cs**

- Email (string)  
- Password (string)  

**LoginResponse.cs**

- Token (string)  
- UsuarioId (int)  
- Nombre (string)  
- Rol (string)  

**UserAuth.cs**

- Id (long)  
- NombreUsuario (string)  
- Email (string)  
- PasswordHash (string)  
- Rol (string)  

### 📊 Marcador (MarcadorDtos.cs)

- PartidoId (int)  
- MarcadorLocal (int)  
- MarcadorVisitante (int)  
- Periodo (int)  

### 📅 Partidos

**PartidosCrudDtos.cs**

- Id (int)  
- EquipoLocalId (int)  
- EquipoVisitanteId (int)  
- Fecha (DateTime)  

**PartidosDtos.cs**

- Id (int)  
- EquipoLocal (string)  
- EquipoVisitante (string)  
- MarcadorLocal (int)  
- MarcadorVisitante (int)  
- Estado (string)  

### ⏸ Tiempos Muertos (TiemposMuertosDtos.cs)

- Id (int)  
- PartidoId (int)  
- EquipoId (int)  
- Cantidad (int)  

---

## ⚙ Servicios / Lógica de Negocio

Los **services** encapsulan la lógica de negocio. Algunos ejemplos:

- **AuthService**:  
  - Registro de usuarios  
  - Inicio de sesión y generación de tokens JWT  
- **EquipoService**: CRUD de equipos  
- **JugadorService**: CRUD de jugadores  
- **PartidoService**: programación y actualización de resultados  
- **TorneoService**: gestión de torneos  

---

## 🌐 Controladores / Endpoints de la API

El backend expone múltiples controladores organizados según la lógica del sistema.  
A continuación, un resumen de cada uno:

### 🔑 AuthController

**Propósito:** Manejo de autenticación de usuarios.  

- `POST /api/auth/login` → Iniciar sesión y obtener JWT.  
- `POST /api/auth/register` → Registrar un nuevo usuario.  
- `POST /api/auth/refresh` → Renovar token de sesión.  

### 👤 UsersController

**Propósito:** Administración de usuarios.  

- `GET /api/users` → Lista todos los usuarios.  
- `GET /api/users/{id}` → Obtiene detalles de un usuario.  
- `PUT /api/users/{id}` → Actualiza datos de un usuario.  
- `DELETE /api/users/{id}` → Elimina un usuario.  

### 🏀 Equipos

**Controlador:** `AdminEquiposController`  
**Propósito:** Administración de equipos de baloncesto.  

- `GET /api/admin/equipos` → Lista de equipos.  
- `POST /api/admin/equipos` → Crear un equipo.  
- `PUT /api/admin/equipos/{id}` → Editar equipo.  
- `DELETE /api/admin/equipos/{id}` → Eliminar equipo.  

### 👥 Jugadores

**Controladores:** `JugadoresController`, `AdminJugadoresController`  
**Propósito:** CRUD de jugadores.  

- `GET /api/jugadores` → Lista de jugadores.  
- `POST /api/jugadores` → Crear jugador.  
- `PUT /api/jugadores/{id}` → Editar jugador.  
- `DELETE /api/jugadores/{id}` → Eliminar jugador.  

### 📅 Partidos

**Controladores:** `PartidosController`, `Admin.PartidosController`  
**Propósito:** Programación y control de partidos.  

- `GET /api/partidos` → Lista de partidos.  
- `POST /api/partidos` → Programar nuevo partido.  
- `PUT /api/partidos/{id}` → Editar datos de un partido.  
- `DELETE /api/partidos/{id}` → Eliminar partido.  

### ⏱ CronometroController

**Propósito:** Control de tiempos del partido.  

- `GET /api/cronometro` → Estado del cronómetro.  
- `POST /api/cronometro/start` → Iniciar cronómetro.  
- `POST /api/cronometro/stop` → Detener cronómetro.  
- `POST /api/cronometro/reset` → Reiniciar cronómetro.  

### ⏸ TIemposMuertosController

**Propósito:** Manejo de tiempos muertos solicitados.  

- `GET /api/tiemposmuertos` → Lista de tiempos muertos.  
- `POST /api/tiemposmuertos` → Registrar tiempo muerto.  

### 🚨 FaltasController

**Propósito:** Registro y control de faltas.  

- `GET /api/faltas` → Lista de faltas.  
- `POST /api/faltas` → Registrar falta.  

### 🕓 CuatrosController

**Propósito:** Administración de cuartos del partido.  

- `GET /api/cuatros` → Obtener cuartos.  
- `POST /api/cuatros` → Crear cuarto.  

### 📊 MarcadorControlller

**Propósito:** Gestión del marcador en vivo.  

- `GET /api/marcador` → Estado actual del marcador.  
- `POST /api/marcador/update` → Actualizar marcador.  

### 📜 HistorialController

**Propósito:** Consultas de historial de partidos.  

- `GET /api/historial` → Lista de eventos/partidos anteriores.  

### ⚙ AjustesController

**Propósito:** Configuración general del sistema.  

- `GET /api/ajustes` → Ver ajustes.  
- `PUT /api/ajustes` → Actualizar ajustes.  

### 🏠 InicioController

**Propósito:** Pantalla inicial / pruebas de salud.  

- `GET /api/inicio` → Endpoint básico de inicio.  

### 📡 PingController

**Propósito:** Verificación de conexión / healthcheck.  

- `GET /api/ping` → Retorna un `pong` para verificar disponibilidad.  

---

## 🔴 Comunicación en Tiempo Real (Hubs)

El proyecto incluye una carpeta **/Hubs**, lo que indica el uso de **SignalR**.  
Esto permite:  

- Actualización en vivo del marcador de partidos.  
- Sincronización de eventos entre múltiples clientes conectados.  

Ejemplo de Hub:

```csharp
public class ScoreHub : Hub
{
    public async Task UpdateScore(string partidoId, int marcadorLocal, int marcadorVisitante)
    {
        await Clients.All.SendAsync("ReceiveScoreUpdate", partidoId, marcadorLocal, marcadorVisitante);
    }
}
```

---

## 🔐 Autenticación y Seguridad

- Basada en **JWT**.  
- Clave secreta configurada en `appsettings.json` (`Jwt:Key`).  
- Configuración de **Issuer** y **Audience**.  
- Rutas protegidas con `[Authorize]`.  
- Roles definidos en la entidad Usuario (ej: `Admin`, `User`).  

---

## ⚙ Configuración y Variables de Entorno

Archivo `appsettings.json` incluye:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=ProyectoWebDb;Trusted_Connection=True;Encrypt=False"
},
"Jwt": {
  "Key": "clave-secreta-aqui",
  "Issuer": "ProyectoAPI",
  "Audience": "ProyectoClient",
  "ExpiresInMinutes": 60
}
```

Variables importantes:

- `ConnectionStrings:DefaultConnection` → cadena de conexión a SQL Server  
- `Jwt:Key` → clave secreta JWT  
- `Jwt:Issuer` / `Jwt:Audience` → configuración de autenticación  

---

## 💻 Instalación y Ejecución

### Requisitos

- .NET 8 SDK  
- SQL Server 2022  
- Docker (opcional para despliegue)  

### Pasos

```bash
# 1. Clonar el repositorio
git clone https://github.com/ANDERSONISEM1/PROYECTO-II-DESARROLLO-WEB-BACKEND.git
cd PROYECTO-II-DESARROLLO-WEB-BACKEND

# 2. Configurar variables en appsettings.json

# 3. Restaurar dependencias
dotnet restore

# 4. Ejecutar migraciones (si existen)
dotnet ef database update

# 5. Ejecutar el servidor
dotnet run
```

El backend estará disponible en:  
👉 `https://localhost:5001`  
👉 `http://localhost:5000`  

---

## 🧪 Pruebas y Calidad

- Se recomienda probar endpoints con **Postman** o **Swagger**.  
- Middleware de logging habilitado para depuración.  
- (Si existen tests unitarios, ejecutar con `dotnet test`).  

---

## 🚀 Mejoras Futuras / Roadmap

- Documentar API con **Swagger / OpenAPI**  
- Manejo avanzado de errores y validaciones  
- Soporte multi-idioma  
- Escalabilidad con microservicios  
- Integración con CI/CD  

---

## 👨‍💻 Créditos y Licencia

- **Autores:** Equipo de Desarrollo Web – UMG  
- **Curso:** Desarrollo Web – Proyecto II  
- **Licencia:** MIT  
