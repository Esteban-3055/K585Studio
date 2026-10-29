# K585 Studio

Aplicación independiente para Redragon K585 RGB (USB 0C45:5004), con el estilo de VictusFanControl. Windows 10/11 x64.

**[Descargar el instalador EXE](https://github.com/Esteban-3055/K585Studio/releases/latest)** · [Funciones originales revisadas](docs/OEM-AUDIT.md) · [Protocolo v0.3](docs/PROTOCOL-v0.3.md)

## Funciones

- Cuatro perfiles editables con imagen interactiva del teclado y editor lateral. Seleccionarlos en Studio los activa en el dispositivo, con respaldo y lectura de verificación.
- Reasignación de teclas, combinaciones, modificadores, ratón, multimedia y funciones en perfiles 1–4.
- Biblioteca, editor y grabador de macros; escritura de macros nuevas sin sobrescribir las existentes. Captura solo dentro de la zona de grabación, sin hooks globales.
- Biblioteca local separada del teclado: copia explícita, nombres sin colisiones y comprobación previa de memoria y compatibilidad. Las lecturas conservan las ediciones locales.
- Iluminación y lluvia Serpenteo, color fijo/Colorful, brillo y velocidad con sus cinco niveles reales.
- Frecuencia USB de 125/250/500/1000 Hz por perfil.
- Dos lecturas coincidentes y respaldo completo antes de escribir; comparación integral después de aplicar.
- Proyectos persistentes e importación/exportación JSON, incluidos los cambios pendientes.
- Instalación por usuario y actualización desde GitHub con descarga, tamaño/SHA-256 y reconfirmación de la publicación antes de ejecutar el instalador.
- Gestor de firmware con EXE local reconocido, respaldo nuevo de los cuatro perfiles y preparación que conserva la configuración. Incluye una modificación opcional de lluvia arcoíris, experimental y desactivada por defecto.

## Uso

Cierra el programa original y las copias anteriores. Studio lee el teclado al iniciar y muestra el perfil que está activo. Seleccionar **Perfil 1–4** en la barra lateral activa ese perfil en el teclado y abre su editor después de verificarlo. El selector cambia únicamente el perfil activo y conserva todos los borradores. Al volver a abrir Studio se lee de nuevo el perfil activo del dispositivo, sin imponer otro automáticamente.

Mover un slider o preparar una asignación modifica únicamente el proyecto; cada botón **Aplicar** escribe y verifica su ajuste en el perfil seleccionado. Si cambias de perfil con los botones físicos M1–M4 mientras Studio está abierto, pulsa **Conectar / leer** para actualizar la vista. Si una escritura queda sin verificar, la sesión se bloquea hasta volver a conectar explícitamente.

El borde naranja indica una asignación distinta de fábrica o pendiente de aplicar. Fn y los botones M1–M4/REC conservan su función reservada. El perfil 1 admite las mismas ediciones que los perfiles 2–4 desde v0.3.0; su desbloqueo no restablece ni cambia su configuración existente.

En **Macros**, usa **Copiar del teclado** para seleccionar una macro almacenada y editar una copia local. El panel indica si puede reutilizarse o necesita memoria nueva; también explica los límites antes de aplicar. El botón Aplicar asignación permanece deshabilitado para una macro que no puede guardarse. La comprobación definitiva se repite contra una lectura nueva del dispositivo.

**Descartar iluminación** y **Descartar** junto a la frecuencia eliminan sus borradores locales. Conservan los ajustes del teclado y las asignaciones pendientes de otras secciones. Los proyectos JSON conservan el formato de v0.2.0 y tienen un límite de 2 MiB.

El modo 23 requiere nuestro firmware de lluvia y funciona únicamente en el perfil 2. El modo 20 de pintura por tecla fue retirado de ese firmware y no se ofrece. No se convierten los archivos de configuración binarios del programa OEM.

En **Actualizaciones → Firmware del teclado**, carga tu EXE local, elige si quieres preparar la lluvia arcoíris experimental y pulsa **Preparar y respaldar**. Studio verifica el ejecutable y el firmware por sus huellas, comprueba la revisión USB e incorpora al paquete la configuración recién leída del dispositivo. Los borradores pendientes no se aplican. Después, **Abrir actualizador** vuelve a comprobar todo, libera USB y abre la herramienta original: la grabación comienza cuando pulsas **Start update** en esa ventana. No se pulsa automáticamente. Consulta [FIRMWARE.md](docs/FIRMWARE.md) para compatibilidad y límites.

Al terminar, **Verificar ajustes** compara la configuración con el respaldo. La aplicación no puede leer el código instalado ni confirmar por sí sola el resultado físico de la actualización. El seguimiento pendiente sobrevive a cerrar y abrir Studio; los respaldos quedan en `%LOCALAPPDATA%\K585Studio\Firmware`.

Los proyectos y respaldos privados quedan en `%LOCALAPPDATA%\K585Studio`. La desinstalación los conserva. Las macros anteriores del dispositivo no se mueven ni compactan; una tabla OEM sin espacio libre en su directorio se conserva. Consulta los límites y la evidencia en [VALIDATION.md](docs/VALIDATION.md).

## Desarrollo

SDK .NET 8 y Windows:

```powershell
dotnet build K585Studio.sln -c Release
dotnet run --project tests/K585Studio.Checks -c Release --no-build
dotnet run --project src/K585Studio.App -c Release --no-build
```

Portable:

```powershell
dotnet publish src/K585Studio.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/K585Studio-win-x64
```

El workflow compila, ejecuta las pruebas, crea el instalador con Inno Setup y prueba instalación, actualización y desinstalación en el runner de GitHub. Solo publica una versión cuando todas esas etapas pasan. Una release existente nunca se sobrescribe.

No hay afiliación con Redragon. La imagen se usa tal como la proporcionó el propietario; no se afirma autoría sobre la marca o imagen. No se redistribuyen el programa OEM, imágenes completas de firmware ni configuraciones personales. La modificación de lluvia se distribuye como diferencias de código y requiere el EXE local del propietario.
