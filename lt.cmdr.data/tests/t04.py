import Interface as I
import math, time, struct

NAME = '3D Sokoban'

class Test:
    def __init__(Me, back):
        Me.tq = I.getTextureQuad()
        Me.back_cb = back
        
        # Shader für Position und Farbe
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

        # Definition des Sokoban-Levels
        Me.reset()
        
        # 3D-Einstellungen
        Me.wall_height = 2.0
        
        # OBJEKT-TEMPLATES (einmal beim Start erstellen)
        Me.create_templates()

        # Dynamische Positionen (werden bei Bewegung aktualisiert)
        Me.player_pos = I.Vector3(float(Me.px), 0, float(Me.py))
        Me.box_positions = []  # Liste von (x, z) für Kisten
        Me.target_positions = []  # Liste von (x, z) für Ziele
        Me.update_dynamic_positions()

    def reset(Me):
        """Level zurücksetzen"""
        lvl = [
            "  ######",
            "###    #",
            "# $    #",
            "# #$. ##",
            "# .   # ",
            "###  $# ",
            "  # . # ",
            "  #@  # ",
            "  ##### "
        ]
        Me.max_w = max(len(r) for r in lvl)
        Me.grid = [list(r.ljust(Me.max_w)) for r in lvl]
        for r in range(len(Me.grid)):
            for c in range(len(Me.grid[r])):
                if Me.grid[r][c] in ('@', '+'):
                    Me.py, Me.px = r, c

    def create_templates(Me):
        """Erstellt OBJEKT-TEMPLATES (einmal beim Start)"""
        
        # WAND-Template als SDF mit Steinmuster
        Me.wall_va = Me.create_wall_template()
        
        # BODEN-Template
        Me.floor_va = Me.create_floor_template()
        
        # KISTE-Template
        Me.box_va = Me.create_box_template()
        
        # ZIEL-Template
        Me.target_va = Me.create_target_template()
        
        # SPIELER-Template
        Me.player_va = Me.create_player_template()

    def create_wall_template(Me):
        """Erstellt EIN Wand-Objekt als SDF mit Ziegelmuster"""
               
        # SDF-Hilfsfunktion
        def vec3_length(v):
            return math.sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z)

        def sdBox(p, center, size):
            q = I.Vector3(abs(p.X - center.X), abs(p.Y - center.Y), abs(p.Z - center.Z))
            d = I.Vector3(q.X - size.X * 0.5, q.Y - size.Y * 0.5, q.Z - size.Z * 0.5)
            return min(max(d.X, max(d.Y, d.Z)), 0.0) + vec3_length(
                I.Vector3(max(d.X, 0), max(d.Y, 0), max(d.Z, 0)))

        def wall_sdf(p):
            # Grundkörper der Mauer
            width = 0.4
            height = Me.wall_height
            depth = 0.4

            center = I.Vector3(0, height * 0.5, 0)
            size = I.Vector3(width, height, depth)

            box_dist = sdBox(p, center, size)

            # Ziegelmaße
            brick_width = 0.2
            brick_height = 0.1

            # Breite und Tiefe der Mörtelfugen
            mortar_width = 0.008
            mortar_depth = 0.015

            # Vorderseite der Wand
            front_z = depth * 0.5

            # Lokale Koordinaten:
            # X: von der linken Wandkante
            # Y: vom Boden der Wand
            # Z: Abstand von der Vorderseite
            local_x = p.X + width * 0.5
            local_y = p.Y

            # Negative Koordinaten bei Bedarf normalisieren
            row = int(math.floor(local_y / brick_height))

            # Versetztes Läufermuster
            offset = 0.0
            if row % 2 != 0:
                offset = brick_width * 0.5

            brick_x = (local_x + offset) % brick_width
            brick_y = local_y % brick_height

            # Abstand zur nächsten horizontalen Fuge
            dy = min(brick_y, brick_height - brick_y)

            # Abstand zur nächsten vertikalen Fuge
            dx = min(brick_x, brick_width - brick_x)

            # Signed distance zum Fugenmuster:
            # Negativ innerhalb einer Fuge
            seam_dist = min(
                dy - mortar_width * 0.5,
                dx - mortar_width * 0.5
            )

            # Fugen nur an der Vorderseite aus dem Stein schneiden.
            # Die Vertiefung reicht von der Oberfläche nach innen.
            groove_center_z = front_z - mortar_depth * 0.5
            groove_half_z = mortar_depth * 0.5 + 0.001

            groove_z_dist = (
                abs(p.Z - groove_center_z) - groove_half_z
            )

            # Schnittvolumen der Fuge
            groove_dist = max(seam_dist, groove_z_dist)

            # Subtraktion des Fugenvolumens vom Wandkörper
            # max(a, -b) entspricht einer SDF-Differenz
            wall_dist = max(box_dist, -groove_dist)

            # Dezente Farbvariation pro Ziegel
            column = int(math.floor(
                (local_x + offset) / brick_width
            ))

            variation = ((row * 17 + column * 31) % 7) / 100.0

            brick_color = (
                0.65 + variation,
                0.27 + variation * 0.5,
                0.16 + variation * 0.3
            )

            # Mörtel: warmes Grau-Beige
            mortar_color = (0.48, 0.43, 0.36)

            # Farbe der Fugen auf der Vorderseite
            if (
                abs(p.Z - front_z) < mortar_depth + 0.003
                and seam_dist < 0.0
            ):
                color = mortar_color
            else:
                color = brick_color

            return (wall_dist, color)
        
        # SDF-Bereich für eine Wand
        min_bound = I.Vector3(-0.25, -0.5, -0.25)
        max_bound = I.Vector3(0.25, Me.wall_height + 0.5, 0.25)
        resolution = I.Vector3i(32, 192, 32)
        print(max_bound-min_bound)
        wall_mesh = I.createMeshFromSDF(min_bound, max_bound, resolution, wall_sdf)
        
        # VertexArray erstellen
        b = b''.join(struct.pack('=fff fff', x, y, z, r, g, b) 
                     for x, y, z, (r, g, b) in wall_mesh.Triangulate())
        aa = Me.sp.getActiveAttributes()
        return I.createVertexArray([aa[x] for x in ('aPosition', 'aColor')], b, I.PrimitiveType.Triangles)

    def create_floor_template(Me):
        """Erstellt Boden-Template (1x1 Fliese)"""
        floor_mesh = I.createCubeMesh(I.Vector3(0, -0.05, 0), I.Vector3(1.0, 0.1, 1.0), (0.8, 0.8, 0.8))
        b = b''.join(struct.pack('=fff fff', x, y, z, r, g, b) 
                     for x, y, z, (r, g, b) in floor_mesh.Triangulate())
        aa = Me.sp.getActiveAttributes()
        return I.createVertexArray([aa[x] for x in ('aPosition', 'aColor')], b, I.PrimitiveType.Triangles)

    def create_box_template(Me):
        """Erstellt Kisten-Template"""
        box_mesh = I.createCubeMesh(I.Vector3(0, 0.45, 0), I.Vector3(0.9, 0.9, 0.9), (1.0, 0.5, 0.0))
        b = b''.join(struct.pack('=fff fff', x, y, z, r, g, b) 
                     for x, y, z, (r, g, b) in box_mesh.Triangulate())
        aa = Me.sp.getActiveAttributes()
        return I.createVertexArray([aa[x] for x in ('aPosition', 'aColor')], b, I.PrimitiveType.Triangles)

    def create_target_template(Me):
        """Erstellt Ziel-Template"""
        target_mesh = I.createCubeMesh(I.Vector3(0, 0.05, 0), I.Vector3(0.9, 0.1, 0.9), (0.0, 1.0, 0.0))
        b = b''.join(struct.pack('=fff fff', x, y, z, r, g, b) 
                     for x, y, z, (r, g, b) in target_mesh.Triangulate())
        aa = Me.sp.getActiveAttributes()
        return I.createVertexArray([aa[x] for x in ('aPosition', 'aColor')], b, I.PrimitiveType.Triangles)

    def create_player_template(Me):
        """Erstellt Spieler-Template"""
        player_mesh = I.createCubeMesh(I.Vector3(0, 0.6, 0), I.Vector3(0.8, 1.2, 0.8), (0.0, 0.0, 1.0))
        b = b''.join(struct.pack('=fff fff', x, y, z, r, g, b) 
                     for x, y, z, (r, g, b) in player_mesh.Triangulate())
        aa = Me.sp.getActiveAttributes()
        return I.createVertexArray([aa[x] for x in ('aPosition', 'aColor')], b, I.PrimitiveType.Triangles)

    def update_dynamic_positions(Me):
        """Aktualisiert Positionen von Kisten, Zielen und Spieler"""
        Me.box_positions = []
        Me.target_positions = []
        for r in range(len(Me.grid)):
            for c in range(len(Me.grid[r])):
                cell = Me.grid[r][c]
                if cell in ('$', '*'):
                    Me.box_positions.append(I.Vector3(float(c), 0, float(r)))
                if cell in ('.', '*'):
                    Me.target_positions.append(I.Vector3(float(c), 0, float(r)))
        Me.player_pos = I.Vector3(float(Me.px), 0, float(Me.py))

    def move(Me, dr, dc):
        """Bewegung des Spielers (wie in t01)"""
        nr, nc = Me.py + dr, Me.px + dc
        if not (0 <= nr < len(Me.grid) and 0 <= nc < len(Me.grid[0])): return
        
        cell = Me.grid[nr][nc]
        if cell == '#': return
        
        if cell in ('$', '*'):
            nnr, nnc = nr + dr, nc + dc
            if not (0 <= nnr < len(Me.grid) and 0 <= nnc < len(Me.grid[0])): return
            b_cell = Me.grid[nnr][nnc]
            if b_cell in (' ', '.'):
                Me.grid[nnr][nnc] = '$' if b_cell == ' ' else '*'
                Me.grid[nr][nc] = ' ' if cell == '$' else '.'
                cell = Me.grid[nr][nc]
            else: return
        
        Me.grid[Me.py][Me.px] = ' ' if Me.grid[Me.py][Me.px] == '@' else '.'
        Me.py, Me.px = nr, nc
        Me.grid[Me.py][Me.px] = '@' if cell == ' ' else '+'
        
        Me.update_dynamic_positions()

    def Up(Me): Me.move(-1, 0)
    def Down(Me): Me.move(1, 0)
    def Left(Me): Me.move(0, -1)
    def Right(Me): Me.move(0, 1)
    def Action(Me): Me.reset(); Me.update_dynamic_positions()
    def Back(Me): Me.back_cb()

    def onRender(Me, size):
        aspect = float(size.X) / float(size.Y)
        
        # Kamera
        proj = I.Matrix4.CreatePerspectiveFieldOfView(math.pi / 4.0, aspect, 0.1, 100.0)
        camera_dist = 15.0
        eye = I.Vector3(0, camera_dist * 0.8, camera_dist)
        target = I.Vector3(float(Me.max_w) * 0.5 - 0.5, 0, float(len(Me.grid)) * 0.5 - 0.5)
        view = I.Matrix4.LookAt(eye, target, I.Vector3(0, 1, 0))
        base_view = view
        
        # Rendering
        Me.sp.activate()
        
        # Boden: für jede Zelle
        for r in range(len(Me.grid)):
            for c in range(len(Me.grid[r])):
                # Nur wenn Zelle nicht Wand ist (Wände haben eigenen Boden)?
                # Oder einfach alle Zellen mit Boden
                mvp = I.Matrix4.CreateTranslation(float(c), 0, float(r)) * base_view * proj
                Me.sp.SetUniform("uMVP", mvp)
                Me.floor_va.draw()
        
        # Wände: für jede '#' Zelle
        for r in range(len(Me.grid)):
            for c in range(len(Me.grid[r])):
                if Me.grid[r][c] == '#':
                    mvp = I.Matrix4.CreateTranslation(float(c), 0, float(r)) * base_view * proj
                    Me.sp.SetUniform("uMVP", mvp)
                    Me.wall_va.draw()
        
        # Ziele
        for pos in Me.target_positions:
            mvp = I.Matrix4.CreateTranslation(pos.X, 0, pos.Z) * base_view * proj
            Me.sp.SetUniform("uMVP", mvp)
            Me.target_va.draw()
        
        # Kisten
        for pos in Me.box_positions:
            mvp = I.Matrix4.CreateTranslation(pos.X, 0, pos.Z) * base_view * proj
            Me.sp.SetUniform("uMVP", mvp)
            Me.box_va.draw()
        
        # Spieler
        mvp = I.Matrix4.CreateTranslation(Me.player_pos.X, 0, Me.player_pos.Z) * base_view * proj
        Me.sp.SetUniform("uMVP", mvp)
        Me.player_va.draw()
