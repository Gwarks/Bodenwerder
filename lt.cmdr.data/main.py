import Interface as I
import itertools,struct

class BasicInput:
    def __init__(Me,keys):
        Me.keys=keys
    def getInputProcessor(Me,ip):
        def f(key):
            if key in Me.keys:
                getattr(ip,Me.keys[key],lambda:None)()
        return f

class BasicInputConfigurator:
    def __init__(Me,onFinish):
        Me.tq=I.getTextureQuad()
        Me.t_title=I.getTextRenderer(64.0).renderText('⌨Basic Keyboard Config⌨')
        Me.tr=I.getTextRenderer(48.0)
        Me.t_presskey=Me.tr.renderText('(Press Key)')
        Me.keys=('Left','Right','Up','Down','Action','Back')
        Me.t_keys=tuple(Me.tr.renderText(key) for key in Me.keys)
        Me.set_keys={}
        Me.currentkey=Me.getunsetkey()
        Me.onFinish=onFinish
    def getunsetkey(Me):        
        for key in Me.keys:
            if key not in Me.set_keys:
                return key
    def onKeyDown(Me,key):
        if not Me.currentkey:
            return
        for sk in Me.set_keys:
            if Me.set_keys[sk][0]==key:
                del Me.set_keys[sk]
                break
        Me.set_keys[Me.currentkey]=(key,Me.tr.renderText(str(key)))
        Me.currentkey=Me.getunsetkey()
        if not Me.currentkey:
            Me.onFinish(BasicInput({v[0]:k for k,v in Me.set_keys.items()}))
    def onRender(Me,size):
        Me.tq.render(Me.t_title,I.Vector2i((size.X-Me.t_title.Size.X)>>1,0),size)
        py=Me.t_title.Size.Y+4
        for i,key in enumerate(Me.keys):
            t=Me.t_keys[i]
            Me.tq.render(t,I.Vector2i((size.X>>1)-t.Size.X-4,py),size)
            if key in Me.set_keys:
                tk=Me.set_keys[key][1]
                Me.tq.render(tk,I.Vector2i((size.X>>1)+4,py),size)
            elif Me.currentkey==key:
                Me.tq.render(Me.t_presskey,I.Vector2i((size.X>>1)+4,py),size)           
            py+=t.Size.Y      

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
        Me.va=I.createVertexArray(Me.sp.getActiveAttributes(),b,I.PrimitiveType['TriangleFan'])
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

def onCompleteConfig(bi):
    global onRender,onKeyDown
    paint=Paint()
    onRender=paint.onRender
    onKeyDown=bi.getInputProcessor(paint) 
bi=BasicInputConfigurator(onCompleteConfig)
onKeyDown=bi.onKeyDown
onRender=bi.onRender