import Interface as I
import json
import os

class BasicInput:
    def __init__(Me,keys):
        Me.keys=keys
    def getInputProcessor(Me,ip):
        def f(key):
            if key in Me.keys:
                getattr(ip,Me.keys[key],lambda:None)()
        return f

class ConfigStateConfirm:
    def __init__(Me,ctx,loaded_config):
        Me.ctx=ctx
        Me.loaded_config=loaded_config
        Me.t_msg1=Me.ctx.tr.renderText('Config found. Press [Action]')
        Me.t_msg2=Me.ctx.tr.renderText('to load or any key to reset.')
        Me.t_conf={k:Me.ctx.tr.renderText(f"{k}: {v}") for k,v in Me.loaded_config.items()}
    def onKeyDown(Me,key):
        if key==Me.loaded_config['Action']:
            Me.ctx.onFinish(BasicInput({v:k for k,v in Me.loaded_config.items()}))
        else:
            Me.ctx.setState(ConfigStateDefining(Me.ctx))
    def onRender(Me,size,py):
        Me.ctx.tq.render(Me.t_msg1,I.Vector2i((size.X-Me.t_msg1.Size.X)>>1,py),size)
        py+=Me.t_msg1.Size.Y
        Me.ctx.tq.render(Me.t_msg2,I.Vector2i((size.X-Me.t_msg2.Size.X)>>1,py),size)
        py+=Me.t_msg2.Size.Y+8
        for k in Me.ctx.keys:
            if k in Me.t_conf:
                t=Me.t_conf[k]
                Me.ctx.tq.render(t,I.Vector2i((size.X-t.Size.X)>>1,py),size)
                py+=t.Size.Y

class ConfigStateDefining:
    def __init__(Me,ctx):
        Me.ctx=ctx
        Me.t_presskey=Me.ctx.tr.renderText('(Press Key)')
        Me.t_keys=tuple(Me.ctx.tr.renderText(key) for key in Me.ctx.keys)
        Me.set_keys={}
        Me.currentkey=Me.getunsetkey()
    def getunsetkey(Me):        
        for key in Me.ctx.keys:
            if key not in Me.set_keys:
                return key
    def onKeyDown(Me,key):
        if not Me.currentkey:
            return
        for sk in list(Me.set_keys):
            if Me.set_keys[sk][0]==key:
                del Me.set_keys[sk]
                break
        Me.set_keys[Me.currentkey]=(key,Me.ctx.tr.renderText(str(key)))
        Me.currentkey=Me.getunsetkey()
        if not Me.currentkey:
            cfg={k:str(v[0]) for k,v in Me.set_keys.items()}
            with open(Me.ctx.config_path,'w') as f:
                json.dump(cfg,f)
            Me.ctx.onFinish(BasicInput({v[0]:k for k,v in Me.set_keys.items()}))
    def onRender(Me,size,py):
        for i,key in enumerate(Me.ctx.keys):
            t=Me.t_keys[i]
            Me.ctx.tq.render(t,I.Vector2i((size.X>>1)-t.Size.X-4,py),size)
            if key in Me.set_keys:
                tk=Me.set_keys[key][1]
                Me.ctx.tq.render(tk,I.Vector2i((size.X>>1)+4,py),size)
            elif Me.currentkey==key:
                Me.ctx.tq.render(Me.t_presskey,I.Vector2i((size.X>>1)+4,py),size)           
            py+=t.Size.Y

class BasicInputConfigurator:
    def __init__(Me,onFinish):
        Me.tq=I.getTextureQuad()
        Me.t_title=I.getTextRenderer(64.0).renderText('⌨Basic Keyboard Config⌨')
        Me.tr=I.getTextRenderer(48.0)
        Me.keys=('Left','Right','Up','Down','Action','Back')
        Me.onFinish=onFinish
        os.makedirs(os.path.join(I.getConfigPath(),'Bodenwerder'), exist_ok=True) 
        Me.config_path=os.path.join(I.getConfigPath(),'Bodenwerder','keybinds.json')
        
        loaded_config=None
        if os.path.exists(Me.config_path):
            try:
                with open(Me.config_path,'r') as f:
                    lc=json.load(f)
                if 'Action' in lc:
                    for k in lc:
                        lc[k]=I.Keys[lc[k]]
                    loaded_config=lc
            except:
                pass
        
        if loaded_config:
            Me.state=ConfigStateConfirm(Me,loaded_config)
        else:
            Me.state=ConfigStateDefining(Me)
    def setState(Me,state):
        Me.state=state
    def onKeyDown(Me,key):
        Me.state.onKeyDown(key)
    def onRender(Me,size):
        Me.tq.render(Me.t_title,I.Vector2i((size.X-Me.t_title.Size.X)>>1,0),size)
        py=Me.t_title.Size.Y+4
        Me.state.onRender(size,py)