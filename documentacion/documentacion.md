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
- `Data/InicializadorBaseDatos.cs`: preparación de esquema y datos iniciales.
- `Models/`: entidades de usuario, rol, carrera, evento, proyecto, registros y notificaciones.
- `Views/`: interfaz Razor.

## Modelo de datos

El esquema principal está descrito en [`GestionEventos.sql`](GestionEventos.sql):

| Tabla | Propósito |
|---|---|
| `ROLES` | Roles de estudiante, organizador/docente y administrador. |
| `CARRERAS` | Carreras asociadas a usuarios y proyectos. |
| `USUARIOS` | Perfil, correo único, rol, estado y hash de contraseña. |
| `EVENTOS` | Publicaciones de eventos, organizador, solicitante y estado de aprobación. |
| `PROYECTOS` | Publicaciones de proyectos, responsable, carrera y estado de aprobación. |
| `REGISTROS` | Inscripciones de usuarios a eventos; único por usuario y evento. |
| `REGISTROS_PROYECTOS` | Participación de colaboradores; única por usuario y proyecto. |
| `NOTIFICACIONES` | Avisos dirigidos a usuarios sobre publicaciones y cambios. |

Al dar de baja un evento o proyecto, la publicación se marca inactiva para conservar su historial y se notifica a los usuarios afectados. Las vistas públicas omiten publicaciones inactivas o pendientes de aprobación.

## Flujos principales

1. **Cuenta:** el usuario crea una cuenta de estudiante o inicia sesión. La autenticación utiliza cookies; las contraseñas se procesan con `IPasswordHasher<Usuario>`.
2. **Eventos:** los usuarios exploran eventos y pueden inscribirse con sesión iniciada. Organizadores gestionan sus publicaciones y los administradores resuelven tareas administrativas.
3. **Proyectos:** usuarios exploran proyectos y se registran como colaboradores. Las solicitudes se someten a los flujos de aprobación configurados por rol.
4. **Participación y avisos:** el usuario consulta y cancela sus registros desde su agenda. Las notificaciones se muestran en la vista de agenda.
5. **Recursos no encontrados:** si una ruta es inválida o un recurso no está disponible, se muestra la vista 404 de la aplicación y se conserva el estado HTTP 404.

## Inicialización de base de datos

1. Ejecuta `GestionEventos.sql` en una instancia de SQL Server nueva para crear la base de datos y el esquema base.
2. Configura `ConnectionStrings__GestionEventos` fuera del repositorio.
3. Inicia la aplicación. El arranque prepara algunos elementos de esquema y puede crear cuentas iniciales si las variables `BootstrapUsers__AdminEmail`, `BootstrapUsers__AdminPassword`, `BootstrapUsers__OrganizerEmail` y `BootstrapUsers__OrganizerPassword` están definidas.

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
```

La ejecución completa necesita SQL Server accesible y un esquema inicializado.
