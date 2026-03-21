import Interface as I

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