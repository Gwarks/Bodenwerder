import Interface as I
import itertools,struct,math
import input

I.setDepthTest(True)

def Kugel(v,r,c):
    return (v).Length-r,c
def sdfUnion(o1,o2):
    return o1 if o1[0]<o2[0] else o2
def f(v):
    k1=Kugel(v-I.Vector3(0.4,0.0,0.0),.5,(1.0,0.0,0.0))
    k2=Kugel(v+I.Vector3(0.4,0.0,0.0),.5,(0.0,1.0,0.0))
    return sdfUnion(k1,k2)
sdf=list(I.createMeshFromSDF(I.Vector3(-1.0,-1.0,-1.0),I.Vector3(1.0,1.0,1.0),I.Vector3i(64,64,64),f).Triangulate())

class Paint:
    def __init__(Me):
        Me.angle = 0.0
        Me.tq=I.getTextureQuad()        
        Me.t=I.createTexture(256,256,4,bytes(itertools.chain.from_iterable((x,y,0,255) for y in range(256) for x in range(256))))
        Me.pos=I.Vector2i(0,0)
        Me.cur=0
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
""",I.ShaderType['VertexShader'])
     ,   ("""
#version 330 core
in vec3 Color;
out vec4 FragColor;
void main()
{
    FragColor = vec4(Color,1.0);
}
""",I.ShaderType['FragmentShader'])
])
        b=b''.join(struct.pack('=fff fff',x,y,z,*o) for x,y,z,o in sdf)
        aa=Me.sp.getActiveAttributes()
        Me.va=I.createVertexArray([aa[x] for x in ('aPosition','aColor')],b,I.PrimitiveType['Triangles'])
    def onRender(Me,size):
        Me.angle += 0.0005
        aspect = float(size.X) / float(size.Y)
        
        proj = I.Matrix4.CreatePerspectiveFieldOfView(math.pi / 4.0, aspect, 0.1, 100.0)
        eye = I.Vector3(math.sin(Me.angle) * 5.0, 1.5, math.cos(Me.angle) * 5.0)
        view = I.Matrix4.LookAt(eye, I.Vector3(0, 0, 0), I.Vector3(0, 1, 0))
        mvp = view * proj
        
        Me.sp.activate()
        Me.sp.SetUniform("uMVP", mvp)
        Me.va.draw()
        Me.tq.render(Me.t,Me.pos,size)
    def Left(Me):
        Me.pos+=I.Vector2i(-1,0)
    def Right(Me):
        Me.pos+=I.Vector2i(1,0)
    def Up(Me):
        Me.pos+=I.Vector2i(0,-1)
    def Down(Me):
        Me.pos+=I.Vector2i(0,1)
    def Action(Me):
        Me.cur=1
    def Back(Me):
        Me.cur=0

I.setBackgroundColor(I.Color4(0.2,0.5,0,0))
def onCompleteConfig(bi):
    global onRender,onKeyDown
    paint=Paint()
    onRender=paint.onRender
    onKeyDown=bi.getInputProcessor(paint) 
    I.setBackgroundColor(I.Color4(0.6,0.9,1,0))
bi=input.BasicInputConfigurator(onCompleteConfig)
onKeyDown=bi.onKeyDown
onRender=bi.onRender