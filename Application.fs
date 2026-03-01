module Application

open System
open System.IO
open OpenTK.Graphics.OpenGL4
open OpenTK.Mathematics
open OpenTK.Windowing.GraphicsLibraryFramework
open IronPython.Hosting
open IronPython.Runtime

//type TextureQuad(texture,)=
    

type Interface()=
    member Me.getTextRenderer(size:float32):Engine.Canvas.TextRenderer=
        Engine.Canvas.TextRenderer(size)
    member Me.getTextureQuad()=
        Engine.Canvas.getTextureQuad()
    member this.PrintMessage(message: string)=
        printfn "F# object received message: %s" message
    member Me.createTextureRGBA(width:int, height:int, texels:System.Collections.Generic.IList<byte>) =
        let texture = Engine.Texture()
        // IronPython übergibt 'bytes'/'bytearray' als IList<byte>.
        // Wir müssen es in ein für OpenGL passendes byte[] konvertieren.
        let arr = 
            match texels with
            | :? (byte[]) as a -> a // Falls es schon ein Array ist
            | _ -> System.Linq.Enumerable.ToArray(texels) // Ansonsten konvertieren
        texture.RGBAfromByteArray(width, height, arr)
        texture
    member Me.createShaderProgram(shaders: System.Collections.Generic.IList<obj>) =
        let shaderList =
            shaders
            |> Seq.map (fun item ->
                // IronPython übergibt Python-Tupel/Listen als IList<obj>
                let parts = item :?> System.Collections.Generic.IList<obj>
                let src = parts.[0] :?> string
                let typ = parts.[1] :?> ShaderType
                (src, typ)
            ) |> List.ofSeq
        new Engine.ShaderProgram(shaderList)
    member Me.createVertexArray(attributes: System.Collections.IEnumerable, vertexs:System.Collections.Generic.IList<byte>,primetivetype:PrimitiveType) =
        let arr = 
            match vertexs with
            | :? (byte[]) as a -> a // Falls es schon ein Array ist
            | _ -> System.Linq.Enumerable.ToArray(vertexs) // Ansonsten konvertieren
        let attrList =
            attributes
            |> Seq.cast<obj>
            |> Seq.map (fun item ->
                match item with
                | :? (string * ActiveAttribType * int * int) as t -> t
                | :? System.Collections.Generic.IList<obj> as parts ->
                    let name = parts.[0] :?> string
                    let typ = parts.[1] :?> ActiveAttribType
                    let size = parts.[2] :?> int
                    let loc = parts.[3] :?> int
                    (name, typ, size, loc)
                | _ -> failwith "Invalid attribute format"
            ) |> List.ofSeq
        new Engine.VertexArray(attrList, arr, primetivetype)
    member Me.getConfigPath()=
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
    member Me.ShaderType = (
        let d = PythonDictionary()
        for v in Enum.GetValues(typeof<ShaderType>) do
            let e = v :?> ShaderType
            d.[e.ToString()] <- e
        d)
    member Me.PrimitiveType = (
        let d = PythonDictionary()
        for v in Enum.GetValues(typeof<PrimitiveType>) do
            let e: PrimitiveType = v :?> PrimitiveType
            d.[e.ToString()] <- e
        d)
    member Me.Vector2i = typeof<Vector2i>

let testPython():Engine.EngineCallbacks=
    let engine=Python.CreateEngine()
    let scope=engine.CreateScope()

    let sys = Python.GetSysModule(engine)
    let modules = sys.GetVariable<System.Collections.Generic.IDictionary<string, obj>>("modules")
    modules.["Interface"] <- Interface()

    engine.ExecuteFile(Path.Combine(Path.GetDirectoryName System.Environment.ProcessPath,"lt.cmdr.data","main.py"),scope)|>ignore
    let onRender(size:Vector2i)=
        GL.ClearColor(Color4.CornflowerBlue)
        GL.Clear(ClearBufferMask.ColorBufferBit)    
        scope.GetVariable<Func<Vector2i,unit>>("onRender").Invoke(size)

    let pyOnKeyDown = scope.GetVariable<Func<Keys,unit>>("onKeyDown")
    let onKeyDown(k:Keys)=
        scope.GetVariable<Func<Keys,unit>>("onKeyDown").Invoke(k)

    { onRender = onRender; onKeyDown = onKeyDown }

let run():unit=
    let window=new Engine.Window()    
    window.Run(testPython())
    GC.Collect()
    GC.WaitForPendingFinalizers()
    
