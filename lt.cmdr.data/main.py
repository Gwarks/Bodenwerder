import Interface as I
import itertools,struct
import input

class Paint:
    def __init__(Me):
        Me.tq=I.getTextureQuad()        
        Me.t=I.createTexture(256,256,4,bytes(itertools.chain.from_iterable((x,y,0,255) for y in range(256) for x in range(256))))
        Me.pos=I.Vector2i(0,0)
        Me.cur=0
        Me.sp=I.createShaderProgram([("""
#version 330 core
in vec2 aPosition;
in vec3 aColor;
out vec3 Color;
void main()
{
    gl_Position = vec4(aPosition,0.0,1.0);
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
        b=b''.join(
            struct.pack('=ff fff',*f) for f in (
     ( 0.5, -0.5,   1.0, 0.0, 0.0 ),
     (-0.5, -0.5,   0.0, 1.0, 0.0 ),
     ( 0.0,  0.5,   0.0, 0.0, 1.0 ),
            )
        )
        aa=Me.sp.getActiveAttributes()
        Me.va=I.createVertexArray([aa[x] for x in ('aPosition','aColor')],b,I.PrimitiveType['TriangleFan'])
    def onRender(Me,size):
        Me.sp.activate()
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