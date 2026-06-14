import Interface as I
import clr
import math
import System

# SkiaSharp ist in der Engine verfügbar und wird hier eingebunden
clr.AddReference("SkiaSharp")
from SkiaSharp import *

NAME = 'Canvas Geometrie & Gradient'

class Test:
    def __init__(Me, back):
        Me.back_cb = back
        Me.tq = I.getTextureQuad()
        
        # Wir erstellen eine quadratische Zeichenfläche (Canvas)
        Me.res = 512
        Me.surface = I.createSurfaceRGBA(Me.res, Me.res)
        # Der Canvas bietet Zugriff auf die 2D-Zeichenfunktionen von Skia
        Me.canvas = Me.surface.Canvas
        
        Me.draw_scene()
        
        # Eine OpenGL-Textur aus den Canvas-Daten generieren
        Me.tex = I.image2texture(Me.surface.Snapshot())

    def draw_scene(Me):
        # Hintergrund transparent löschen
        Me.canvas.Clear(SKColors.Transparent)
        
        center = SKPoint(Me.res / 2.0, Me.res / 2.0)
        radius = Me.res * 0.45
        
        # Gemeinsamer radialer Farbverlauf für beide Formen
        colors = System.Array[SKColor]([SKColors.Cyan, SKColors.Magenta, SKColors.Green])
        pos = System.Array[System.Single]([0.0, 0.7, 1.0])
        shader = SKShader.CreateRadialGradient(center, radius, colors, pos, SKShaderTileMode.Clamp)
        
        paint = SKPaint()
        paint.Shader = shader
        paint.IsAntialias = True
        paint.Style = SKPaintStyle.Fill

        # 1. Ein einfaches Polygon (Sechseck)
        poly_path = SKPath()
        sides = 6
        r = 120
        p_center = SKPoint(Me.res * 0.35, Me.res * 0.35)
        for i in range(sides):
            angle = (2.0 * math.pi * i) / sides
            x = p_center.X + r * math.cos(angle)
            y = p_center.Y + r * math.sin(angle)
            if i == 0: poly_path.MoveTo(x, y)
            else: poly_path.LineTo(x, y)
        poly_path.Close()
        Me.canvas.DrawPath(poly_path, paint)

        # 2. Eine Fläche aus Splines (Bezier-Kurven)
        spline_path = SKPath()
        s_center = SKPoint(Me.res * 0.65, Me.res * 0.65)
        spline_path.MoveTo(s_center.X - 50, s_center.Y - 50)
        # QuadTo für einfache Kurven, CubicTo für komplexe Splines
        spline_path.QuadTo(s_center.X + 150, s_center.Y - 100, s_center.X + 100, s_center.Y + 100)
        spline_path.CubicTo(s_center.X + 50, s_center.Y + 150, s_center.X - 150, s_center.Y + 50, s_center.X - 50, s_center.Y - 50)
        spline_path.Close()
        Me.canvas.DrawPath(spline_path, paint)

    def onRender(Me, size):
        # Das generierte Bild zentriert auf dem Bildschirm ausgeben
        x = (size.X - Me.res) // 2
        y = (size.Y - Me.res) // 2
        Me.tq.render(Me.tex, I.Vector2i(x, y), size)

    def Back(Me):
        Me.back_cb()