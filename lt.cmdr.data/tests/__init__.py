import os
import glob
import importlib
import Interface as I

tests=[importlib.import_module("." + os.path.basename(_f)[:-3], __package__)
       for _f in sorted(glob.glob(os.path.join(os.path.dirname(__file__), "t??.py")))]

class Menu:
  def __init__(Me,bi):    
    Me.cur=0
    tr=I.getTextRenderer(48.0)
    Me.options=[(tr.renderText(t.NAME),t) for t in tests]
    Me.tq=I.getTextureQuad()
    Me.bi=bi
    Me.ip=bi.getInputProcessor(Me)
    Me.t_selector=tr.renderText('🐑🦈')
  def activate(Me):
    I.setBackgroundColor(I.Color4(0.5,0.2,0.5,0))
    I.setRenderer(Me.onRender)
    I.setInputHandler(Me.ip)
  def onRender(Me,size):
    py=(size.Y-Me.t_selector.Size.Y)>>1
    Me.tq.render(Me.t_selector,I.Vector2i(size.X-Me.t_selector.Size.X,py),size)
    c=-(py//Me.t_selector.Size.Y)-1
    py+=c*Me.t_selector.Size.Y
    c+=Me.cur
    c%=len(Me.options)
    while(py<size.Y):
      o=Me.options[c][0]
      Me.tq.render(o,I.Vector2i(size.X-o.Size.X-Me.t_selector.Size.X,py),size)
      py+=Me.options[c][0].Size.Y
      c+=1
      c%=len(Me.options)
  def Up(Me):
    Me.cur+=1
    Me.cur%=len(Me.options)
  def Down(Me):
    Me.cur-=1
    Me.cur%=len(Me.options)
  def Action(Me):
    I.setBackgroundColor(I.Color4(0.6,0.9,1.0,0))
    test=Me.options[Me.cur][1].Test(Me.activate)
    I.setRenderer(test.onRender)
    I.setInputHandler(Me.bi.getInputProcessor(test))
  def Back(Me):
    I.closeEngine()