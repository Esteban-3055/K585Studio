K585 Studio v0.3.0 para Windows x64.

## Cambios

- Seleccionar Perfil 1–4 en Studio activa ese perfil en el teclado mediante un único byte de configuración. El selector queda en el perfil confirmado por la lectura USB.
- Al abrir la aplicación o volver a leer, el editor muestra el perfil activo del teclado.
- Perfil 1 desbloqueado: iluminación, asignación de teclas y macros, y frecuencia USB con los mismos controles que los demás perfiles. Desbloquearlo conserva sus valores actuales.
- Los borradores permanecen separados por perfil y se conservan al cambiar de uno a otro. Se envían únicamente con los botones Aplicar correspondientes.
- Cada cambio de perfil se respalda y compara por completo. Seleccionar un perfil que ya está activo no escribe. Un resultado incierto bloquea la sesión y requiere Conectar / leer, sin reintentos automáticos.

## Validación

542 comprobaciones de lógica y 61 de interfaz en segundo plano. 188 paquetes C# comprobados en el firmware emulado para los cuatro perfiles. La prueba USB activó 2, 3 y 4 y volvió al perfil 1 original; todos los ajustes, bindeos, macros y bloques persistentes coincidieron con el respaldo inicial al finalizar.

La prueba física de esta revisión cambió exclusivamente el perfil activo. La edición de ajustes del perfil 1 se verificó en modelo y emulación nativa. Fn, M1–M4 y REC mantienen su función reservada. No se instala ni modifica firmware.

Descarga **K585Studio-0.3.0-Setup-win-x64.exe** o actualiza desde **Actualizaciones** en la aplicación. Se conservan proyectos y respaldos. También se ofrece el ZIP portable. El instalador pasa las pruebas del runner, incluida la actualización desde la v0.2.0 publicada, antes de distribuirse.
