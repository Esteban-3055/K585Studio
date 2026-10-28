Primera versión de K585 Studio para Windows x64.

- Estilo oscuro con acento azul, cuatro perfiles e imagen interactiva del K585.
- Perfil 1 bloqueado; lectura doble y respaldos privados de la configuración.
- Iluminación del perfil 2, incluida lluvia Serpenteo (modo 23) con color fijo, Colorful, brillo y velocidad.
- Borradores de asignación con borde naranja y biblioteca de macros local.
- Importación/exportación de proyectos y consulta de futuras versiones de GitHub.

**Alcance:** bindeos, macros y Hz del dispositivo no se escriben en esta versión. Solo la lluvia tiene sus parámetros habilitados para edición; los otros efectos conservan parámetros sin validar. La instalación de futuras versiones es manual. No incluye ni actualiza firmware; modo 23 requiere el firmware personalizado de lluvia previamente instalado. Modo 20 no está disponible en ese firmware.

**Validación:** 78 comprobaciones del protocolo/modelo y 18 de interfaz sin mostrar ventanas. La versión local hizo cuatro lecturas coincidentes del teclado real; no se probó todavía una escritura física desde Studio. No hay garantía de compatibilidad de modo 23 con otros K585 de fábrica.

Descarga el ZIP, extrae todo su contenido y ejecuta `K585Studio.exe`. No requiere instalar .NET. Cierra el programa original antes de conectar; activa físicamente M2 y vuelve a leer antes de aplicar iluminación. El programa nunca cambia el perfil activo.

La suma SHA-256 acompaña al ZIP. El paquete no tiene firma de editor. Los respaldos quedan en `%LOCALAPPDATA%\K585Studio\Backups`; no los publiques porque pueden contener macros personales.
