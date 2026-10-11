import Interface as I
import math, struct, time

NAME = 'CSG Spielwiese'

# Schrittweite und Grenzen für das Verschieben von B.
# Mit Quader B = 1.4 breit und Schritt 0.25 liegen die Kanten von A und B nie
# exakt in einer Ebene (das wäre für CSG ein unangenehmer Sonderfall).
STEP = 0.25
LIMIT = 2.5

SIZE_A = (2.0, 2.0, 2.0)
SIZE_B = (1.4, 1.4, 3.0)
COLOR_A = (0.2, 0.7, 0.3)
COLOR_B = (0.9, 0.3, 0.2)

def vec3(t):
    return I.Vector3(t[0], t[1], t[2])

def blend(d1, d2, t):
    mt = 1 - t
    return (d1[0] * mt + d2[0] * t, d1[1] * mt + d2[1] * t, d1[2] * mt + d2[2] * t)

OPERATIONS = (
    ('Vereinigung', lambda a, b: I.CSGunion(a, b, blend)),
    ('Schnitt', lambda a, b: I.CSGintersection(a, b, blend)),
    ('Differenz A - B', lambda a, b: I.CSGsubtraction(a, b, blend)),
    ('Differenz B - A', lambda a, b: I.CSGsubtraction(b, a, blend)),
)

def wire_box(size, color):
    """Die 12 Kanten eines Quaders um den Ursprung als Linien-Vertexdaten"""
    h = [s * 0.5 for s in size]
    corners = [(x, y, z) for x in (-h[0], h[0]) for y in (-h[1], h[1]) for z in (-h[2], h[2])]
    # Ecken-Index = x*4 + y*2 + z, eine Kante verbindet Ecken die sich in genau einem Bit unterscheiden
    edges = [(i, i | bit) for i in range(8) for bit in (1, 2, 4) if not i & bit]
    return b''.join(struct.pack('=fff fff', c[0], c[1], c[2], color[0], color[1], color[2])
                    for e in edges for c in (corners[e[0]], corners[e[1]]))

class Test:
    def __init__(Me, back):
        Me.back_cb = back
        Me.tq = I.getTextureQuad()

        tr = I.getTextRenderer(40.0)
        Me.t_ops = [tr.renderText(name) for name, _ in OPERATIONS]
        Me.t_empty = tr.renderText('(leer)')
        Me.t_hint = I.getTextRenderer(28.0).renderText('Left/Right/Up/Down: B bewegen    Action: Operation    Back: Menü')

        # Fläche mit einfachem Flat-Shading. Die Normale kommt aus den Ableitungen der
        # Position im View-Space, dadurch braucht das Mesh keine Normalen-Attribute.
        Me.sp_solid = I.createShaderProgram([("""
#version 330 core
in vec3 aPosition;
in vec3 aColor;
uniform mat4 uMV;
uniform mat4 uMVP;
out vec3 Color;
out vec3 ViewPos;
void main()
{
    ViewPos = (uMV * vec4(aPosition, 1.0)).xyz;
    gl_Position = uMVP * vec4(aPosition, 1.0);
    Color = aColor;
}
""", I.ShaderType.VertexShader),
            ("""
#version 330 core
in vec3 Color;
in vec3 ViewPos;
out vec4 FragColor;
void main()
{
    vec3 n = normalize(cross(dFdx(ViewPos), dFdy(ViewPos)));
    if (n.z < 0.0) n = -n;
    float d = max(dot(n, normalize(vec3(0.4, 0.8, 0.6))), 0.0);
    FragColor = vec4(Color * (0.35 + 0.65 * d), 1.0);
}
""", I.ShaderType.FragmentShader)])

        # Einfarbiger Shader für die Drahtgitter der beiden Ausgangsquader
        Me.sp_line = I.createShaderProgram([("""
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

        Me.wire_a = Me.make_va(Me.sp_line, wire_box(SIZE_A, COLOR_A), I.PrimitiveType.Lines)
        Me.wire_b = Me.make_va(Me.sp_line, wire_box(SIZE_B, COLOR_B), I.PrimitiveType.Lines)

        Me.op = 0
        Me.bx = 0.5
        Me.by = 0.5
        Me.va = None
        Me.rebuild()

    def make_va(Me, sp, data, primitive):
        aa = sp.getActiveAttributes()
        return I.createVertexArray([aa[x] for x in ('aPosition', 'aColor')], data, primitive)

    def rebuild(Me):
        """Berechnet das CSG-Ergebnis neu (Quader sind billig, daher jedes Mal frisch erzeugt)"""
        a = I.createCubeMesh(I.Vector3(0.0, 0.0, 0.0), vec3(SIZE_A), COLOR_A)
        b = I.createCubeMesh(I.Vector3(Me.bx, Me.by, 0.0), vec3(SIZE_B), COLOR_B)
        Me.va = None
        try:
            mesh = OPERATIONS[Me.op][1](a, b)
            data = b''.join(struct.pack('=fff fff', x, y, z, *o) for x, y, z, o in mesh.Triangulate())
            # Ein leeres Ergebnis (z.B. Schnitt ohne Überlappung) hat keine Dreiecke
            if data:
                Me.va = Me.make_va(Me.sp_solid, data, I.PrimitiveType.Triangles)
        except Exception as ex:
            print('CSG fehlgeschlagen bei', OPERATIONS[Me.op][0], 'mit B =', (Me.bx, Me.by), ':', ex)

    def move(Me, dx, dy):
        bx = max(-LIMIT, min(LIMIT, Me.bx + dx * STEP))
        by = max(-LIMIT, min(LIMIT, Me.by + dy * STEP))
        if (bx, by) != (Me.bx, Me.by):
            Me.bx, Me.by = bx, by
            Me.rebuild()

    def Left(Me): Me.move(-1, 0)
    def Right(Me): Me.move(1, 0)
    def Up(Me): Me.move(0, 1)
    def Down(Me): Me.move(0, -1)
    def Action(Me):
        Me.op = (Me.op + 1) % len(OPERATIONS)
        Me.rebuild()
    def Back(Me): Me.back_cb()

    def onRender(Me, size):
        aspect = float(size.X) / float(size.Y)
        proj = I.Matrix4.CreatePerspectiveFieldOfView(math.pi / 4.0, aspect, 0.1, 100.0)
        view = I.Matrix4.LookAt(I.Vector3(0.0, 3.5, 8.0), I.Vector3(0.0, 0.0, 0.0), I.Vector3(0.0, 1.0, 0.0))
        # Das Objekt dreht sich unter festem Licht, so wandert der Lichtreflex über die Flächen
        model = I.Matrix4.CreateRotationY(time.perf_counter() * 0.5)
        mv = model * view

        if Me.va is not None:
            Me.sp_solid.activate()
            Me.sp_solid.SetUniform("uMV", mv)
            Me.sp_solid.SetUniform("uMVP", mv * proj)
            Me.va.draw()

        Me.sp_line.activate()
        Me.sp_line.SetUniform("uMVP", mv * proj)
        Me.wire_a.draw()
        Me.sp_line.SetUniform("uMVP", I.Matrix4.CreateTranslation(Me.bx, Me.by, 0.0) * mv * proj)
        Me.wire_b.draw()

        t = Me.t_ops[Me.op]
        Me.tq.render(t, I.Vector2i(16, 8), size)
        if Me.va is None:
            Me.tq.render(Me.t_empty, I.Vector2i(32 + t.Size.X, 8), size)
        Me.tq.render(Me.t_hint, I.Vector2i(16, size.Y - Me.t_hint.Size.Y - 8), size)
