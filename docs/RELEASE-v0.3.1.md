# K585 Studio v0.3.1

Corrige el cierre al pulsar Actualizaciones u otro control después de cerrar un menú de efectos o macros. Los menús permanecen bajo la propiedad de la ventana mientras Windows termina de procesar el clic. Los errores de GitHub y respuestas inválidas se muestran en la aplicación y permiten reintentar.

Añade **Actualizaciones → Firmware del teclado**: carga de un EXE local reconocido, preparación con respaldo nuevo e incorporación de todos los ajustes actuales, verificación antes de abrir y liberación de USB para la herramienta original. La grabación se inicia manualmente mediante Start update. El seguimiento y respaldo sobreviven a reiniciar Studio; la comparación posterior verifica los ajustes y no afirma leer el código instalado.

Incluye una modificación opcional de **lluvia arcoíris en la estela**, marcada como experimental y desactivada por defecto. Requiere el EXE local; no se incluyen el programa OEM, imágenes completas de firmware ni datos personales. Esta variante está validada en emulación, pendiente de prueba física. El gestor exige la revisión estudiada y no garantiza recuperación ante un fallo de programación.

Se conserva la edición de los cuatro perfiles y los proyectos existentes. La lluvia del firmware estudiado funciona en el perfil 2. La aplicación no restablece perfiles ni instala firmware durante el arranque o al instalar Studio. El instalador espera al cierre de Studio y no utiliza el cierre automático de aplicaciones de Windows.

Validación: compilación Release; pruebas de protocolo, modelo y actualizaciones; regresión de menús y navegación; revisión invisible de interfaz; pruebas locales de preparación con EXE original y Serpenteo sin ejecutarlos. El workflow valida además el EXE portable y la instalación, actualización y desinstalación antes de publicar. No se grabó el teclado para validar esta versión de Studio.
