import Interface as I
import json
import os
from pathlib import Path

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
        os.makedirs(os.path.join(I.getConfigPath(),'Bodenwerder'), exist_ok=True) 
        Me.config_path=os.path.join(I.getConfigPath(),'Bodenwerder','keybinds.json')
        Me.state='CONFIGURING'
        Me.loaded_config=None
        if os.path.exists(Me.config_path):
            try:
                with open(Me.config_path,'r') as f:
                    Me.loaded_config=json.load(f)
                if 'Action' in Me.loaded_config:
                    for k in Me.loaded_config:
                        Me.loaded_config[k]=I.Keys[Me.loaded_config[k]]
                    Me.state='CONFIRM'
                    Me.t_msg1=Me.tr.renderText('Config found. Press [Action]')
                    Me.t_msg2=Me.tr.renderText('to load or any key to reset.')
                    Me.t_conf={k:Me.tr.renderText(f"{k}: {v}") for k,v in Me.loaded_config.items()}
            except:
                pass
    def getunsetkey(Me):        
        for key in Me.keys:
            if key not in Me.set_keys:
                return key
    def onKeyDown(Me,key):
        if Me.state=='CONFIRM':
            if key==Me.loaded_config['Action']:
                Me.onFinish(BasicInput({v:k for k,v in Me.loaded_config.items()}))
            else:
                Me.state='CONFIGURING'
            return
        if not Me.currentkey:
            return
        for sk in list(Me.set_keys):
            if Me.set_keys[sk][0]==key:
                del Me.set_keys[sk]
                break
        Me.set_keys[Me.currentkey]=(key,Me.tr.renderText(str(key)))
        Me.currentkey=Me.getunsetkey()
        if not Me.currentkey:
            cfg={k:str(v[0]) for k,v in Me.set_keys.items()}
            with open(Me.config_path,'w') as f:
                json.dump(cfg,f)
            Me.onFinish(BasicInput({v[0]:k for k,v in Me.set_keys.items()}))
    def onRender(Me,size):
        Me.tq.render(Me.t_title,I.Vector2i((size.X-Me.t_title.Size.X)>>1,0),size)
        py=Me.t_title.Size.Y+4
        if Me.state=='CONFIRM':
            Me.tq.render(Me.t_msg1,I.Vector2i((size.X-Me.t_msg1.Size.X)>>1,py),size)
            py+=Me.t_msg1.Size.Y
            Me.tq.render(Me.t_msg2,I.Vector2i((size.X-Me.t_msg2.Size.X)>>1,py),size)
            py+=Me.t_msg2.Size.Y+8
            for k in Me.keys:
                if k in Me.t_conf:
                    t=Me.t_conf[k]
                    Me.tq.render(t,I.Vector2i((size.X-t.Size.X)>>1,py),size)
                    py+=t.Size.Y
            return
        for i,key in enumerate(Me.keys):
            t=Me.t_keys[i]
            Me.tq.render(t,I.Vector2i((size.X>>1)-t.Size.X-4,py),size)
            if key in Me.set_keys:
                tk=Me.set_keys[key][1]
                Me.tq.render(tk,I.Vector2i((size.X>>1)+4,py),size)
            elif Me.currentkey==key:
                Me.tq.render(Me.t_presskey,I.Vector2i((size.X>>1)+4,py),size)           
            py+=t.Size.Y      