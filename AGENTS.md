# Bodenwerder - Agent Guidelines

## Project Overview

Bodenwerder is an **F#**-based **3D graphics application** using **OpenTK (OpenGL 4)** for rendering and **IronPython** for scripting. It provides a framework for 3D modeling, CSG (Constructive Solid Geometry) operations, and real-time rendering with Python as the scripting interface.

### Core Components

- **Engine.fs**: OpenGL resource management (ShaderProgram, Texture, VertexArray, Window)
- **Engine.Tools.fs**: Attribute layout utilities for OpenGL vertex attributes
- **Engine.Canvas.fs**: 2D rendering utilities (TextRenderer, TextureQuad) using SkiaSharp
- **Mesh.fs**: Half-edge mesh data structure with AABB tree support
- **Mesh.CSG.fs**: Constructive Solid Geometry operations (union, intersection, subtraction)
- **Application.fs**: Python scripting interface and main application loop
- **Tools.fs**: Reflection utilities and dynamic type wrappers for Python interop

### Scripting

The application is controlled by **Python scripts** located in `lt.cmdr.data/`. The main entry point is `main.py`. The `Interface` type in Application.fs exposes the API to Python scripts.

---

## Development Rules

### Language and Framework

- **Primary language**: F# (.NET 10)
- **Graphics API**: OpenTK.Graphics.OpenGL4
- **2D Rendering**: SkiaSharp
- **Scripting**: IronPython 3.4.2
- **Build**: Microsoft.NET.Sdk

### File Organization

- Source files are listed in `Bodenwerder.fsproj` in **dependency order** (Tools.fs → Mesh.fs → Engine.Tools.fs → Engine.fs → Engine.Canvas.fs → Application.fs → Program.fs)
- **Do not** add files to the project without adding them to the `.fsproj` in the correct order
- Python scripts and data files are in `lt.cmdr.data/` and are copied to output directory

### Build and Test

```bash
# Build
dotnet build Bodenwerder.fsproj

# Run
dotnet run --project Bodenwerder.fsproj
```

### OpenGL Resource Management

- **All OpenGL resources** (shaders, textures, buffers, VAOs) **must** use `Globals.resourceCollector.AddResource()` in their Finalize() method
- Resources are automatically cleaned up at the start of each frame via `Globals.resourceCollector.CleanupAll()`
- Do not manually call GL.Delete* functions directly

### Python Interop

- Python types are exposed via `Tools.TypeWrapper` for constructor access and method calls
- Use `System.Collections.Generic.IList<obj>` for Python list/tuple parameters
- Use `Func<...>` delegates for Python function callbacks
- Python exceptions are formatted via `Interface.FormatPyError()` before logging

### Coding Style

- Use **CamelCase** for module names (e.g., `Engine.Tools`)
- Use **PascalCase** for types and members
- Use **snake_case** is NOT used (this is F#, not Python)
- Use F# idiomatic patterns: discriminated unions, records, pattern matching
- Prefer **immutable** data where possible
- Use `member Me.` syntax for class members (as seen in existing code)

### Error Handling

- Use `failwithf` for unrecoverable errors with formatted messages
- Use `printfn` for diagnostic logging (not for error reporting in production code)
- Catch and log OpenGL errors, but continue rendering when possible

---

## Agent-Specific Instructions

### Reading Code

Before making changes:
1. Read the **target file** completely
2. Read **all files that import/depend on it** (check `.fsproj` order and `open` statements)
3. Read **Application.fs** to understand the Python API surface
4. Read **Engine.fs** for OpenGL patterns

### F# Editing Rules

- **Never** edit an F# file you haven't read in the current session
- **Never** edit a file and read another in the same turn
- Match existing indentation (4 spaces, not tabs)
- Match existing naming conventions
- Ensure F# type inference works correctly - if the compiler can't infer, add explicit type annotations

### OpenGL Best Practices

- Vertex attribute layouts must use `Tools.attribLayout` from Engine.Tools.fs
- Shader compilation errors **must** check `GL.GetShaderInfoLog` and fail with the log
- Program linking errors **must** check `GL.GetProgramInfoLog` and fail with the log
- Always bind textures to texture units before setting uniform samplers
- Use `PrimitiveType` enum values for draw calls

### Python Scripting Interface

The `Interface` type in Application.fs is the **only** bridge between F# and Python. When adding new features:

1. Add the F# implementation first
2. Add a corresponding method to `Interface` type
3. Expose necessary .NET types via `TypeWrapper` properties
4. Handle Python type conversions (IList<byte>, etc.)

### Mesh and CSG Operations

- Meshes use a **half-edge data structure** (`HEHalfEdge`, `HEVertex`, `HEFace`)
- CSG operations work on `HalfEdgeMesh<'Data>` with generic data payloads
- SDF (Signed Distance Function) based mesh generation is supported via `FromSDF`
- AABB trees are used for spatial partitioning

---

## Common Patterns

### Creating a VertexArray

```fsharp
let va = VertexArray(
    attributes = [(ActiveAttribType.FloatVec3, 1, 0)], // (type, size, location)
    vertexs = vertexDataBytes,
    primitivetype = PrimitiveType.Triangles
)
```

### Creating a ShaderProgram

```fsharp
let shader = ShaderProgram [
    (vertexShaderSource, ShaderType.VertexShader)
    (fragmentShaderSource, ShaderType.FragmentShader)
]
```

### Python Type Exposure

```fsharp
member Me.Vector3 = Tools.TypeWrapper(typeof<Vector3>)
member Me.ShaderType = Tools.TypeWrapper(typeof<ShaderType>)
```

### Resource Cleanup

```fsharp
override Me.Finalize() =
    Globals.resourceCollector.AddResource(resourceId, fun () -> GL.DeleteResource(resourceId))
```

---

## Important Types

| Type | Location | Purpose |
|------|----------|---------|
| `ShaderProgram` | Engine.fs | Compiles and manages GLSL shaders |
| `Texture` | Engine.fs | Manages 2D textures |
| `VertexArray` | Engine.fs | Manages VAO + VBO for vertex data |
| `Window` | Engine.fs | Main application window with input handling |
| `HalfEdgeMesh<'Data>` | Mesh.fs | 3D mesh with half-edge topology |
| `AttribLayout` | Engine.Tools.fs | Describes OpenGL vertex attribute layout |
| `TypeWrapper` | Tools.fs | Exposes .NET types to Python dynamically |
| `Interface` | Application.fs | Bridge between F# engine and Python scripts |

---

## External Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| OpenTK | 4.9.4 | OpenGL bindings and windowing |
| SkiaSharp | 3.119.2 | 2D rendering, text, image processing |
| IronPython | 3.4.2 | Python scripting engine |
| IronPython.StdLib | 3.4.2 | Python standard library |
| Msagl | 1.2.1 | Graph layout algorithms |

---

## Special Notes

1. **Python Data Path**: `lt.cmdr.data/` is added to Python search paths. This directory contains `main.py`, fonts (`NotoSans-Regular.ttf`, `NotoColorEmoji.ttf`), and any other script assets.

2. **Font Handling**: Text rendering uses SkiaSharp with Noto fonts loaded from `lt.cmdr.data/`. Fonts are loaded via `Tools.WeakSingleton.get<NotoFont>()`.

3. **Joystick Support**: Input handling includes joystick/configure support via GLFW. Joystick state is polled each frame and mapped to Python callbacks.

4. **Coordinate Systems**: The rendering uses a standard OpenGL coordinate system. TextureQuad rendering uses normalized device coordinates (-1 to 1) with adjustments for aspect ratio.

5. **WeakSingleton Pattern**: Use `Tools.WeakSingleton.get<'T>()` for singleton instances that should be garbage collected when no longer referenced (like fonts).

---

## Do Not Do

- ❌ Don't call `GL.Delete*` directly - use `Globals.resourceCollector`
- ❌ Don't add Python dependencies to .fsproj - they're handled via IronPython
- ❌ Don't use C# naming conventions (PascalCase for everything) - F# uses different conventions
- ❌ Don't modify lt.cmdr.data/ files directly - these are user scripts
- ❌ Don't change the compile order in .fsproj without understanding F# dependency resolution

---

## Verification

After making changes:
1. Ensure the project **compiles** without errors
2. Verify Python scripts can still **import and use** the Interface module
3. Check that OpenGL resource cleanup happens without exceptions
4. Test with a simple Python script that exercises the changed functionality
