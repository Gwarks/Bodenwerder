module Engine.Canvas

open OpenTK.Windowing.Desktop
open OpenTK.Windowing.Common
open OpenTK.Graphics.OpenGL4
open OpenTK.Mathematics
open SkiaSharp
open System.IO
open System.ComponentModel
open System.Threading
open Microsoft.FSharp.Math

let private typefaceLatin=SKTypeface.FromFile(Path.Combine(Path.GetDirectoryName System.Environment.ProcessPath,"lt.cmdr.data","NotoSans-Regular.ttf"))
let private typefaceEmoji=SKTypeface.FromFile(Path.Combine(Path.GetDirectoryName System.Environment.ProcessPath,"lt.cmdr.data","NotoColorEmoji.ttf"))
let private fontLatin = new SKFont(Size = 256.0f, Typeface = typefaceLatin)
let private fontEmoji = new SKFont(Size = 256.0f, Typeface = typefaceEmoji)
let private paint = new SKPaint(Color = SKColors.White, IsAntialias = true)

let private renderText(text:string):SKImage=
    let mutable bounds = SKRect.Empty
    let glyphsLatin = typefaceLatin.GetGlyphs(text)
    let glyphsEmoji = typefaceEmoji.GetGlyphs(text)
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
    surface.Snapshot()
    
let private image2texture(image:SKImage):Texture=
    let texData = Array.zeroCreate<byte> (image.Width * image.Height * 4)
    let pinned =
        System.Runtime.InteropServices.GCHandle.Alloc(texData, System.Runtime.InteropServices.GCHandleType.Pinned)
    if not (image.ReadPixels(image.Info, pinned.AddrOfPinnedObject(), image.Width * 4, 0, 0)) then
        printfn "ARRAY FAILED"
    pinned.Free()
    let texture = Texture()
    texture.RGBAfromByteArray(image.Width,image.Height,texData)
    texture

(*
    let image = surface.Snapshot()
    let data = image.Encode(SKEncodedImageFormat.Png, 100)
    let stream = File.OpenWrite("output.png")
    data.SaveTo(stream)
    *)

// Vertex shader: passes position and texture coordinates to the fragment shader.
let vertexShaderSource =
    """
#version 330 core
layout (location = 0) in vec2 aPosition;
out vec2 TexCoord;
void main()
{
    gl_Position = vec4(aPosition*vec2(1.0,-1.0)+vec2(-0.5,0.5), 0.0, 1.0);
    TexCoord = aPosition;
}
"""

// Fragment shader: samples a texture.
let fragmentShaderSource =
    """
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D texture1;
void main()
{
    FragColor = texture(texture1, TexCoord);
}
"""

// Define a triangle with both position (x, y, z) and texture coordinates (u, v)
let vertices =
    [|
       0.0f ; 0.0f // Top-Left
       0.0f ; 1.0f // Bottom-left
       1.0f ; 1.0f // Bottom-right
       1.0f ; 0.0f // Top-right
       |]

let mutable shaderProgram:ShaderProgram option=None
let mutable vao = 0
let mutable vbo = 0
let mutable texture:Texture option=None

let init()=
    shaderProgram <- Some(ShaderProgram ([
        vertexShaderSource,ShaderType.VertexShader
        fragmentShaderSource,ShaderType.FragmentShader
        ]))

    vao <- GL.GenVertexArray()
    vbo <- GL.GenBuffer()
    GL.BindVertexArray(vao)
    GL.BindBuffer(BufferTarget.ArrayBuffer, vbo)
    GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof<float32>, vertices, BufferUsageHint.StaticDraw)
    GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof<float32>, 0)
    GL.EnableVertexAttribArray(0)
    GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
    GL.BindVertexArray(0)
    texture <- Some(image2texture(renderText ("🦈:Ɲaɗoƴa:🐑")))

let onRender () =
    GL.ClearColor(Color4.CornflowerBlue)
    GL.Clear(ClearBufferMask.ColorBufferBit)
    match shaderProgram with
        |Some(s) ->
            match texture with
                |Some(t) ->
                    s.activate()            
                    t.activate()
                    GL.BindVertexArray(vao)
                    GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4)        
                |None ->()
        |None ->()

//type SimpleText()=
//    do
//       
//    member Me.render()=
        

// Clean up all resources.
//GL.DeleteBuffer(vbo)
//GL.DeleteVertexArray(vao)
