# Gestión de eventos universitarios

Aplicación web ASP.NET Core MVC para publicar y administrar eventos y proyectos académicos, y facilitar la participación de la comunidad universitaria. La interfaz está en español y utiliza SQL Server con Entity Framework Core.

## Funcionalidades

- Registro e inicio de sesión con contraseñas almacenadas mediante el hasher de ASP.NET Core.
- Exploración de eventos y proyectos publicados, con páginas de detalle.
- Creación y edición de eventos y proyectos, con flujos de aprobación y permisos por rol.
- Inscripción a eventos y registro como colaborador de proyectos; consulta y cancelación de participaciones.
- Notificaciones sobre cambios y bajas de eventos y proyectos.
- Administración de usuarios, carreras y publicaciones.
- Página 404 para direcciones inválidas y recursos no disponibles.

## Requisitos

- .NET SDK 10.
- SQL Server y una base de datos accesible por la aplicación.
- PowerShell (los comandos siguientes usan sintaxis de PowerShell).

## Configuración local

1. Clona el repositorio y entra al directorio del proyecto.
2. Crea la base de datos ejecutando una sola vez [`documentacion/GestionEventos.sql`](documentacion/GestionEventos.sql) en SQL Server. El script crea el esquema inicial, los roles y carreras de ejemplo.
3. Configura la conexión por variable de entorno. Ejemplo con autenticación integrada:

   ```powershell
   $env:ConnectionStrings__GestionEventos = "Server=localhost;Database=GestionEventosUnison;Trusted_Connection=True;TrustServerCertificate=True"
   ```

   Si tu servidor requiere usuario y contraseña, obtén esos valores de un almacén de secretos local o del entorno de despliegue. No los agregues a `appsettings*.json` ni al repositorio.
4. Restaura, compila e inicia la aplicación:

   ```powershell
   dotnet restore
   dotnet build
   dotnet run --project GestionEventos.csproj
   ```

La aplicación inicia en la vista de exploración de eventos. También puede abrirse `GestionEventos.csproj` desde Visual Studio.

### Cuentas iniciales

El arranque puede crear una cuenta inicial de administrador y otra de organizador solo cuando no exista ya una cuenta para ese rol y se proporcionen explícitamente las credenciales por variables de entorno:

```powershell
$env:BootstrapUsers__AdminEmail = "admin@ejemplo.invalid"
$env:BootstrapUsers__AdminPassword = [System.Net.NetworkCredential]::new('', (Read-Host 'Contraseña del administrador' -AsSecureString)).Password
$env:BootstrapUsers__OrganizerEmail = "organizador@ejemplo.invalid"
$env:BootstrapUsers__OrganizerPassword = [System.Net.NetworkCredential]::new('', (Read-Host 'Contraseña del organizador' -AsSecureString)).Password
```

Usa correos reales bajo tu control y contraseñas fuertes. Las variables son solo para la sesión actual de PowerShell; configúralas mediante el mecanismo de secretos adecuado para el entorno donde ejecutes la aplicación. Si no se proporcionan, la aplicación no crea esas cuentas y registra una advertencia.

## Roles

- **Estudiante:** consulta publicaciones, se registra en eventos y proyectos y revisa sus participaciones y notificaciones.
- **Organizador/docente:** administra sus publicaciones y resuelve solicitudes de proyectos que le corresponden.
- **Administrador:** gestiona usuarios, carreras y publicaciones.

Las cuentas nuevas se asignan al rol de estudiante. Las funciones protegidas requieren iniciar sesión y las acciones administrativas verifican el rol.

## Estructura del proyecto

| Ruta | Responsabilidad |
|---|---|
| `Controllers/` | Acciones MVC, autenticación, permisos y flujos de negocio. |
| `Models/` | Entidades persistidas en SQL Server. |
| `ViewModels/` | Datos y validación para formularios. |
| `Data/` | Contexto de Entity Framework Core y preparación del esquema. |
| `Views/` | Vistas Razor para las pantallas de la aplicación. |
| `wwwroot/` | CSS, JavaScript, Bootstrap e imágenes estáticas. |
| `documentacion/` | Script de base de datos y documentación funcional/técnica. |

Consulta la [guía técnica](documentacion/documentacion.md) para conocer el modelo de datos y los flujos principales.

## Validación

```powershell
dotnet build GestionEventos.csproj
```

El inicio y las pruebas funcionales requieren además una instancia de SQL Server con la base de datos preparada.

## Seguridad y configuración

- No subas contraseñas, cadenas de conexión con credenciales, archivos `.env`, certificados, claves privadas, configuraciones locales ni archivos de usuario de Visual Studio.
- `appsettings*.json` se excluye de Git; `appsettings.example.json` es una plantilla sin datos de acceso.
- Para producción, configura secretos fuera del código, habilita HTTPS y asigna permisos mínimos a la identidad de base de datos.
- Si una credencial se compartió o se agregó accidentalmente a Git, elimínala del historial y rótala; borrarla del último commit no la invalida.

## Licencia

No se ha especificado una licencia. Todos los derechos permanecen con sus titulares hasta que se agregue explícitamente una.
