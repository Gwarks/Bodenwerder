# Bodenwerder

Bodenwerder ist eine Grafik-Engine, die auf F# und OpenTK basiert. Sie integriert IronPython für die Skriptsteuerung der Anwendungslogik und nutzt SkiaSharp für das Rendern von Texten.

## Features

* **Core (F#)**: Abstraktion von OpenGL 4.x (Shader, VBOs, VAOs, Texturen) und Fenstermanagement.
* **Scripting (Python)**: Die Hauptlogik befindet sich in Python-Skripten (z.B. `lt.cmdr.data/main.py`), die über eine Bridge auf die Engine zugreifen.
* **UI/Text**: Text-Rendering mittels SkiaSharp mit Unterstützung für Noto Fonts und Emojis.
*   **Input**: Unterstützung für Tastatur und mehrere Joysticks/Gamepads mit Flankenerkennung für Buttons.
*   **Fehlerbehandlung**: Detaillierte Python-Tracebacks (inkl. Zeilennummern) und aussagekräftige Fehlermeldungen bei falschen .NET-Methodenaufrufen.
* **Geometrie**: Enthält eine Half-Edge Datenstruktur (`Mesh.fs`) zur Verarbeitung und Triangulierung (Ear Clipping) von Polygon-Meshes.

## Coding Style

Das Projekt verwendet `Me` anstelle von `this` (F# Style und Python Konvention).

## Installation & Ausführung

1. Stelle sicher, dass das .NET 6.0/7.0+ SDK installiert ist.
2. Abhängigkeiten wiederherstellen: `dotnet restore`.
3. Die Engine startet standardmäßig die `lt.cmdr.data/main.py`.

## Python Interface API

Das Modul `Interface` (oft als `I` importiert) stellt Funktionen für Rendering, Ressourcenmanagement und Mathematik (OpenTK) bereit.

### Basisfunktionen

*   `getTextRenderer(size: float) -> TextRenderer`: Erstellt einen Renderer für Text in der angegebenen Größe.
*   `createSurfaceRGBA(width: int, height: int) -> SKSurface`: Erstellt eine SkiaSharp-Surface im RGBA-Format.
*   `getTextureQuad() -> TextureQuad`: Liefert ein Objekt zum Zeichnen von 2D-Texturen.
*   `createTexture(width: int, height: int, channels: int, texels: bytes) -> Texture`: Erstellt eine Textur aus Byte-Daten.
*   `createShaderProgram(shaders: list) -> ShaderProgram`: Erstellt ein Shader-Programm. Erwartet eine Liste von Tupeln `(SourceCode, ShaderType)`.
*   `createVertexArray(attributes: list|dict, vertices: bytes, primitive_type: PrimitiveType) -> VertexArray`: Erstellt ein Vertex-Array (VAO) aus Attribut-Definitionen und Vertex-Daten.
*   `CSGunion(meshA: HalfEdgeMesh, meshB: HalfEdgeMesh, interpolate_func: function) -> HalfEdgeMesh`: Berechnet die Vereinigung zweier Meshes.
*   `CSGintersection(meshA: HalfEdgeMesh, meshB: HalfEdgeMesh, interpolate_func: function) -> HalfEdgeMesh`: Berechnet die Schnittmenge zweier Meshes.
*   `CSGsubtraction(meshA: HalfEdgeMesh, meshB: HalfEdgeMesh, interpolate_func: function) -> HalfEdgeMesh`: Subtrahiert Mesh B von Mesh A.
*   `createMeshFromSDF(min: Vector3, max: Vector3, res: Vector3i, sdf: function) -> HalfEdgeMesh`: Erzeugt ein Mesh aus einer Signed Distance Function (SDF).
*   `createCubeMesh(center: Vector3, size: float, data: object) -> HalfEdgeMesh`: Erzeugt einen Würfel als Half-Edge Mesh.
*   `getConfigPath() -> string`: Liefert den Pfad zum AppData-Verzeichnis.
*   `setBackgroundColor(color: Color4)`: Setzt die Clear-Color des Fensters.
*   `setDepthTest(enable: bool)`: Aktiviert oder deaktiviert den OpenGL Depth-Test (Z-Buffer).
*   `getJoysticks() -> list`: Liefert eine Liste aller aktiven Joysticks als Tuples `(id, name, axes, buttons)`.

### Debugging & Fehlerbehandlung

Die Engine bietet spezialisierte Fehlermeldungen für die Interaktion zwischen Python und .NET:

*   **Syntax-Fehler**: Beim Laden von Skripten (z.B. `main.py`) werden Syntaxfehler mit Pfad, Zeile und Code-Ausschnitt direkt in der Konsole ausgegeben.
*   **Runtime-Exceptions**: Tritt ein Fehler in `onRender` oder einem Input-Callback auf, wird ein vollständiger Python-Traceback angezeigt, bevor die Engine sicher schließt.
*   **Overload-Fehler**: Wenn eine .NET-Funktion (wie `I.Color4(...)`) mit den falschen Parametern aufgerufen wird, listet die Engine:
    1. Den Namen der Funktion/des Typs.
    2. Die Typen der tatsächlich übergebenen Argumente.
    3. Eine Liste aller verfügbaren .NET-Überladungen (Kandidaten) inklusive Parameternamen und Typen.

Beispiel: `No matching overload found for 'Color4'. Provided: (Single, Single, Single)`.


### Typen und Konstanten

*   `Vector2i`: OpenTK Vektor (Integer).
*   `Vector2`: OpenTK Vektor (Float).
*   `Vector3`: OpenTK Vektor (Float).
*   `Vector3i`: OpenTK Vektor (Integer 3D).
*   `Color4`: OpenTK Farbe.
*   `Matrix4`: OpenTK 4x4 Matrix (für Transformationen, `LookAt`, `CreatePerspectiveFieldOfView`).
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
*   `CalculateVolume() -> float`: Berechnet das Volumen des geschlossenen Meshes.

**VertexArray**
*   `draw()`: Führt den Draw-Call aus.

## Skript-Schnittstelle (Callbacks)

Die Engine kommuniziert mit dem Python-Skript über Callbacks, die beim Start (üblicherweise in der `main.py`) registriert werden müssen.

### Registrierung

*   `setRenderer(func)`: Registriert die Funktion für den Render-Loop. Sie empfängt die aktuelle Fenstergröße (`Vector2i`).
*   `setInputHandler(obj)`: Registriert ein Objekt für Eingabe-Events. Die Engine sucht auf diesem Objekt nach spezifischen Methoden (siehe unten).
*   `closeEngine()`: Beendet die Engine und schließt das Fenster.

### Beispiel `main.py`

```python
import Interface as I

def onRender(size):
    # Haupt-Render-Loop. size.X und size.Y enthalten die Dimensionen.
    pass

class MyHandler:
    def onKeyDown(Me, key):
        if key == I.Keys['Escape']:
            I.closeEngine()

    def onKeyUp(Me, key):
        pass

    def onJoystickButtonDown(Me, id, name, button):
        pass

    def onJoystickButtonUp(Me, id, name, button):
        pass

    def onJoystickAxis(Me, id, name, axis, value):
        pass

# Callbacks an die Engine binden 
I.setRenderer(onRender)
I.setInputHandler(MyHandler())
```