# Validación de v0.1

## Reproducible sin teclado

```powershell
dotnet build K585Studio.sln -c Release -p:NuGetAudit=false
dotnet run --project tests/K585Studio.Checks -c Release --no-build
```

El ejecutable de comprobaciones usa transporte simulado. Cubre cabeceras y suma de petición, límites de regiones, rechazo del perfil 1 y modo 20, falta de confirmación de firmware, cambios externos, perfil activo, respaldo fallido, verificación integral tras escritura, timeout sin reintento y bloqueo de una sesión fallida. Comprueba también borradores, macros y destino de URLs de actualización. Un PASS aquí no es una prueba física del teclado.

## Interfaz en segundo plano

```powershell
.\K585Studio.exe --ui-self-test C:\Temp\K585Studio-UI
```

Construye controles invisibles propios de Studio y los dibuja en imágenes, sin mostrar ventanas ni controlar otras aplicaciones. No abre HID, no captura entrada ni carga/guarda el proyecto personal. Comprueba 42 áreas de teclas, G1–G5, borradores de iluminación, perfil 1 bloqueado, indicadores de asignación y orden de pasos de macro. Produce `ui-checks.json` e imágenes. Código de salida 0 indica que las 18 comprobaciones pasaron. La grabación con eventos reales y los diálogos nativos requieren validación interactiva por separado.

## Resultado local inicial

La compilación Release pasó sin avisos; pasaron 78 comprobaciones de protocolo/modelo y 18 de interfaz en segundo plano a 192 DPI. El transporte C# realizó cuatro lecturas completas del teclado real coincidentes: perfil activo 2, modo 23, brillo 4, velocidad de hardware 4, Colorful desactivado, RGB 34/157/255. No se hicieron escrituras físicas durante esta validación inicial de Studio. El controlador de Aplicar está comprobado con transporte simulado; su primera escritura real desde esta aplicación queda pendiente.

## Lectura del dispositivo real

Cierra el original y otras copias antes de iniciar Studio. El inicio solo lee y respalda. Puede ejecutarse sin interfaz:

```powershell
.\K585Studio.exe --read-only-diagnostics
```

Código de salida 0: dos instantáneas, cada una leída dos veces, coinciden. Informe privado en `%LOCALAPPDATA%\K585Studio\diagnostics.json`. Los respaldos privados están en la subcarpeta `Backups`. No son un volcado completo del firmware ni una restauración implementada.

La prueba real de escritura de parámetros desde Studio se documenta por separado de la comprobación de lectura. La animación en pantalla o el valor de configuración no sustituyen la observación física.
