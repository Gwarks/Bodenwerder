import Interface as I
import input
import tests

I.setDepthTest(True)

I.setBackgroundColor(I.Color4(0.2,0.5,0,0))
bi=input.BasicInputConfigurator(lambda bi:tests.Menu(bi).activate())
I.setRenderer(bi.onRender)
I.setInputHandler(bi)