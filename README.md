# Bodenwerder

Bodenwerder ist eine Grafik-Engine, die auf F# und OpenTK basiert. Sie integriert IronPython für die Skriptsteuerung der Anwendungslogik und nutzt SkiaSharp für das Rendern von Texten.

## Features

* **Core (F#)**: Abstraktion von OpenGL 4.x (Shader, VBOs, VAOs, Texturen) und Fenstermanagement.
* **Scripting (Python)**: Die Hauptlogik befindet sich in Python-Skripten (z.B. `lt.cmdr.data/main.py`), die über eine Bridge auf die Engine zugreifen.
* **UI/Text**: Text-Rendering mittels SkiaSharp mit Unterstützung für Noto Fonts und Emojis.
* **Geometrie**: Enthält eine Half-Edge Datenstruktur (`Mesh.fs`) zur Verarbeitung und Triangulierung (Ear Clipping) von Polygon-Meshes.

## Coding Style

Me project use Me instead of this.

## Python Interface API

Das Modul `Interface` (in Python oft als `I` importiert) stellt Funktionen für Rendering und Ressourcenmanagement bereit.

### Basisfunktionen

*   `getTextRenderer(size: float) -> TextRenderer`: Erstellt einen Renderer für Text in der angegebenen Größe.
*   `getTextureQuad() -> TextureQuad`: Liefert ein Objekt zum Zeichnen von 2D-Texturen.
*   `createTexture(width: int, height: int, channels: int, texels: bytes) -> Texture`: Erstellt eine Textur aus Byte-Daten.
*   `createShaderProgram(shaders: list) -> ShaderProgram`: Erstellt ein Shader-Programm. Erwartet eine Liste von Tupeln `(SourceCode, ShaderType)`.
*   `createVertexArray(attributes: list|dict, vertices: bytes, primitive_type: PrimitiveType) -> VertexArray`: Erstellt ein Vertex-Array (VAO) aus Attribut-Definitionen und Vertex-Daten.
*   `createMeshFromSDF(min: Vector3, max: Vector3, res: Vector3i, sdf: function) -> HalfEdgeMesh`: Erzeugt ein Mesh aus einer Signed Distance Function (SDF).
*   `getConfigPath() -> string`: Liefert den Pfad zum AppData-Verzeichnis.
*   `setBackgroundColor(color: Color4)`: Setzt die Clear-Color des Fensters.
*   `setDepthTest(enable: bool)`: Aktiviert oder deaktiviert den OpenGL Depth-Test (Z-Buffer).

### Typen und Konstanten

*   `Vector2i`: OpenTK Vektor (Integer).
*   `Vector3`: OpenTK Vektor (Float).
*   `Vector3i`: OpenTK Vektor (Integer 3D).
*   `Color4`: OpenTK Farbe.
*   `ShaderType`: Mapping von Shadertypen (z.B. `I.ShaderType['VertexShader']`).
*   `PrimitiveType`: Mapping von Primitiven (z.B. `I.PrimitiveType['TriangleFan']`).
*   `Keys`: Mapping von Tastaturtasten (z.B. `I.Keys['Space']`).

### Klassen

**TextRenderer**
*   `renderText(text: string) -> Texture`: Rendert Text in eine Textur.

**TextureQuad**
*   `render(texture: Texture, pos: Vector2i, window_size: Vector2i)`: Zeichnet eine Textur an einer Bildschirmposition.

**ShaderProgram**
*   `activate()`: Aktiviert den Shader.
*   `SetUniform(name: string, value)`: Setzt Uniforms (unterstützt `int`, `float`, `Vector2/3/4`, `Matrix4`).
*   `getActiveAttributes() -> dict`: Liefert aktive Attribute für `createVertexArray`.

**HalfEdgeMesh**
*   `Triangulate() -> sequence`: Zerlegt das Mesh in Dreiecke. Liefert eine Liste von (x, y, z, data).
*   `Map(mapping_func: function) -> HalfEdgeMesh`: Transformiert die Daten des Meshes unter Beibehaltung der Struktur.

**VertexArray**
*   `draw()`: Führt den Draw-Call aus.

## Skript-Schnittstelle (Callbacks)

Die Engine erwartet, dass die `main.py` nach ihrer Ausführung folgende Funktionen im globalen Namensraum bereitstellt, um auf Ereignisse zu reagieren:

*   `onRender(size: Vector2i)`: Wird in jedem Frame aufgerufen. Hier erfolgt das Zeichnen der Objekte.
*   `onKeyDown(key: Keys)`: Wird aufgerufen, wenn eine Taste gedrückt wird.
*   `onKeyUp(key: Keys)`: Wird aufgerufen, wenn eine Taste losgelassen wird.
