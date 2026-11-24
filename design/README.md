# Identidad monocroma

Referencia visual aportada por el usuario: VictusFanControl / Night Light Control.
Negro puro, texto blanco, grises neutros, paneles con esquinas recortadas,
trama triangular discreta y selección con luz blanca suave. El naranja identifica
las asignaciones modificadas; el selector RGB conserva el color elegido.

El icono integrado en `src/K585Studio.App/Assets/app-icon.png` es la adaptación del icono cian elegido por el usuario,
generada con la herramienta integrada de imágenes y fondo transparente. Los
originales proporcionados por el usuario se conservan fuera del repositorio.

Prompt utilizado:

> Use case: style-transfer. Edit target: image 1, the compact keyboard application icon. Style reference only: image 2, the VictusFanControl black and white interface. Restyle the exact compact keypad silhouette, same perspective, same key arrangement and wrist rest from image 1 into a crisp monochrome black/silver/white icon matching image 2. Replace all cyan/blue glowing key outlines and accents with white light and a restrained soft white glow. Dark graphite/black keycaps and housing, fine silver bevels, angular cut corners, clear luminous white borders, legible at small Windows icon sizes. Preserve the recognizable original keypad shape and all keys; no V letter, no text, no badge, no added objects. Center entire keyboard with even transparent margin inside a square. Truly transparent background, preserve alpha. Clean polished app icon, high contrast, not overly glowy or blurry.

Regenerar los tamaños Windows sin alterar el diseño:

```powershell
./scripts/Build-AppIcon.ps1 -Source ruta/al/icono-original.png
```

El ICO contiene imágenes PNG RGBA de 16, 20, 24, 32, 40, 48, 64, 96, 128 y 256 px.
