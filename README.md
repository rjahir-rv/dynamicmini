# Dynamic Mini Player

Mini reproductor universal para Windows 11. Lee la sesión multimedia que Windows recibe de Chrome, Edge, Firefox, Spotify y otras aplicaciones compatibles, y permite controlarla sin cambiar de ventana.

## Funciones

- Tarjeta compacta sobre la barra de tareas que se abre bajo demanda desde la bandeja y se oculta al hacer clic fuera.
- Modo persistente opcional para mostrarla automáticamente cuando empieza a sonar algo.
- Portada, título, artista y barra de progreso con búsqueda por clic o arrastre.
- Controles de anterior, reproducir/pausar, siguiente y retroceder/avanzar 10 segundos.
- Selección automática de Windows y selector manual entre sesiones.
- Icono y menú en la bandeja del sistema.
- Autoarranque opcional, preferencias locales e instancia única.
- Sin cuentas, telemetría ni conexiones propias a Internet.

## Uso

- **Clic izquierdo** en el icono de la bandeja: mostrar u ocultar el reproductor.
- **Clic derecho** en el icono de la bandeja: modo, fuente multimedia, inicio con Windows, ajustes y salir.

| Atajo | Acción |
| --- | --- |
| `Espacio` | Reproducir / pausar |
| `←` / `→` | Retroceder / avanzar 10 s |
| `Esc` | Ocultar el reproductor |

## Desarrollo

Requisitos: Windows 11 x64 y [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet restore DynamicMiniPlayer.slnx
dotnet build DynamicMiniPlayer.slnx
dotnet test DynamicMiniPlayer.slnx
dotnet run --project src/DynamicMiniPlayer/DynamicMiniPlayer.csproj
```

Estructura del repositorio:

- `src/DynamicMiniPlayer/`: aplicación WPF (`ViewModels`, `Services`, `Models`, `Assets`).
- `tests/DynamicMiniPlayer.Tests/`: pruebas unitarias con MSTest.
- `scripts/`: utilidades de compilación y publicación.

Para generar una compilación autocontenida:

```powershell
.\scripts\publish.ps1
```

El ejecutable quedará en `artifacts\publish\win-x64`. Para anclarlo, ejecútalo y usa **clic derecho → Anclar a la barra de tareas** sobre su icono. Windows no permite que una app realice ese anclaje permanentemente sin confirmación del usuario.

## Notas de compatibilidad

La información y los controles disponibles dependen de lo que publique cada fuente en los controles multimedia de Windows. Si una página no permite avanzar, retroceder o buscar una posición, la interfaz deshabilita ese control. La aplicación no necesita extensiones de navegador.

## Licencia

Distribuido bajo la licencia MIT. Consulta [LICENSE](LICENSE).
