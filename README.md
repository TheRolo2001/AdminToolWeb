# ⚙️ AdminTool Web

Herramienta web de **administración remota de equipos Windows** en redes con Active Directory. Permite inventariar una red, renombrar equipos de forma masiva y habilitar WinRM de manera remota, todo desde una sola interfaz en el navegador.

Construida con **Blazor Server (.NET 8)**.

> Proyecto personal basado en tareas reales de soporte TI y administración de sistemas. Todos los dominios, rangos de red y datos de ejemplo son genéricos.

---

## ✨ Funcionalidades

### 📊 Inventario de red
- Escaneo de un rango de IPs en paralelo (ping, resolución de hostname y detección de puertos/servicios).
- Clasificación automática del tipo de equipo (servidor web, servidor de archivos, estación de trabajo).
- Con credenciales, consulta por **WMI** el serial, fabricante, modelo, sistema operativo, versión, RAM, dominio y usuario activo.
- Barra de progreso, cancelación del escaneo y registro en vivo.
- Exportación de resultados a **CSV**.

### ✏️ Renombrar equipos
- Cuatro formas de armar la cola de trabajo: **IP manual**, **escaneo de red**, **patrón automático** (`PC-{N3}`) e **importación desde CSV**.
- Validación de nombres según las reglas de NetBIOS (máx. 15 caracteres, solo letras, números y guiones).
- Renombrado con **cascada de métodos**: WMI nativo → WinRM (PowerShell) → WMIC, hasta que uno funcione.
- Reinicio opcional del equipo y registro del resultado por cada máquina.

### 🔌 Habilitar WinRM
- Activa WinRM de forma remota usando **PsExec**.
- Verifica que el servicio quede funcionando antes de dar el equipo por configurado.
- Diagnóstico por equipo: ping, WinRM (5985/5986), SMB, DCOM y RDP.

### ⚙️ Configuración
- Red base, dominio, ruta de PsExec y carpeta de archivos generados.
- Las credenciales se piden una sola vez y se reutilizan en todos los módulos durante la sesión.

---

## 🧱 Arquitectura

```
AdminToolWeb/
├── Program.cs                  # Configuración de la app y servicios (DI)
├── Models/
│   └── Modelos.cs              # Credenciales, EquipoInfo, AppConfig, etc.
├── Services/
│   ├── SesionService.cs        # Credenciales y configuración por sesión
│   ├── RedService.cs           # Ping, puertos, DNS, detección de servicios
│   ├── WmiService.cs           # Consultas y acciones remotas vía WMI
│   ├── InventarioService.cs    # Escaneo masivo y exportación CSV
│   ├── RenombrarService.cs     # Cola de renombrado y métodos en cascada
│   └── WinRMService.cs         # Habilitación y diagnóstico de WinRM
├── Components/
│   ├── Layout/MainLayout.razor # Barra lateral y estructura general
│   └── Pages/                  # Inicio, Inventario, Renombrar, WinRM, Configuración
└── wwwroot/app.css             # Estilos
```

**Puntos técnicos destacados**
- Servicios registrados como *Scoped*: cada sesión web mantiene su propio estado.
- Escaneos concurrentes con `Parallel.ForEach` y `CancellationToken`.
- Actualización de la UI en tiempo real mediante eventos (`OnCambio`) en componentes interactivos de servidor.
- Estrategia de *fallback* entre tres canales de administración remota.

---

## 🛠️ Tecnologías

- C# / .NET 8
- Blazor Server (componentes interactivos)
- `System.Management` (WMI)
- PowerShell y WinRM
- PsExec (Sysinternals)

---

## 📋 Requisitos

- Windows (el proyecto usa `net8.0-windows` y WMI)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PsExec](https://learn.microsoft.com/sysinternals/downloads/psexec) (solo para el módulo de WinRM)
- Una cuenta con permisos administrativos sobre los equipos objetivo

---

## 🚀 Ejecución

```bash
git clone https://github.com/<TheRolo2001>/AdminToolWeb.git
cd AdminToolWeb
dotnet run
```

Abre `http://localhost:5000` en el navegador, inicia sesión con tus credenciales de dominio y ajusta la **red base** y el **dominio** en *Configuración*.

---

## 🔐 Notas de seguridad

Esta herramienta está pensada para **uso interno y en laboratorios**, y ejecuta acciones administrativas sobre equipos reales. Antes de usarla en producción ten en cuenta:

- Por defecto escucha en `0.0.0.0:5000` (toda la red local) y **no tiene autenticación propia**. Cámbialo a `localhost` o protégela con autenticación y HTTPS.
- Las credenciales se mantienen en memoria durante la sesión y algunas herramientas externas (PsExec, WMIC) las reciben por línea de comandos.
- El módulo de WinRM configura `TrustedHosts` con `*`. Restríngelo a los equipos que realmente administres.
- Úsala únicamente en redes y equipos sobre los que tengas autorización.

---

## 🗺️ Ideas para mejorar

- Autenticación y roles para acceder a la aplicación.
- Soporte de HTTPS y WinRM sobre HTTPS (5986).
- Persistencia del historial de inventarios.
- Exportación a Excel y comparación entre escaneos.
- Pruebas unitarias para la validación y los patrones de nombres.
