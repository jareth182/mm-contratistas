# M&M Contratistas

Sistema web de gestión de obras para **M&M Contratistas**. Reúne en una sola plataforma el sitio público de la empresa (portafolio, servicios y cotizaciones) y la gestión interna de proyectos: hitos y avances, cuadrillas, documentación, presupuesto, gastos y pagos. Además, ofrece un portal para que el cliente siga su obra.

## Tabla de contenido

- [Funcionalidades](#funcionalidades)
- [Tecnologías](#tecnologías)
- [Estructura del repositorio](#estructura-del-repositorio)
- [Requisitos](#requisitos)
- [Puesta en marcha local](#puesta-en-marcha-local)
- [Variables de entorno](#variables-de-entorno)
- [Pruebas](#pruebas)
- [Despliegue (Render)](#despliegue-render)
- [Flujo de trabajo con Git](#flujo-de-trabajo-con-git)
- [Equipo](#equipo)

## Funcionalidades

### Roles

| Rol | Acceso |
|---|---|
| Administrador | Todo el sistema, incluida la administración de usuarios y roles |
| Gerente de Operaciones | Gestión de proyectos, personal, presupuesto, gastos y pagos |
| Supervisor de Obra | Registro de avances, cuadrillas y documentación de sus obras |
| Cliente | Portal de seguimiento de su propia obra |

### Historias de usuario implementadas

| Módulo | Historias |
|---|---|
| Seguridad | HU01 Inicio de sesión con JWT, contraseñas cifradas, bloqueo y acceso por rol · HU02 Cierre de sesión manual y por inactividad · HU03 Recuperación de contraseña por correo · HU04 Administración de usuarios y roles |
| Sitio público | HU05 Portafolio de proyectos históricos · HU06 Catálogo de servicios · HU07 Solicitud de cotización en línea |
| Contenido | HU08 Mantenimiento del portafolio · HU09 Mantenimiento del catálogo de servicios |
| Proyectos | HU10 Personal de obra · HU11 Registro de proyectos · HU12 Hitos · HU13 Avance de hitos · HU14 Asignación de cuadrillas · HU15 Documentación de proyectos |
| Control económico | HU16 Presupuesto por partidas · HU17 Gastos de proyecto (con comprobantes y anulación) · HU18 Pagos a personal · HU19 Presupuestado vs. gastado |
| Portal del cliente | HU20 Seguimiento de obra · HU21 Detalle de gastos con comprobantes · HU22 Comentarios en hitos |
| Cumplimiento | HU23 Vencimiento de SCTR y exámenes médicos · HU24 Alertas de documentos por vencer |

### Historias técnicas

- **HT-04**: proceso programado diario de vencimientos documentales.
- **HT-05**: servicio unificado de notificaciones, con registro y control de duplicados.
- **HT-06**: autorización por recurso y descarga segura de archivos.
- **HT-07**: paginación, filtros estándar e índices en gastos y pagos.

## Tecnologías

- **Backend**: ASP.NET Core 9 (Web API), Entity Framework Core 8 con PostgreSQL (Npgsql), autenticación JWT Bearer y `DotNetEnv` para cargar la configuración desde `.env`.
- **Frontend**: HTML, CSS y JavaScript sin frameworks ni proceso de compilación (SPA servida como archivos estáticos).
- **Pruebas**: xUnit con SQLite en memoria.
- **CI**: GitHub Actions con escaneo de vulnerabilidades de NuGet y análisis estático con Semgrep.
- **Despliegue**: Docker en Render.

## Estructura del repositorio

```
mm-contratistas/
├── backend/
│   ├── MymContratistas.Api/          # API REST
│   │   ├── Controllers/              # Endpoints (api/[controller])
│   │   ├── Data/                     # DbContext y datos iniciales (DbSeeder)
│   │   ├── Migrations/               # Migraciones de EF Core
│   │   ├── Models/                   # Entidades y DTOs
│   │   ├── Services/                 # Tokens, correo, notificaciones, alertas, paginación
│   │   ├── Uploads/                  # Archivos subidos (no se versionan)
│   │   └── .env.example              # Plantilla de variables de entorno
│   └── MymContratistas.Api.Tests/    # Pruebas automatizadas (xUnit)
├── frontend/
│   ├── index.html
│   ├── css/style.css
│   └── js/                           # api.js, app.js, components.js, state.js, views.js
├── .github/workflows/security.yml    # Escaneo de seguridad en CI
└── Dockerfile                        # Imagen de producción (API + frontend)
```

## Requisitos

- [.NET SDK 9.0](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/download/) 14 o superior
- (Opcional) Docker, para probar la imagen de producción
- (Opcional) `dotnet-ef`, para crear migraciones nuevas: `dotnet tool install --global dotnet-ef`

## Puesta en marcha local

1. **Clona el repositorio**

   ```bash
   git clone https://github.com/jareth182/mm-contratistas.git
   cd mm-contratistas
   ```

2. **Crea la base de datos** en PostgreSQL:

   ```sql
   CREATE DATABASE mym_contratistas;
   ```

3. **Configura las variables de entorno**. Copia la plantilla y completa los valores (ver [Variables de entorno](#variables-de-entorno)):

   ```bash
   cp backend/MymContratistas.Api/.env.example backend/MymContratistas.Api/.env
   ```

4. **Ejecuta la API**:

   ```bash
   cd backend/MymContratistas.Api
   dotnet run
   ```

   Al iniciar, la API aplica automáticamente las migraciones pendientes y, si la tabla de usuarios está vacía, crea los usuarios y servicios de ejemplo.

5. **Abre el frontend**. Abre `frontend/index.html` directamente en el navegador. Cuando se abre como archivo (`file://`), el frontend llama a la API en `http://localhost:5067` (ver `frontend/js/api.js`). Si la sirves desde la propia API, usa rutas relativas.

### Usuarios de ejemplo

Se crean solo si la tabla `Usuarios` está vacía y `Seed__Password` tiene un valor. Todos comparten esa contraseña.

| Correo | Rol |
|---|---|
| maribel.minaya@mym.pe | Administrador |
| marco.zapata@mym.pe | Gerente de Operaciones |
| romulo.ccahuana@mym.pe | Supervisor de Obra |
| contacto@delrio.pe | Cliente |

## Variables de entorno

La API lee `backend/MymContratistas.Api/.env` (ignorado por Git) o, en producción, las variables del entorno. El doble guion bajo (`__`) equivale a una sección anidada de la configuración.

| Variable | Descripción |
|---|---|
| `ConnectionStrings__DefaultConnection` | Cadena de conexión de PostgreSQL |
| `Jwt__Key` | Clave secreta para firmar los JWT (mínimo 32 caracteres, aleatoria) |
| `Jwt__Issuer` | Emisor del token (`MymContratistas.Api`) |
| `Jwt__Audience` | Audiencia del token (`MymContratistas.Web`) |
| `Seed__Password` | Contraseña inicial de los usuarios de ejemplo |
| `App__FrontendUrl` | URL del frontend, usada en los enlaces de los correos |
| `Smtp__Host`, `Smtp__Port` | Servidor SMTP (por ejemplo, `smtp.gmail.com` y `587`) |
| `Smtp__User`, `Smtp__Password` | Credenciales SMTP (en Gmail, una contraseña de aplicación). Si están vacías, los correos solo se escriben en el log |

> Nunca subas el archivo `.env` ni publiques claves reales en el repositorio.

## Pruebas

```bash
cd backend
dotnet test MymContratistas.Api.Tests
```

Las pruebas usan SQLite en memoria, así que no requieren PostgreSQL. Cubren las historias de usuario, la seguridad, la autorización por recurso, la paginación y el proceso programado de alertas.

## Despliegue (Render)

La aplicación se despliega como un único servicio Docker: la API sirve el frontend desde `wwwroot`.

1. En Render, crea una base de datos **PostgreSQL** y un **Web Service** con entorno *Docker* apuntando a este repositorio (rama `main`).
2. Configura en el Web Service las [variables de entorno](#variables-de-entorno). `ConnectionStrings__DefaultConnection` debe usar los datos de la base creada en Render, y `App__FrontendUrl` la URL pública del servicio.
3. Render define la variable `PORT`. El contenedor escucha en ese puerto (por defecto, `8080`).
4. En el primer arranque, la API aplica las migraciones y carga los datos iniciales.

Para probar la imagen en local:

```bash
docker build -t mm-contratistas .
docker run -p 8080:8080 --env-file backend/MymContratistas.Api/.env mm-contratistas
```

> Dentro del contenedor, `localhost` apunta al propio contenedor. Para usar un PostgreSQL de tu máquina, cambia el host a `host.docker.internal`.

## Flujo de trabajo con Git

| Rama | Propósito |
|---|---|
| `main` | Código en producción. Solo recibe merges desde `release` y `hotfix` |
| `develop` | Rama de integración del sprint en curso |
| `release` | Estabilización y pruebas antes de pasar a producción |
| `hotfix` | Correcciones urgentes sobre producción |
| `feature/huXX-descripcion` | Una rama por historia de usuario o técnica, creada desde `develop` |

Flujo:

1. Crea tu rama desde `develop`: `git switch -c feature/hu25-descripcion origin/develop`.
2. Usa commits con el formato `tipo(HUxx): descripción` (por ejemplo, `feat(HU13): registrar avance de un hito`).
3. Abre un Pull Request hacia `develop`. El escaneo de seguridad se ejecuta en cada PR.
4. Al cerrar el sprint, pasa `develop` a `release`, prueba y fusiona `release` en `main`.
5. Para corregir producción, trabaja en `hotfix` y fusiónala en `main` y en `develop`.
6. Después del merge, borra la rama de la historia.

## Equipo

Proyecto desarrollado por el equipo de M&M Contratistas con metodología Scrum. El backlog del sprint está en la hoja de cálculo del proyecto.
