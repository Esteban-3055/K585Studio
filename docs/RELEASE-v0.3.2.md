# K585 Studio v0.3.2

El actualizador de firmware viene integrado: al abrir **Actualizaciones → Firmware del teclado**, Studio carga y verifica en memoria el EXE original estudiado. Ya no hace falta buscarlo ni descargarlo aparte. **Usar integrado** permite volver a esa copia y **Elegir otro EXE…** mantiene la importación opcional de archivos reconocidos.

La copia incluida es el original de fábrica, verificado por SHA-256 completo; no contiene configuraciones del propietario. El EXE ejecutable se genera solamente después de respaldar e incorporar los ajustes actuales del teclado. La herramienta se abre a petición del usuario y la grabación comienza al pulsar Start update. Instalar o abrir Studio no instala firmware.

La lluvia arcoíris permanece opcional, experimental y desactivada por defecto. Se conservan las comprobaciones de compatibilidad, lectura nueva antes de abrir, respaldo, liberación de USB, seguimiento pendiente y comparación posterior de ajustes. No se realizó una actualización física para validar esta versión.

Las pruebas normales ahora también verifican el recurso incorporado y la preparación completa con datos ficticios, sin rutas del escritorio ni teclado conectado. El workflow comprueba el portable y la instalación, actualización y desinstalación antes de publicar.
