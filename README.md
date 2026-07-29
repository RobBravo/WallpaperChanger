<div align="center">

# WallpaperChanger

### Fondos de pantalla independientes para cada monitor en Windows 11

**.NET 8** · **Windows 11** · **WPF**

</div>

## Una experiencia de escritorio que se adapta a cada pantalla

`WallpaperChanger` rota fondos reales de Windows de forma independiente por monitor. Elige una carpeta y un intervalo para cada pantalla; la aplicación conserva la programación y evita repetir imágenes hasta completar el ciclo.

## Características

- Configuración independiente de carpeta e intervalo por monitor.
- Rotación aleatoria con bolsa de selección sin repeticiones.
- Aplicación inmediata y programación persistente.
- Ícono de bandeja para abrir la configuración o salir.
- Inicio automático para el usuario actual de Windows.
- Uso del fondo de escritorio nativo de Windows, no ventanas superpuestas.

## Requisitos

- Windows 11.
- .NET SDK 8.0 o superior para compilar desde código fuente.
- Visual Studio 2022 con la carga de trabajo **Desarrollo de escritorio con .NET**.
- Una o más carpetas que contengan archivos `.jpg`, `.jpeg`, `.png` o `.bmp`.

## Instalación

```powershell
git clone https://github.com/RobBravo/WallpaperChanger.git
Set-Location WallpaperChanger
dotnet restore
dotnet build
```

Abre `WallpaperChanger.sln` en Visual Studio 2022 y presiona **F5** para iniciar la aplicación.

## Crear el instalador

El instalador se genera para Windows x64, se instala por usuario y no requiere permisos de administrador.

1. Instala Inno Setup 6 una sola vez:

   ```powershell
   winget install --id JRSoftware.InnoSetup --exact
   ```

2. Genera el ejecutable y el instalador:

   ```powershell
   .\scripts\build-installer.ps1
   ```

El instalador resultante se encuentra en `artifacts\installer\WallpaperChanger-Setup.exe`. Esta primera versión no está firmada digitalmente, por lo que Windows SmartScreen puede mostrar una advertencia al ejecutar un archivo descargado.

## Configuración por monitor

1. Abre la ventana desde el ícono de bandeja.
2. Selecciona una carpeta para cada monitor conectado.
3. Indica el intervalo y su unidad: minutos, horas o días.
4. Pulsa **Aplicar ahora** para establecer una imagen de inmediato.
5. Cierra la ventana; la aplicación sigue funcionando desde la bandeja.

## Cómo funciona

Cada monitor conserva su propia configuración, próxima ejecución y estado de selección. Cuando llega el momento programado, la aplicación obtiene una imagen de la carpeta asignada, la aplica mediante la API de escritorio de Windows y guarda el nuevo estado. Al agotar una ronda de imágenes, crea una nueva bolsa aleatoria.

## Persistencia e inicio automático

La configuración se guarda por usuario en `%LocalAppData%\WallpaperChanger\settings.json`. El inicio automático se registra en la clave `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, sin requerir privilegios de administrador.

## Arquitectura

| Componente | Responsabilidad |
| --- | --- |
| Lógica de la aplicación | Modelos, programación, selección aleatoria y persistencia JSON. |
| Aplicación de escritorio | Interfaz WPF, ícono de bandeja, monitores de Windows e integración con el fondo de escritorio. |
| Pruebas automatizadas | Pruebas unitarias de la lógica y de los flujos del modelo de vista. |

## Pruebas

```powershell
dotnet test
```

## Notas

- La aplicación solo funciona en Windows porque usa las API de fondo de escritorio y monitor de Windows.
- Si una carpeta deja de existir o no contiene imágenes compatibles, la rotación de ese monitor se pausa hasta corregirla.
