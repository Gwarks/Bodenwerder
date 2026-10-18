import Interface as I
import math, struct, time

NAME = 'Offscreen Render Target'

class Test:
    def __init__(Me, back):
        Me.back_cb = back
        Me.tq = I.getTextureQuad()

        # Einfacher Shader für Position und Farbe
        Me.sp = I.createShaderProgram([("""
#version 330 core
in vec3 aPosition;
in vec3 aColor;
uniform mat4 uMVP;
out vec3 Color;
void main()
{
    gl_Position = uMVP * vec4(aPosition, 1.0);
    Color = aColor;
}
""", I.ShaderType.VertexShader),
            ("""
#version 330 core
in vec3 Color;
out vec4 FragColor;
void main()
{
    FragColor = vec4(Color, 1.0);
}
""", I.ShaderType.FragmentShader)])

        # Offscreen Render Target erstellen (512x512)
        Me.rt_size = 512
        Me.rt = I.createOffscreenRenderTarget(Me.rt_size, Me.rt_size)

        # Einfaches Dreieck als Test-Objekt
        Me.triangle_va = Me.create_triangle()

        # Text-Rendering für Anzeige
        tr = I.getTextRenderer(32.0)
        Me.t_title = tr.renderText('Offscreen Render Target Demo')
        Me.t_hint = I.getTextRenderer(20.0).renderText(
            'Left/Right: Rotation   Up/Down: Offscreen Size   Action: Toggle Mode   Back: Menu')

        # Zustandsvariablen
        Me.angle = 0.0
        Me.mode = 0  # 0 = beide, 1 = nur Offscreen, 2 = nur direkt

    def create_triangle(Me):
        """Erstellt ein einfaches buntes Dreieck"""
        # Drei Ecken mit Positionen und Farben
        # Format: x,y,z r,g,b für jede Ecke (6 Floats pro Ecke, 18 insgesamt)
        data = struct.pack('=18f',
            0.0, 0.5, 0.0,   1.0, 0.0, 0.0,   # Rot oben (x,y,z, r,g,b)
            -0.5, -0.5, 0.0, 0.0, 1.0, 0.0,  # Grün unten links
            0.5, -0.5, 0.0,  0.0, 0.0, 1.0)  # Blau unten rechts
        aa = Me.sp.getActiveAttributes()
        return I.createVertexArray([aa['aPosition'], aa['aColor']], list(data), I.PrimitiveType.Triangles)

    def Left(Me):
        Me.angle -= 0.1

    def Right(Me):
        Me.angle += 0.1

    def Up(Me):
        Me.rt_size = min(1024, Me.rt_size + 64)
        # Recreate offscreen target with new size
        Me.rt = I.createOffscreenRenderTarget(Me.rt_size, Me.rt_size)

    def Down(Me):
        Me.rt_size = max(64, Me.rt_size - 64)
        # Recreate offscreen target with new size
        Me.rt = I.createOffscreenRenderTarget(Me.rt_size, Me.rt_size)

    def Action(Me):
        Me.mode = (Me.mode + 1) % 3

    def Back(Me):
        Me.back_cb()

    def onRender(Me, size):
        # Berechne Projektions- und View-Matrix
        aspect = float(size.X) / float(size.Y)
        proj = I.Matrix4.CreatePerspectiveFieldOfView(math.pi / 3.0, aspect, 0.1, 100.0)
        view = I.Matrix4.LookAt(I.Vector3(0.0, 0.0, 3.0), I.Vector3(0.0, 0.0, 0.0), I.Vector3(0.0, 1.0, 0.0))

        # ============================================
        # 1. Rendere die Szene in das Offscreen Target
        # ============================================
        I.setBackgroundColor(I.Color4(0.3, 0.0, 0.0, 1.0))
        Me.rt.Bind()
        
        # Clear des Offscreen Buffers
        
        # Kamera für Offscreen-Rendering (1:1 Aspect Ratio da quadratisch)
        offscreen_proj = I.Matrix4.CreatePerspectiveFieldOfView(math.pi / 3.0, 1.0, 0.1, 100.0)
        model = I.Matrix4.CreateRotationY(Me.angle) * I.Matrix4.CreateRotationX(Me.angle * 0.7)
        mvp = model * view * offscreen_proj
        
        Me.sp.activate()
        Me.sp.SetUniform("uMVP", mvp)
        Me.triangle_va.draw()
        
        I.bindScreenRenderTarget()  # Zurück zum Bildschirm-Framebuffer

        # ============================================
        # 2. Rendere auf den Bildschirm
        # ============================================
        I.setBackgroundColor(I.Color4(0.1, 0.1, 0.15, 1.0))

        # Zeige das Offscreen-Ergebnis als Textur
        if Me.mode != 2:  # Mode 0 oder 1
            # Berechne Position und Größe für die Textur-Anzeige
            # Offscreen Target ist quadratisch (rt_size x rt_size)
            display_width = min(size.X * 0.6, size.Y * 0.6)
            display_height = display_width
            x_pos = (size.X - display_width) // 2
            y_pos = size.Y // 4
            
            # Zeichne die Textur des Offscreen-Render-Targets
            # TextureQuad.render() erwartet: (texture, pos, client_size)
            Me.tq.render(Me.rt.Texture, I.Vector2i(x_pos, y_pos), size)

        # Zeige das Dreieck direkt auf dem Bildschirm
        if Me.mode != 1:  # Mode 0 oder 2
            Me.sp.activate()
            model = I.Matrix4.CreateRotationY(Me.angle) * I.Matrix4.CreateRotationX(Me.angle * 0.7) * I.Matrix4.CreateScale(2.5, 2.5, 2.5)
            mvp = model * view * proj
            Me.sp.SetUniform("uMVP", mvp)
            Me.triangle_va.draw()

        # Anzeige der UI-Elemente
        Me.tq.render(Me.t_title, I.Vector2i(16, 16), size)
        
        # Zeige den aktuellen Modus an
        mode_text = I.getTextRenderer(24.0).renderText(
            'Mode: ' + ('Both' if Me.mode == 0 else ('Offscreen Only' if Me.mode == 1 else 'Direct Only')))
        Me.tq.render(mode_text, I.Vector2i(16, 60), size)
        
        # Zeige die aktuelle Größe an
        size_text = I.getTextRenderer(24.0).renderText(f'Offscreen Size: {Me.rt_size}x{Me.rt_size}')
        Me.tq.render(size_text, I.Vector2i(16, 96), size)
        
        # Hinweistext unten
        Me.tq.render(Me.t_hint, I.Vector2i(16, size.Y - Me.t_hint.Size.Y - 16), size)
