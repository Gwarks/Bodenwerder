import Interface as I
import Vector2i
import itertools

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
        Me.tq.render(Me.t_title,Vector2i((size.X-Me.t_title.Size.X)>>1,0),size)
        py=Me.t_title.Size.Y+4
        for i,key in enumerate(Me.keys):
            t=Me.t_keys[i]
            Me.tq.render(t,Vector2i((size.X>>1)-t.Size.X-4,py),size)
            if key in Me.set_keys:
                tk=Me.set_keys[key][1]
                Me.tq.render(tk,Vector2i((size.X>>1)+4,py),size)
            elif Me.currentkey==key:
                Me.tq.render(Me.t_presskey,Vector2i((size.X>>1)+4,py),size)           
            py+=t.Size.Y      

class Paint:
    def __init__(Me):
        Me.tq=I.getTextureQuad()        
        tr=I.getTextRenderer(128.0)
        Me.t=I.createTextureRGBA(256,256,bytes(bytearray(itertools.chain.from_iterable((x,y,0,255) for y in range(256) for x in range(256)))))
        Me.pos=Vector2i(0,0)
        Me.cur=0
    def onRender(Me,size):
        Me.tq.render(Me.t,Me.pos,size)
    def Left(Me):
        Me.pos+=Vector2i(-1,0)
    def Right(Me):
        Me.pos+=Vector2i(1,0)
    def Up(Me):
        Me.pos+=Vector2i(0,-1)
    def Down(Me):
        Me.pos+=Vector2i(0,1)
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
print(I.createShaderProgram([("""
#version 330 core
in vec2 aPosition;
in mat4x3 aTest;
in mat3x4 aTest2;
out vec2 TexCoord;
uniform vec2 base;
uniform vec2 delta;
void main()
{
    //gl_Position = vec4(aPosition*vec2(2.0,-2.0)+vec2(-1.0,1.0), 0.0, 1.0);
    gl_Position = vec4(aPosition*delta+base, 0.0, 1.0)*aTest2*aTest;
    TexCoord = aPosition;
}
""",I.ShaderType['VertexShader'])
     ,   ("""
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D texture1;
void main()
{
    FragColor = texture(texture1, TexCoord);
}
""",I.ShaderType['FragmentShader'])
]).getActiveAttributes())