# Bodenwerder

Bodenwerder ist eine Grafik-Engine, die auf F# und OpenTK basiert. Sie integriert IronPython für die Skriptsteuerung der Anwendungslogik und nutzt SkiaSharp für das Rendern von Texten.

## Features

* **Core (F#)**: Abstraktion von OpenGL 4.x (Shader, VBOs, VAOs, Texturen) und Fenstermanagement.
* **Scripting (Python)**: Die Hauptlogik befindet sich in Python-Skripten (z.B. `lt.cmdr.data/main.py`), die über eine Bridge auf die Engine zugreifen.
* **UI/Text**: Text-Rendering mittels SkiaSharp mit Unterstützung für Noto Fonts und Emojis.
* **Geometrie**: Enthält eine Half-Edge Datenstruktur (`Mesh.fs`) zur Verarbeitung und Triangulierung (Ear Clipping) von Polygon-Meshes.

## Coding Style

Me project use Me instead of this.
