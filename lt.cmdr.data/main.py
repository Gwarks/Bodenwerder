import Interface as I
import input
import tests

I.setDepthTest(True)

I.setBackgroundColor(I.Color4(0.2,0.5,0,0))
def onCompleteConfig(bi):
    global onRender,InputHandler
    test=tests.t00.Test()
    onRender=test.onRender
    InputHandler=bi.getInputProcessor(test) 
    I.setBackgroundColor(I.Color4(0.6,0.9,1,0))
bi=input.BasicInputConfigurator(onCompleteConfig)

InputHandler = bi
onRender = bi.onRender