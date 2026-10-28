# Validación de v0.2

La prueba automática y la observación física son evidencias diferentes. Las capturas de la interfaz se generan en segundo plano con el mismo dibujado del producto, sin mostrar ventanas ni controlar otras aplicaciones.

Resultado local de esta versión: compilación Release sin avisos, 399 comprobaciones de modelo/protocolo/actualizador y 30 de interfaz aprobadas. La prueba USB de escritura y restauración también terminó correctamente. El instalador se prueba por separado en el runner de GitHub antes de publicar.

## Compilar y probar

```powershell
dotnet build K585Studio.sln -c Release -p:NuGetAudit=false
dotnet run --project tests/K585Studio.Checks -c Release --no-build
.\K585Studio.exe --ui-self-test C:\Temp\K585Studio-UI
```

Las pruebas del modelo cubren límites y cabeceras USB, perfil 1 y Fn protegidos, cambios externos, respaldos fallidos, timeout sin reintento, comparación integral, reasignaciones, frecuencia por perfil, macros nuevas/reutilizadas, límites y conservación de registros anteriores. El actualizador se prueba con respuestas HTTP simuladas: URL, versión, tamaño, digest, descarga íntegra o incompleta, caché alterada, publicación modificada y errores de red.

La interfaz se comprueba a 1040×660, 1672×941 y 1920×1080: 42 áreas de teclas, bloqueo del perfil 1, borradores sin escritura, indicadores de cambios, páginas y editores. La prueba no abre HID ni carga/guarda proyectos personales.

## Evidencia nativa, fuera del repositorio

Con el firmware suministrado por el propietario, se emularon 135 escrituras de frecuencia y registros de teclas en los perfiles 2–4. Cada una llegó a la dirección y longitud previstas y no modificó otras regiones. Se comprobaron las 42 correspondencias contra la tabla de fábrica.

Una macro codificada por el modelo C# se ejecutó en el motor Thumb real del firmware: W pulsada en 0 ms, liberada en 150 ms, segundo ciclo en 170 ms y liberación en 320 ms. También se verificaron las cuatro etapas nativas de su publicación USB: datos, puntero, cabecera y asignación. Esto es emulación, no ejecución física en el teclado.

## Prueba física realizada

La v0.2 hizo dos lecturas por instantánea, respaldó la configuración y cambió/restauró, por separado:

1. Brillo del perfil 2.
2. Asignación de G5 del perfil 2.
3. Frecuencia USB almacenada del perfil 2.

La lectura final completa coincidió con el respaldo inicial: todos los perfiles, todos los bindeos, macros, RGB y configuración general. No se cambió el firmware. El perfil 1 permaneció intacto.

No se midió la frecuencia física de eventos USB ni la latencia. La nueva asignación se comprobó por escritura/lectura; no se simuló una pulsación física. La escritura y reproducción de una macro desde una tecla real quedan pendientes de observación por el propietario. Los perfiles 3–4 se validaron en el modelo/emulador, sin modificar sus configuraciones físicas en estas pruebas.

## Diagnósticos

`--read-only-diagnostics` abre exclusivamente la colección vendor del K585, toma cuatro lecturas completas y guarda el resultado en `%LOCALAPPDATA%\K585Studio\diagnostics.json`.

`--hardware-self-test` es una prueba de desarrollo que **escribe temporalmente en perfil 2**: brillo, G5 y frecuencia, y restaura cada valor después de verificarlo. No es parte del arranque ni del instalador. Requiere perfil 2 activo y programas competidores cerrados. Si una escritura queda incierta se detiene sin reintentar. No debe ejecutarse como comprobación de rutina.

Los respaldos y diarios de la prueba física son privados y no se suben a GitHub.
