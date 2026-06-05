import Interface as I
import itertools, struct, math, time

NAME='CSG'

class Test:
    def __init__(Me,back):
        Me.tq=I.getTextureQuad()        
        Me.sp=I.createShaderProgram([("""
#version 330 core
in vec3 aPosition;
in vec3 aColor;
uniform mat4 uMVP;
out vec3 Color;
void main()
{
    gl_Position = uMVP * vec4(aPosition,1.0);
    Color = aColor;
}
""",I.ShaderType.VertexShader)
     ,   ("""
#version 330 core
in vec3 Color;
out vec4 FragColor;
void main()
{
    FragColor = vec4(Color,1.0);
}
""",I.ShaderType.FragmentShader)
])
        ma=I.createCubeMesh(I.Vector3(0.0,0.0,0.0),I.Vector3(1.0),(0.0,1.0,0.0))
        mb=I.createCubeMesh(I.Vector3(-0.5,-0.5,-0.5),I.Vector3(1.0),(1.0,0.0,0.0))
        def f(d1,d2,t):
            mt=1-t
            return (d1[0]*mt+d2[0]*t,d1[1]*mt+d2[1]*t,d1[2]*mt+d2[2]*t)
        sdf=I.CSGintersection(ma,mb,f).Triangulate()
        b=b''.join(struct.pack('=fff fff',x,y,z,*o) for x,y,z,o in sdf)
        aa=Me.sp.getActiveAttributes()
        Me.va=I.createVertexArray([aa[x] for x in ('aPosition','aColor')],b,I.PrimitiveType.Triangles)
        Me.Back=back
    def onRender(Me,size):
        angle = time.perf_counter()
        aspect = float(size.X) / float(size.Y)
        
        proj = I.Matrix4.CreatePerspectiveFieldOfView(math.pi / 4.0, aspect, 0.1, 100.0)
        eye = I.Vector3(math.sin(angle) * 5.0, 1.5, math.cos(angle) * 5.0)
        view = I.Matrix4.LookAt(eye, I.Vector3(0, 0, 0), I.Vector3(0, 1, 0))
        mvp = view * proj
        
        Me.sp.activate()
        Me.sp.SetUniform("uMVP", mvp)
        Me.va.draw()