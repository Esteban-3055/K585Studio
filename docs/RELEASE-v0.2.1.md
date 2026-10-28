K585 Studio v0.2.1 para Windows x64.

## Cambios

- Lectura de macros separada de la biblioteca local. Leer el teclado ya no incorpora macros automáticamente ni depende de nombres coincidentes. Usa **Copiar del teclado** para trabajar sobre una copia; se conservan los borradores existentes y se resuelven los nombres repetidos.
- Vista previa de compatibilidad y espacio: indica los bytes necesarios, la reutilización de un registro existente y los motivos por los que no se puede añadir. Comprueba capacidad, directorio, referencias sin definir y pulsaciones sin liberar, sin escribir. El dispositivo se vuelve a leer y comprobar al aplicar.
- Aplicar asignación queda deshabilitado si la macro seleccionada no se puede guardar.
- Controles para descartar los borradores de iluminación y frecuencia por separado, sin escribir ni descartar asignaciones.
- Importación JSON con validación de campos nulos, referencias de macros, acciones desconocidas y asignaciones reservadas. Límite de 2 MiB tanto al guardar como al importar; un guardado rechazado conserva el archivo anterior.
- El editor de macros ofrece 1–255 repeticiones para nuevas macros, de acuerdo con el hardware. Los proyectos anteriores conservan sus valores para corregirlos sin perderlos.

## Validación y límites

Compilación sin avisos, 438 comprobaciones de modelo/protocolo/actualizador/proyectos y 42 de interfaz en segundo plano. Las pruebas no abren HID, no modifican proyectos personales ni escriben en el teclado. El instalador pasa instalación, actualización y desinstalación en el runner antes de publicar.

Se conservan las protecciones de v0.2.0: perfil 1 bloqueado, Fn y botones M1–M4/REC reservados, sin reset ni instalación de firmware. Los registros de macros anteriores no se mueven ni eliminan. La reproducción de macros sigue validada en emulación nativa, pendiente de observación física.

Puedes actualizar desde **Actualizaciones** en la aplicación o instalar **K585Studio-0.2.1-Setup-win-x64.exe**. Conserva proyectos y respaldos de v0.2.0. También se ofrece el ZIP portable.
