# Bodenwerder

Bodenwerder ist eine **3D-Grafik-Engine**, die auf **F#** und **OpenTK (OpenGL 4.x)** basiert. Sie integriert **IronPython** für die Skriptsteuerung der Anwendungslogik und nutzt **SkiaSharp** für 2D-Rendering (Text, Texturen, Oberflächen).

## Features

* **Core (F#)**: Vollständige Abstraktion von OpenGL 4.x (Shader, Texturen, VBOs, VAOs, Framebuffer) und Fenstermanagement mit GLFW
* **Scripting (Python)**: Die Hauptlogik befindet sich in Python-Skripten (z.B. `lt.cmdr.data/main.py`), die über die `Interface`-Bridge auf die Engine zugreifen
* **2D Rendering**: Text-Rendering und 2D-Zeichnungsoperationen mittels SkiaSharp mit Unterstützung für Noto Fonts und Emojis
* **3D Rendering**: Vollständige 3D-Rendering-Pipeline mit Mesh-Operationen, CSG und Offscreen-Rendering
* **Input**: Unterstützung für Tastatur und mehrere Joysticks/Gamepads mit Flankenerkennung für Buttons
* **Fehlerbehandlung**: Detaillierte Python-Tracebacks (inkl. Zeilennummern) und aussagekräftige Fehlermeldungen bei falschen .NET-Methodenaufrufen
* **Geometrie**: Half-Edge Datenstruktur (`Mesh.fs`) zur Verarbeitung und Triangulierung (Ear Clipping) von Polygon-Meshes
* **CSG-Operationen**: Constructive Solid Geometry (Union, Intersection, Subtraction) für Mesh-Operationen
* **SDF-Unterstützung**: Erzeugung von Meshes aus Signed Distance Functions
* **Offscreen-Rendering**: Render Targets für Textur-Generierung und Post-Processing

## Coding Style

Das Projekt verwendet `Me` anstelle von `this` (F# Style und Python Konvention).

## Installation & Ausführung

### Voraussetzungen

* **.NET 10.0 SDK** (erforderlich)
* Optional: Joystick/Gamepad für Input-Unterstützung

### Schritte

1. Projekt wiederherstellen und Abhängigkeiten laden:
   ```bash
   dotnet restore Bodenwerder.fsproj
   ```

2. Projekt kompilieren:
   ```bash
   dotnet build Bodenwerder.fsproj
   ```

3. Anwendung starten:
   ```bash
   dotnet run --project Bodenwerder.fsproj
   ```
   Die Engine startet automatisch die `lt.cmdr.data/main.py` als Entry Point.

## Python Interface API

Das Modul `Interface` (oft als `I` importiert) stellt Funktionen für Rendering, Ressourcenmanagement und Mathematik (OpenTK) bereit.

### Basisfunktionen

#### Rendering & Ressourcen

*   `getTextRenderer(size: float) -> TextRenderer`: Erstellt einen Renderer für Text in der angegebenen Größe
*   `createSurfaceRGBA(width: int, height: int) -> SKSurface`: Erstellt eine SkiaSharp-Surface im RGBA-Format
*   `image2texture(image: SKImage) -> Texture`: Konvertiert ein SkiaSharp-Bild direkt in eine OpenGL-Textur
*   `getTextureQuad() -> TextureQuad`: Liefert ein Objekt zum Zeichnen von 2D-Texturen auf dem Bildschirm
*   `createTexture(width: int, height: int, channels: int, texels: bytes) -> Texture`: Erstellt eine Textur aus Rohdaten (Byte-Array)
*   `createShaderProgram(shaders: list) -> ShaderProgram`: Erstellt ein Shader-Programm. Erwartet eine Liste von Tupeln `(SourceCode, ShaderType)`
*   `createVertexArray(attributes: list|dict, vertices: bytes, primitive_type: PrimitiveType) -> VertexArray`: Erstellt ein Vertex-Array (VAO) aus Attribut-Definitionen und Vertex-Daten
*   `createOffscreenRenderTarget(width: int, height: int) -> OffscreenRenderTarget`: Erstellt ein Render-Target für Offscreen-Rendering (Framebuffer mit Textur-Anbindung)

#### Mesh-Operationen & Geometrie

*   `createMeshFromSDF(min: Vector3, max: Vector3, res: Vector3i, sdf: function) -> HalfEdgeMesh`: Erzeugt ein Mesh aus einer Signed Distance Function (SDF). Die Funktion muss `(Vector3) -> (float, data)` zurückgeben
*   `createCubeMesh(center: Vector3, size: Vector3, data: object) -> HalfEdgeMesh`: Erzeugt einen Würfel als Half-Edge Mesh mit optionalem Benutzerdaten-Objekt
*   `CSGunion(meshA: HalfEdgeMesh, meshB: HalfEdgeMesh, interpolate_func: function) -> HalfEdgeMesh`: Berechnet die Vereinigung zweier Meshes
*   `CSGintersection(meshA: HalfEdgeMesh, meshB: HalfEdgeMesh, interpolate_func: function) -> HalfEdgeMesh`: Berechnet die Schnittmenge zweier Meshes
*   `CSGsubtraction(meshA: HalfEdgeMesh, meshB: HalfEdgeMesh, interpolate_func: function) -> HalfEdgeMesh`: Subtrahiert Mesh B von Mesh A

#### Fenster & Konfiguration

*   `getConfigPath() -> string`: Liefert den Pfad zum AppData-Verzeichnis (plattformabhängig)
*   `setTitle(title: string)`: Setzt den Titel des Anwendungsfensters
*   `setBackgroundColor(color: Color4)`: Setzt die Clear-Color des Fensters (Hintergrundfarbe)
*   `setDepthTest(enable: bool)`: Aktiviert oder deaktiviert den OpenGL Depth-Test (Z-Buffer)
*   `getJoysticks() -> list`: Liefert eine Liste aller aktiven Joysticks als Dictionary-Objekte mit `id`, `name`, `axes`, `buttons`
*   `closeEngine()`: Beendet die Engine und schließt das Fenster

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

#### OpenTK Mathematik-Typen

*   `Vector2i`: 2D-Vektor mit Integer-Koordinaten
*   `Vector2`: 2D-Vektor mit Float-Koordinaten
*   `Vector3`: 3D-Vektor mit Float-Koordinaten
*   `Vector3i`: 3D-Vektor mit Integer-Koordinaten
*   `Color4`: RGBA-Farbe (jeweils Float 0.0-1.0)
*   `Matrix4`: 4x4-Matrix für Transformationen (z.B. `Matrix4.CreatePerspectiveFieldOfView`, `Matrix4.LookAt`)

#### OpenGL-Enums

*   `ShaderType`: Shadertypen (`VertexShader`, `FragmentShader`, `GeometryShader`, etc.)
*   `PrimitiveType`: Primitive Typen (`Triangles`, `TriangleStrip`, `TriangleFan`, `Lines`, `Points`, etc.)

#### Input

*   `Keys`: Tastaturtasten-Enum (z.B. `I.Keys['Space']`, `I.Keys['Escape']`, `I.Keys['W']`)

#### SkiaSharp-Typen

*   `SKColor`: SkiaSharp-Farbtyp
*   `SKColors`: Vordefinierte Farben (z.B. `I.SKColors['Red']`, `I.SKColors['White']`)
*   `SKPoint`: 2D-Punkt für SkiaSharp
*   `SKShader`: SkiaSharp-Shader für Gradienten und Muster
*   `SKShaderTileMode`: Shader-Kachelmodi (`Clamp`, `Repeat`, `Mirror`)
*   `SKPaint`: Zeichenstift-Konfiguration (Farbe, Stil, Strichstärke)
*   `SKPaintStyle`: Zeichenstil (`Fill`, `Stroke`, `StrokeAndFill`)
*   `SKPath`: Vektorpfad für komplexe Formen

#### Basistypen

*   `Single`: .NET Float-Typ (entspricht Python `float`)

### Klassen

#### 2D Rendering

**TextRenderer**
*   `renderText(text: string) -> Texture`: Rendert Text in eine OpenGL-Textur

**TextureQuad**
*   `render(texture: Texture, pos: Vector2i, window_size: Vector2i)`: Zeichnet eine Textur an einer Bildschirmposition (in Pixeln)

#### Texturen & Oberflächen

**Texture**
*   `Size`: `Vector2i` mit den Dimensionen (Breite, Höhe) der Textur

**SKSurface** (von SkiaSharp)
*   Erstellt durch `createSurfaceRGBA()`. Nutzt die SkiaSharp-API für 2D-Zeichnungsoperationen

**OffscreenRenderTarget**
*   Erstellt ein Framebuffer-Objekt für Offscreen-Rendering
*   `Texture`: Die angebundene Textur, die das Render-Ergebnis enthält
*   `Width`, `Height`: Dimensionen des Render-Targets
*   `Bind()`: Bindet das Render-Target für Render-Operationen
*   `Unbind()`: Kehrt zum Standard-Framebuffer zurück
*   Nützlich für: Textur-Generierung, Post-Processing-Effekte, Shadow Mapping

#### Shader

**ShaderProgram**
*   `activate()`: Aktiviert den Shader für nachfolgende Draw-Calls
*   `SetUniform(name: string, value)`: Setzt Shader-Uniforms (unterstützt `int`, `float`, `Vector2/3/4`, `Matrix4`, `Color4`)
*   `getActiveAttributes() -> dict`: Liefert ein Dictionary mit aktiven Attributen und ihren Locations für die VertexArray-Erstellung

#### Meshes & Geometrie

**HalfEdgeMesh<T>**
*   `Triangulate() -> sequence`: Zerlegt das Mesh in Dreiecke. Liefert eine Sequenz von Tupeln `(Vector3, T)` (Position, Benutzerdaten)
*   `Map(mapping_func: function) -> HalfEdgeMesh<T>`: Transformiert die Benutzerdaten des Meshes unter Beibehaltung der Topologie
*   `CalculateVolume() -> float`: Berechnet das Volumen eines geschlossenen Meshes
*   `GetBounds() -> (Vector3, Vector3)`: Liefert die Bounding-Box (Min, Max) des Meshes

**VertexArray**
*   `draw()`: Führt den Draw-Call aus (basierend auf dem bei der Erstellung angegebenen `PrimitiveType`)
*   Enthält Vertex-Buffer und Attribut-Layout-Informationen

## Skript-Schnittstelle (Callbacks)

Die Engine kommuniziert mit dem Python-Skript über Callbacks, die beim Start (üblicherweise in der `main.py`) registriert werden müssen.

### Registrierung

*   `setRenderer(func)`: Registriert die Render-Funktion. Wird bei jedem Frame aufgerufen und empfängt die aktuelle Fenstergröße als `Vector2i` (mit `.X` und `.Y` Attributen)
*   `setInputHandler(obj)`: Registriert ein Objekt für Eingabe-Events. Die Engine sucht auf diesem Objekt nach spezifischen Methoden (siehe unten)
*   `closeEngine()`: Beendet die Engine und schließt das Fenster sofort

### Input-Callbacks

Die folgenden Methoden werden auf dem registrierten Input-Handler-Objekt aufgerufen:

*   `onKeyDown(key)`: Wird aufgerufen, wenn eine Taste gedrückt wird. `key` ist ein `Keys`-Enum-Wert
*   `onKeyUp(key)`: Wird aufgerufen, wenn eine Taste losgelassen wird
*   `onJoystickButtonDown(id, name, button)`: Wird aufgerufen, wenn ein Joystick-Button gedrückt wird
*   `onJoystickButtonUp(id, name, button)`: Wird aufgerufen, wenn ein Joystick-Button losgelassen wird
*   `onJoystickAxis(id, name, axis, value)`: Wird aufgerufen, wenn sich ein Joystick-Axis ändert. `value` ist ein Float zwischen -1.0 und 1.0

### Beispiel `main.py`

```python
import Interface as I

# Einfaches Render-Beispiel
def onRender(size):
    # Haupt-Render-Loop
    # size.X und size.Y enthalten die Fenster-Dimensionen in Pixeln
    I.setBackgroundColor(I.Color4(0.1, 0.1, 0.1, 1.0))  # Dunkelgrauer Hintergrund

class MyInputHandler:
    def onKeyDown(self, key):
        # Tastenbehandlung
        if key == I.Keys['Escape']:
            I.closeEngine()
        elif key == I.Keys['F1']:
            print("F1 gedrückt")

    def onKeyUp(self, key):
        # Wird aufgerufen, wenn eine Taste losgelassen wird
        pass

    def onJoystickButtonDown(self, id, name, button):
        # Joystick-Button gedrückt
        print(f"Joystick {name} Button {button} gedrückt")

    def onJoystickAxis(self, id, name, axis, value):
        # Joystick-Axis Bewegung
        if abs(value) > 0.1:  # Ignorieren von kleinen Werten (Rauschen)
            print(f"Joystick {name} Axis {axis}: {value}")

# Callbacks an die Engine binden
I.setRenderer(onRender)
I.setInputHandler(MyInputHandler())
```

### Wichtig

* Python-Dateien müssen im Verzeichnis `lt.cmdr.data/` liegen, um automatisch gefunden zu werden
* Der Dateiname `main.py` ist der Standard-Entry-Point
* Fehlermeldungen und Tracebacks werden in der Konsole angezeigt
* Bei fatalen Fehlern in Callbacks wird die Engine automatisch geschlossen