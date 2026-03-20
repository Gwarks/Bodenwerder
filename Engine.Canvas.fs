module Engine.Canvas

open OpenTK.Graphics.OpenGL4
open OpenTK.Mathematics
open SkiaSharp
open System.IO
   
let private image2texture(image:SKImage):Texture=
    let texData = Array.zeroCreate<byte> (image.Width * image.Height * 4)
    let pinned =
        System.Runtime.InteropServices.GCHandle.Alloc(texData, System.Runtime.InteropServices.GCHandleType.Pinned)
    if not (image.ReadPixels(image.Info, pinned.AddrOfPinnedObject(), image.Width * 4, 0, 0)) then
        printfn "ARRAY FAILED"
    pinned.Free()
    let texture = Texture()
    texture.LoadFromByteArray(image.Width, image.Height, 4, texData)
    texture

(*
    let data = image.Encode(SKEncodedImageFormat.Png, 100)
    let stream = File.OpenWrite("output.png")
    data.SaveTo(stream)
    *)

type private NotoFont()=
    let typefaceLatin=SKTypeface.FromFile(Path.Combine(Path.GetDirectoryName System.Environment.ProcessPath,"lt.cmdr.data","NotoSans-Regular.ttf"))
    let typefaceEmoji=SKTypeface.FromFile(Path.Combine(Path.GetDirectoryName System.Environment.ProcessPath,"lt.cmdr.data","NotoColorEmoji.ttf"))
    member Me.TypefaceLatin=typefaceLatin
    member Me.TypefaceEmoji=typefaceEmoji
let private getNotoFont=Tools.WeakSingleton.get<NotoFont>

type TextRenderer(size:float32) =
    let fontLatin = new SKFont(Size = size, Typeface = getNotoFont().TypefaceLatin)
    let fontEmoji = new SKFont(Size = size, Typeface = getNotoFont().TypefaceEmoji)
    let paint = new SKPaint(Color = SKColors.White, IsAntialias = true)
    member Me.renderText(text:string):Texture=
        let mutable bounds = SKRect.Empty
        let glyphsLatin = getNotoFont().TypefaceLatin.GetGlyphs(text)
        let glyphsEmoji = getNotoFont().TypefaceEmoji.GetGlyphs(text)
        let glyphs = Array.zip glyphsLatin glyphsEmoji
        let top = max -fontLatin.Metrics.Top -fontEmoji.Metrics.Top
        let bottom = max fontLatin.Metrics.Bottom fontEmoji.Metrics.Bottom
        let height = ceil (top + bottom)
        let mutable width = 0f
        let blobbuilder = new SKTextBlobBuilder()
        let mutable pos = 0f

        for a, b in glyphs do
            if a > 0us then
                let m = fontLatin.MeasureText([| a |], &bounds, paint)
                blobbuilder.AddHorizontalRun(System.ReadOnlySpan [| a |], fontLatin, System.ReadOnlySpan [| width |], top)
                width <- width + m
            else if b > 0us then
                let m = fontEmoji.MeasureText([| b |], &bounds, paint)
                blobbuilder.AddHorizontalRun(System.ReadOnlySpan [| b |], fontEmoji, System.ReadOnlySpan [| width |], top)
                width <- width + m

        let blob = blobbuilder.Build()
        let info = SKImageInfo(int width, int height, SKColorType.Rgba8888)
        let surface = SKSurface.Create(info)

        let canvas = surface.Canvas
        canvas.Clear(SKColors.Black)
        canvas.DrawText(blob, 0f, 0f, paint)
        image2texture(surface.Snapshot())

type TextureQuad()=
    let shaderProgram = ShaderProgram ([
        ("""
#version 330 core
layout (location = 0) in vec2 aPosition;
out vec2 TexCoord;
uniform vec2 base;
uniform vec2 delta;
void main()
{
    //gl_Position = vec4(aPosition*vec2(2.0,-2.0)+vec2(-1.0,1.0), 0.0, 1.0);
    gl_Position = vec4(aPosition*delta+base, 0.0, 1.0);
    TexCoord = aPosition;
}
""",ShaderType.VertexShader)
        ("""
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D texture1;
void main()
{
    FragColor = texture(texture1, TexCoord);
}
""",ShaderType.FragmentShader)
        ])
    let va =
        let v = [|
               0.0f ; 0.0f // Top-Left
               0.0f ; 1.0f // Bottom-left
               1.0f ; 1.0f // Bottom-right
               1.0f ; 0.0f // Top-right
               |]
        let b = Array.zeroCreate<byte> (v.Length * sizeof<float32>)
        System.Buffer.BlockCopy(v, 0, b, 0, b.Length)
        VertexArray(shaderProgram.getActiveAttributes().Values |> Seq.toList,b,PrimitiveType.TriangleFan)
    member public Me.render (texture:Texture,pos:Vector2i,client:Vector2i) =
        shaderProgram.activate()
        GL.Uniform2(shaderProgram.UniformLocation("base"),((float32 pos.X)*2.0f)/(float32 client.X)-1.0f,1.0f-((float32 pos.Y)*2.0f)/(float32 client.Y));
        GL.Uniform2(shaderProgram.UniformLocation("delta"),((float32 texture.Size.X)*2.0f)/(float32 client.X),((float32 texture.Size.Y)*(-2.0f))/(float32 client.Y));
        texture.activate(0)
        shaderProgram.SetUniform("texture1", 0)
        va.draw()        
let getTextureQuad=Tools.WeakSingleton.get<TextureQuad>
