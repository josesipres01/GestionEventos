# Guía funcional y técnica

## Arquitectura

La solución sigue el patrón Model-View-Controller (MVC) de ASP.NET Core. Los controladores validan solicitudes y permisos, las vistas Razor presentan la interfaz y Entity Framework Core accede a SQL Server.

### Componentes

- `Controllers/CuentaController.cs`: registro, inicio/cierre de sesión y acceso denegado.
- `Controllers/EventosController.cs`: exploración, administración de eventos, inscripciones y notificaciones.
- `Controllers/ProyectosController.cs`: exploración, publicación, colaboración y baja de proyectos.
- `Controllers/SolicitudesController.cs`: revisión de solicitudes y reactivaciones.
- `Controllers/AdminController.cs`: administración de publicaciones, usuarios y carreras.
- `Data/GestionEventosDbContext.cs`: configuración de entidades y relaciones.
- `Data/Migrations/`: única fuente de cambios del esquema.
- `Data/InicializadorBaseDatos.cs`: adopción de bases legadas reconocidas y semillas de datos iniciales.
- `Models/`: entidades de usuario, rol, carrera, evento, proyecto, registros y notificaciones.
- `Views/`: interfaz Razor.

## Modelo de datos

El esquema se define exclusivamente en las migraciones de EF Core. El archivo [`GestionEventos.sql`](GestionEventos.sql) contiene solo datos semilla opcionales. El diagrama visual está en [`modelo-entidad-relacion.svg`](modelo-entidad-relacion.svg).

| Tabla | Propósito |
|---|---|
| `ROLES` | Roles de estudiante, organizador/docente y administrador. |
| `CARRERAS` | Carreras asociadas a usuarios y proyectos. |
| `USUARIOS` | Perfil, correo único, rol, estado y hash de contraseña. |
| `EVENTOS` | Publicaciones con organizador, solicitante, revisor, eliminador, fecha de fin, cupo opcional y estado de aprobación controlado. |
| `PROYECTOS` | Publicaciones con creador, organizador responsable, revisor, eliminador, carrera y estado de aprobación controlado. |
| `REGISTROS` | Inscripciones de usuarios a eventos, con fecha; único por usuario y evento. |
| `REGISTROS_PROYECTOS` | Solicitudes de colaboración con estado, revisión y fecha; único por usuario y proyecto. |
| `NOTIFICACIONES` | Avisos con fecha y estado de lectura; asociados a un evento, a un proyecto o a ninguno. |

Las referencias a usuarios usan `NO ACTION`: las cuentas deben desactivarse lógicamente para preservar historial. La baja de una publicación conserva sus registros y guarda quién la realizó. Los avisos de baja de proyectos se envían al creador y a los colaboradores aceptados.

Las solicitudes de colaboración permanecen pendientes hasta que el organizador asignado las acepte o rechace. Solo los colaboradores aceptados se muestran como participantes. El cupo de un evento es opcional; cuando se define, las inscripciones se serializan y el servidor impide excederlo.

## Flujos principales

1. **Cuenta:** el usuario crea una cuenta de estudiante o inicia sesión. La autenticación utiliza cookies; las contraseñas se procesan con `IPasswordHasher<Usuario>`.
2. **Eventos:** los usuarios exploran eventos y pueden inscribirse con sesión iniciada. Organizadores gestionan sus publicaciones y los administradores resuelven tareas administrativas.
3. **Proyectos:** usuarios exploran proyectos y se registran como colaboradores. Las solicitudes se someten a los flujos de aprobación configurados por rol.
4. **Participación y avisos:** el usuario consulta y cancela sus registros desde su agenda. Las notificaciones se muestran en la vista de agenda.
5. **Recursos no encontrados:** si una ruta es inválida o un recurso no está disponible, se muestra la vista 404 de la aplicación y se conserva el estado HTTP 404.

## Inicialización y migraciones

1. Crea una base vacía en SQL Server y configura `ConnectionStrings__GestionEventos` fuera del repositorio.
2. Inicia la aplicación; `Database.Migrate()` crea o actualiza el esquema.
3. Si la base contiene el esquema legado reconocido pero aún no tiene historial de EF Core, el arranque registra la migración inicial como línea base y aplica la migración de modernización. Un esquema parcial no reconocido provoca un error explícito para evitar alteraciones destructivas.
4. Ejecuta `GestionEventos.sql` solo si quieres cargar manualmente las carreras/roles de ejemplo; no crea ni modifica tablas.

Las migraciones preservan las inscripciones existentes a proyectos como aceptadas y derivan el organizador desde la relación previa o, como respaldo, desde el nombre guardado. Las cuentas iniciales se crean después de las migraciones si las variables `BootstrapUsers__AdminEmail`, `BootstrapUsers__AdminPassword`, `BootstrapUsers__OrganizerEmail` y `BootstrapUsers__OrganizerPassword` están definidas.

Las credenciales de las cuentas iniciales no tienen valores predeterminados en el código. Deben proporcionarse por un almacén de secretos o variables de entorno; si falta una de las dos variables de una cuenta, el arranque reporta el error explícitamente.

## Configuración

ASP.NET Core permite sobrescribir la configuración mediante variables de entorno. Los dos puntos de las claves se representan con doble guion bajo:

| Clave de configuración | Variable de entorno |
|---|---|
| `ConnectionStrings:GestionEventos` | `ConnectionStrings__GestionEventos` |
| `BootstrapUsers:AdminEmail` | `BootstrapUsers__AdminEmail` |
| `BootstrapUsers:AdminPassword` | `BootstrapUsers__AdminPassword` |
| `BootstrapUsers:OrganizerEmail` | `BootstrapUsers__OrganizerEmail` |
| `BootstrapUsers:OrganizerPassword` | `BootstrapUsers__OrganizerPassword` |

No guardes valores reales en archivos versionados. Usa `appsettings.example.json` como referencia para configuración no sensible y el almacén de secretos correspondiente a cada entorno.

## Compilación

Desde el directorio raíz:

```powershell
dotnet restore
dotnet build GestionEventos.csproj
dotnet test tests/GestionEventos.Tests/GestionEventos.Tests.csproj
```

Las pruebas usan una base SQLite efímera y cubren permisos/revisión de colaboradores, inscripciones duplicadas y cupo, baja del proyecto y notificaciones. El workflow `.github/workflows/dotnet.yml` ejecuta build y test en GitHub Actions. La ejecución de la aplicación requiere SQL Server accesible.
