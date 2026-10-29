# Validación de v0.3.1

v0.3.1: se corrige la destrucción de `ContextMenuStrip` dentro de `Closed`, observada en el evento .NET 1026 de Windows. La regresión invisible reproduce el fallo de obtener un handle después de ese patrón y comprueba que el menú nuevo permanece válido, se reutiliza y permite navegar al actualizador. También comprueba HTTP 403, JSON inválido y un reintento correcto sin abrir el navegador o USB.

Resultado local: Release sin avisos, 546 comprobaciones normales, 571 al añadir el EXE original y 571 con el EXE Serpenteo; ambas ejecuciones locales incluyen de nuevo las comprobaciones normales. El portable v0.3.1 supera 84 comprobaciones de interfaz. Las capturas se dibujan sin mostrar ventanas ni controlar el escritorio. No se abrió HID ni se ejecutó un actualizador de firmware en estas pruebas.

El gestor de firmware tiene comprobaciones locales adicionales con el EXE original y el Serpenteo del propietario, **sin ejecutarlos**: huellas del ejecutable y payload, reconstrucción exacta del código arcoíris desde ambas bases, conservación de los cuatro perfiles y todos los bloques, límites de diferencias, cancelación, revisión incorrecta, cambios posteriores a la preparación, archivo alterado, respaldo eliminado y reanudación del registro pendiente. Estos binarios no se incluyen ni descargan en CI. Las pruebas normales de CI y las pruebas con el archivo del propietario se contabilizan por separado.

La incorporación de un gestor no prueba que una grabación física sea segura. Esta versión de Studio no se valida flasheando el teclado; véase [FIRMWARE.md](FIRMWARE.md). El candidato arcoíris continúa marcado como experimental.

La prueba automática y la observación física son evidencias diferentes. Las capturas de la interfaz se generan en segundo plano con el mismo dibujado del producto, sin mostrar ventanas ni controlar otras aplicaciones.

Resultado local de v0.3.0: compilación Release sin avisos, 542 comprobaciones de modelo/protocolo/actualizador/proyectos y 61 de interfaz aprobadas. Esta revisión probó por USB exclusivamente el cambio de perfil y su restauración; las pruebas físicas de iluminación, bindeo y frecuencia descritas más abajo corresponden a v0.2.0. El instalador se prueba por separado en el runner de GitHub antes de publicar.

## Compilar y probar

```powershell
dotnet build K585Studio.sln -c Release -p:NuGetAudit=false
dotnet run --project tests/K585Studio.Checks -c Release --no-build
.\K585Studio.exe --ui-self-test C:\Temp\K585Studio-UI
```

Las pruebas del modelo cubren límites y cabeceras USB, edición aislada de los cuatro perfiles, Fn reservada, selección de perfil, cambios externos, respaldos fallidos, timeout sin reintento, comparación integral, reasignaciones, frecuencia por perfil, macros nuevas/reutilizadas, límites y conservación de registros anteriores. El actualizador se prueba con respuestas HTTP simuladas: URL, versión, tamaño, digest, descarga íntegra o incompleta, caché alterada, publicación modificada y errores de red.

La interfaz se comprueba a 1040×660, 1672×941 y 1920×1080: 42 áreas de teclas, perfil 1 editable, borradores sin escritura, indicadores de cambios, páginas y editores. Un transporte en memoria prueba el mismo manejador del selector de perfiles: confirmación, borradores conservados, botones Aplicar y fallo incierto sin reintento. La prueba de interfaz no abre HID ni carga/guarda proyectos personales.

La v0.2.1 añade casos de biblioteca local separada, copia sin colisiones, edición que conserva los registros del dispositivo, reutilización con tabla llena, falta de espacio, directorio OEM ocupado y referencias sin definir en perfil 1. Comprueba además JSON incompleto, asignaciones inválidas, límites de archivo, conservación del archivo anterior al fallar el guardado y descarte de borradores sin HID. Las capturas con macros conectadas utilizan una instantánea ficticia aislada.

## Evidencia nativa, fuera del repositorio

v0.3.0: 188 paquetes generados por las funciones reales C# se ejecutaron en el emulador del firmware: selección de perfil, ocho bytes de iluminación, 41 bindeos y cuatro frecuencias para cada uno de los cuatro perfiles. Se verificaron las direcciones, datos y longitudes exactas y que ninguna otra región persistente cambiara.

Con el firmware suministrado por el propietario, se emularon 135 escrituras de frecuencia y registros de teclas en los perfiles 2–4. Cada una llegó a la dirección y longitud previstas y no modificó otras regiones. Se comprobaron las 42 correspondencias contra la tabla de fábrica.

Una macro codificada por el modelo C# se ejecutó en el motor Thumb real del firmware: W pulsada en 0 ms, liberada en 150 ms, segundo ciclo en 170 ms y liberación en 320 ms. También se verificaron las cuatro etapas nativas de su publicación USB: datos, puntero, cabecera y asignación. Esto es emulación, no ejecución física en el teclado.

## Prueba física realizada

v0.3.0: se respaldó el teclado en su perfil 1 activo, se activaron y leyeron 2, 3 y 4 y se restauró 1. Cada paso conservó los ajustes de todos los perfiles, macros y demás bloques. La lectura final completa coincidió con el respaldo inicial. La prueba no escribió configuraciones de los perfiles; el desbloqueo y sus escrituras se comprobaron en modelo y emulación nativa.

La v0.2 hizo dos lecturas por instantánea, respaldó la configuración y cambió/restauró, por separado:

1. Brillo del perfil 2.
2. Asignación de G5 del perfil 2.
3. Frecuencia USB almacenada del perfil 2.

La lectura final completa coincidió con el respaldo inicial: todos los perfiles, todos los bindeos, macros, RGB y configuración general. No se cambió el firmware. El perfil 1 permaneció intacto.

No se midió la frecuencia física de eventos USB ni la latencia. La nueva asignación se comprobó por escritura/lectura; no se simuló una pulsación física. La escritura y reproducción de una macro desde una tecla real quedan pendientes de observación por el propietario. Los perfiles 3–4 se validaron en el modelo/emulador, sin modificar sus configuraciones físicas en estas pruebas.

## Diagnósticos

`--read-only-diagnostics` abre exclusivamente la colección vendor del K585, toma cuatro lecturas completas y guarda el resultado en `%LOCALAPPDATA%\K585Studio\diagnostics.json`.

`--hardware-self-test` es una prueba de desarrollo que **escribe temporalmente en perfil 2**: brillo, G5 y frecuencia, y restaura cada valor después de verificarlo. No es parte del arranque ni del instalador. Requiere perfil 2 activo y programas competidores cerrados. Si una escritura queda incierta se detiene sin reintentar. No debe ejecutarse como comprobación de rutina.

`--profile-self-test` es una prueba de desarrollo que cambia el perfil activo para comprobar los cuatro y vuelve al original si todas las verificaciones terminan correctamente. Respalda y compara todos los bloques; no aplica configuraciones ni reintenta una escritura incierta. Guarda un diario privado en `%LOCALAPPDATA%\K585Studio\profile-checks-v03.json`.

Los respaldos y diarios de la prueba física son privados y no se suben a GitHub.
