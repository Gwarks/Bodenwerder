namespace Engine

open OpenTK.Windowing.Desktop
open OpenTK.Windowing.Common
open OpenTK.Graphics.OpenGL4
open OpenTK.Mathematics
open System.ComponentModel
open System.Threading
open OpenTK.Windowing.GraphicsLibraryFramework

type ShaderProgram(shaderz:list<string*ShaderType>)=
    let mutable shaderProgram=0
    do 
        shaderProgram<-GL.CreateProgram()
        for src,typ in shaderz do
            let shader=GL.CreateShader(typ)
            GL.ShaderSource(shader,src)
            GL.CompileShader(shader)
            let mutable compileStatus=0
            GL.GetShader(shader,ShaderParameter.CompileStatus,&compileStatus)
            if compileStatus<>1 then
                let info = GL.GetShaderInfoLog(shader)
                failwith (sprintf "Shader compilation failed. Type: %A\nLog: %s" typ info)
            GL.AttachShader(shaderProgram,shader)
            GL.LinkProgram(shaderProgram)
            GL.DeleteShader(shader)
        let mutable linkStatus=0
        GL.GetProgram(shaderProgram,GetProgramParameterName.LinkStatus,&linkStatus)
        if linkStatus<>1 then
            let info = GL.GetProgramInfoLog(shaderProgram)
            failwith (sprintf "Shader program linking failed.\nLog: %s" info)
    member Me.activate():unit=
        GL.UseProgram(shaderProgram)
    member Me.UniformLocation(name:string):int=
         GL.GetUniformLocation(shaderProgram,name)
    member Me.SetUniform(name:string, value:int) = GL.Uniform1(Me.UniformLocation(name), value)
    member Me.SetUniform(name:string, value:float32) = GL.Uniform1(Me.UniformLocation(name), value)
    member Me.SetUniform(name:string, value:Vector2) = GL.Uniform2(Me.UniformLocation(name), value)
    member Me.SetUniform(name:string, value:Vector3) = GL.Uniform3(Me.UniformLocation(name), value)
    member Me.SetUniform(name:string, value:Vector4) = GL.Uniform4(Me.UniformLocation(name), value)
    member Me.SetUniform(name:string, value:Matrix4) = 
        let mutable m = value
        GL.UniformMatrix4(Me.UniformLocation(name), false, &m)
    member Me.getActiveUniforms() : (string * ActiveUniformType * int) list =
        let mutable count = 0
        GL.GetProgram(shaderProgram, GetProgramParameterName.ActiveUniforms, &count)
        [
            for i in 0 .. count - 1 do
                let mutable size = 0
                let mutable typ = ActiveUniformType.Bool
                let name = GL.GetActiveUniform(shaderProgram, i, &size, &typ)
                (name, typ, size)
        ]
    member Me.getActiveAttributes() : (string * ActiveAttribType * int * int) list =
        let mutable count = 0
        GL.GetProgram(shaderProgram, GetProgramParameterName.ActiveAttributes, &count)
        [
            for i in 0 .. count - 1 do
                let mutable size = 0
                let mutable typ = ActiveAttribType.Float
                let name = GL.GetActiveAttrib(shaderProgram, i, &size, &typ)
                let location = GL.GetAttribLocation(shaderProgram, name)
                (name, typ, size, location)
        ]
    override Me.Finalize()=
        GL.DeleteProgram(shaderProgram)

type Texture()=
    let mutable texture = 0
    let mutable size = Vector2i(0)
    do
        texture<-GL.GenTexture()
        GL.BindTexture(TextureTarget.Texture2D, texture)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, int All.Repeat)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, int All.Repeat)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, int All.Nearest)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, int All.Nearest)
    member Me.Size=size
    member Me.RGBAfromByteArray(width:int,height:int,texels:array<byte>)=
        GL.BindTexture(TextureTarget.Texture2D, texture)
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1)
        GL.TexImage2D(
            TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba,
            width,
            height,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            texels
        )
        size.X<-width
        size.Y<-height
    member Me.activate():unit=
        GL.BindTexture(TextureTarget.Texture2D, texture)
    override Me.Finalize()=
        GL.DeleteTexture(texture)

type VertexArray(attributes:(string * ActiveAttribType * int * int) list, vertexs:System.Collections.Generic.IList<byte>,
        primitivetype:PrimitiveType)=
    let mutable vao=0
    let mutable vbo=0
    let mutable stride=0
    let mutable vertexCount=0
    do
        let vertexs = 
            match vertexs with
            | :? (byte[]) as a -> a
            | _ -> System.Linq.Enumerable.ToArray(vertexs)
        vao <- GL.GenVertexArray()
        vbo <- GL.GenBuffer()
        GL.BindVertexArray(vao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo)
        
        for (_,typ,_,_) in attributes do
            let l = Tools.attribLayout typ
            stride <- stride + (l.Locations * l.Components * 4)
        vertexCount <-vertexs.Length / stride

        GL.BufferData(BufferTarget.ArrayBuffer, vertexs.Length, vertexs, BufferUsageHint.StaticDraw)
        
        let mutable offset = 0
        for (_,typ,_,location) in attributes do
            let l = Tools.attribLayout typ
            if location >= 0 then
                for i in 0 .. l.Locations - 1 do
                    if l.IsInteger then
                        GL.VertexAttribIPointer(location + i, l.Components, enum<VertexAttribIntegerType> (int l.BaseType), stride, nativeint offset)
                    else
                        GL.VertexAttribPointer(location + i, l.Components, l.BaseType, false, stride, offset)
                    GL.EnableVertexAttribArray(location)
                    offset <- offset + (l.Components * 4)
        
        GL.BindVertexArray(0)        
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
    member Me.draw():unit=
        GL.BindVertexArray(vao)
        GL.DrawArrays(primitivetype, 0, vertexCount)
        GL.BindVertexArray(0)
    override Me.Finalize()=
        GL.DeleteBuffer(vbo)
        GL.DeleteVertexArray(vao)

type EngineCallbacks = {
    onRender: Vector2i -> unit
    onKeyDown: Keys -> unit
}

type Window()=
    inherit NativeWindow(NativeWindowSettings(Title = "Mein maximiertes OpenTK-Fenster",
                                            ClientSize = Vector2i(512, 256),  // Startgröße (wird ignoriert bei Maximized)
                                            WindowState = WindowState.Maximized,  // Das macht es maximized!
                                            WindowBorder = WindowBorder.Resizable))
    let mutable isRunning=true
    let mutable onKeyDownHandler: (Keys -> unit) option = None
    do
        base.Context.MakeCurrent()
    member Me.Run(callbacks: EngineCallbacks) =
        onKeyDownHandler <- Some callbacks.onKeyDown
        while isRunning do
            NativeWindow.ProcessWindowEvents(true)
            GL.Clear(ClearBufferMask.ColorBufferBit|||ClearBufferMask.DepthBufferBit)
            callbacks.onRender(Me.ClientSize)
            Thread.Sleep(1)
            Me.Context.SwapBuffers()
    override Me.OnResize(e: ResizeEventArgs)=
        GL.Viewport(0,0,e.Width,e.Height)
        base.OnResize(e)
    override this.OnKeyDown(e: KeyboardKeyEventArgs) =
        base.OnKeyDown(e)
        match onKeyDownHandler with
        | Some handler -> handler e.Key
        | None -> ()
    override Me.OnClosing(e:CancelEventArgs)=
        isRunning<-false
        base.OnClosing(e)
