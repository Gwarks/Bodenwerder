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
    member Me.createTexture(width:int, height:int, channels:int, texels:System.Collections.Generic.IList<byte>) =
        let texture = Engine.Texture()
        // IronPython übergibt 'bytes'/'bytearray' als IList<byte>.
        // Wir müssen es in ein für OpenGL passendes byte[] konvertieren.
        let arr = 
            match texels with
            | :? (byte[]) as a -> a // Falls es schon ein Array ist
            | _ -> System.Linq.Enumerable.ToArray(texels) // Ansonsten konvertieren
        texture.LoadFromByteArray(width, height, channels, arr)
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
                | :? (ActiveAttribType * int * int) as t -> t
                | :? System.Collections.Generic.IList<obj> as parts ->
                    let typ = parts.[0] :?> ActiveAttribType
                    let size = parts.[1] :?> int
                    let loc = parts.[2] :?> int
                    (typ, size, loc)
                | _ -> failwith "Invalid attribute format"
            ) |> List.ofSeq
        new Engine.VertexArray(attrList, arr, primetivetype)
    member Me.getConfigPath()=
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
    member Me.setBackgroundColor(color:Color4)=
        GL.ClearColor(color)
    member Me.setDepthTest(enable:bool)=
        if enable then GL.Enable(EnableCap.DepthTest) else GL.Disable(EnableCap.DepthTest)
    member Me.createMeshFromSDF(min: Vector3, max: Vector3, res: Vector3i, sdf: Func<Vector3, obj>) =
        let sdfWrapper (p: Vector3) =
            let result = sdf.Invoke(p) :?> System.Collections.Generic.IList<obj>
            (Convert.ToSingle(result.[0]), result.[1])
        Mesh.HalfEdgeMesh<obj>.FromSDF(min, max, res, sdfWrapper)
    member Me.PrimitiveType = Tools.TypeWrapper(typeof<PrimitiveType>)
    member Me.ShaderType = Tools.TypeWrapper(typeof<ShaderType>)
    member Me.Vector2i = Tools.TypeWrapper(typeof<Vector2i>)
    member Me.Vector3 = Tools.TypeWrapper(typeof<Vector3>)
    member Me.Vector3i = Tools.TypeWrapper(typeof<Vector3i>)
    member Me.Color4 = Tools.TypeWrapper(typeof<Color4>)
    member Me.Matrix4 = Tools.TypeWrapper(typeof<Matrix4>)
    member Me.Keys = (
        let d = PythonDictionary()
        for v in Enum.GetValues(typeof<Keys>) do
            let e = v :?> Keys
            d.[e.ToString()] <- e
        d)

let loadMain():Engine.EngineCallbacks=
    let engine=Python.CreateEngine()
    let dataPath = Path.Combine(Path.GetDirectoryName System.Environment.ProcessPath, "lt.cmdr.data")
    let searchPaths = engine.GetSearchPaths()
    searchPaths.Add(dataPath)
    engine.SetSearchPaths(searchPaths)

    let scope=engine.CreateScope()

    let sys = Python.GetSysModule(engine)
    let modules = sys.GetVariable<System.Collections.Generic.IDictionary<string, obj>>("modules")
    modules.["Interface"] <- Interface()

    engine.ExecuteFile(Path.Combine(dataPath, "main.py"), scope) |> ignore
    let onRender(size:Vector2i)=
        GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)            
        scope.GetVariable<Func<Vector2i,unit>>("onRender").Invoke(size)

    let onKeyDown(k:Keys)=
        match scope.TryGetVariable<Func<Keys,unit>>("onKeyDown") with
        | true, f -> f.Invoke(k)
        | _ -> ()
    let onKeyUp(k:Keys)=
        match scope.TryGetVariable<Func<Keys,unit>>("onKeyUp") with
        | true, f -> f.Invoke(k)
        | _ -> ()

    let onJoystickButtonDown id name button =
        match scope.TryGetVariable<Func<int,string,int,unit>>("onJoystickButtonDown") with
        | true, f -> f.Invoke(id, name, button)
        | _ -> ()

    let onJoystickButtonUp id name button =
        match scope.TryGetVariable<Func<int,string,int,unit>>("onJoystickButtonUp") with
        | true, f -> f.Invoke(id, name, button)
        | _ -> ()

    let onJoystickAxis id name axis value =
        match scope.TryGetVariable<Func<int,string,int,float32,unit>>("onJoystickAxis") with
        | true, f -> f.Invoke(id, name, axis, value)
        | _ -> ()

    { onRender = onRender; onKeyDown = onKeyDown; onKeyUp = onKeyUp;
      onJoystickButtonDown = onJoystickButtonDown; onJoystickButtonUp = onJoystickButtonUp;
      onJoystickAxis = onJoystickAxis}

let run():unit=
    let window=new Engine.Window()    
    window.Run(loadMain())
    GC.Collect()
    GC.WaitForPendingFinalizers()
    
