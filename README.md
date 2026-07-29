<div align="center">

# WallpaperChanger

### Fondos de pantalla independientes para cada monitor en Windows 11

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Windows 11](https://img.shields.io/badge/Windows-11-0078D4?logo=windows11&logoColor=white)
![WPF](https://img.shields.io/badge/UI-WPF-0C54C2)

</div>

## Una experiencia de escritorio que se adapta a cada pantalla

`WallpaperChanger` rota fondos reales de Windows de forma independiente por monitor. Elige una carpeta y un intervalo para cada pantalla; la aplicacion conserva la programacion y evita repetir imagenes hasta completar el ciclo.

## Caracteristicas

- Configuracion independiente de carpeta e intervalo por monitor.
- Rotacion aleatoria con bolsa de seleccion sin repeticiones.
- Aplicacion inmediata y programacion persistente.
- Icono de bandeja para abrir la configuracion o salir.
- Inicio automatico para el usuario actual de Windows.
- Uso del fondo de escritorio nativo de Windows, no ventanas superpuestas.

## Requisitos

- Windows 11.
- .NET SDK 8.0 o superior para compilar desde codigo fuente.
- Una o mas carpetas que contengan archivos `.jpg`, `.jpeg`, `.png` o `.bmp`.

## Instalacion

```powershell
git clone https://github.com/RobBravo/WallpaperChanger.git
Set-Location WallpaperChanger
dotnet restore
dotnet run --project src/WallpaperChanger.App
```

## Configuracion por monitor

1. Abre la ventana desde el icono de bandeja.
2. Selecciona una carpeta para cada monitor conectado.
3. Indica el intervalo y su unidad: minutos, horas o dias.
4. Pulsa **Aplicar ahora** para establecer una imagen de inmediato.
5. Cierra la ventana; la aplicacion sigue funcionando desde la bandeja.

## Como funciona

Cada monitor conserva su propia configuracion, proxima ejecucion y estado de seleccion. Cuando llega el momento programado, la aplicacion obtiene una imagen de la carpeta asignada, la aplica mediante la API de escritorio de Windows y guarda el nuevo estado. Al agotar una ronda de imagenes, crea una nueva bolsa aleatoria.

## Persistencia e inicio automatico

La configuracion se guarda por usuario en `%LocalAppData%\WallpaperChanger\settings.json`. El inicio automatico se registra en la clave `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, sin requerir privilegios de administrador.

## Arquitectura

| Proyecto | Responsabilidad |
| --- | --- |
| `WallpaperChanger.Core` | Modelos, programacion, seleccion aleatoria y persistencia JSON. |
| `WallpaperChanger.App` | Interfaz WPF, icono de bandeja, monitores de Windows e integracion con el fondo de escritorio. |
| `tests/*` | Pruebas unitarias de la logica y de los flujos del modelo de vista. |

## Pruebas

```powershell
dotnet test
```

## Notas

- La aplicacion solo funciona en Windows porque usa las APIs de fondo de escritorio y monitor de Windows.
- Si una carpeta deja de existir o no contiene imagenes compatibles, la rotacion de ese monitor se pausa hasta corregirla.
