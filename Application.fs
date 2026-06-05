module Application

open System
open System.IO
open OpenTK.Graphics.OpenGL4
open OpenTK.Mathematics
open OpenTK.Windowing.GraphicsLibraryFramework
open IronPython.Hosting
open IronPython.Runtime
open Microsoft.Scripting.Hosting

//type TextureQuad(texture,)=
    

type Interface(window: Engine.Window, engine: Microsoft.Scripting.Hosting.ScriptEngine)=
    let mutable currentOnRender: obj = null
    let mutable currentInputHandler: obj = null
    
    member Me.setRenderer(onRender: obj) =
        let old = currentOnRender
        currentOnRender <- onRender
        old

    member Me.setInputHandler(inputHandler: obj) =
        let old = currentInputHandler
        currentInputHandler <- inputHandler
        old
    member Me.closeEngine()=
        window.Close()

    member internal Me.FormatPyError(ex: Exception) =
        let eo = engine.GetService<ExceptionOperations>()
        eo.FormatException(ex)

    member internal Me.DoRender(size: Vector2i) =
        if not (isNull currentOnRender) then
            try engine.Operations.Invoke(currentOnRender, size) |> ignore
            with ex -> 
                printfn "Critical Python Exception in onRender:\n%s" (Me.FormatPyError ex)
                window.Close()

    member internal Me.DoInput(funcName: string, args: obj[]) =
        if not (isNull currentInputHandler) then
            try
                let m = engine.Operations.GetMember(currentInputHandler, funcName)
                if engine.Operations.IsCallable(m) then engine.Operations.Invoke(m, args) |> ignore
            with ex -> 
                printfn "Critical Python Exception in %s:\n%s" funcName (Me.FormatPyError ex)
                window.Close()

    member Me.createSurfaceRGBA(width:int, height:int)=
        Engine.Canvas.createSurfaceRGBA(width,height)
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

    member Me.CSGintersection(meshA: Mesh.HalfEdgeMesh<obj>, meshB: Mesh.HalfEdgeMesh<obj>, dataFunc: Func<obj, obj, float32, obj>) =
        let df (d1: obj) (d2: obj) (t: float32) = dataFunc.Invoke(d1, d2, t)
        Mesh.CSG.intersection df meshA meshB
    member Me.CSGunion(meshA: Mesh.HalfEdgeMesh<obj>, meshB: Mesh.HalfEdgeMesh<obj>, dataFunc: Func<obj, obj, float32, obj>) =
        let df (d1: obj) (d2: obj) (t: float32) = dataFunc.Invoke(d1, d2, t)
        Mesh.CSG.union df meshA meshB
    member Me.CSGsubstraction(meshA: Mesh.HalfEdgeMesh<obj>, meshB: Mesh.HalfEdgeMesh<obj>, dataFunc: Func<obj, obj, float32, obj>) =
        let df (d1: obj) (d2: obj) (t: float32) = dataFunc.Invoke(d1, d2, t)
        Mesh.CSG.subtraction df meshA meshB
    member Me.createCubeMesh(center: Vector3, size: Vector3, data: obj) =
        let h = size * 0.5f
        let coords = [|
            center + Vector3(-h.X, -h.Y, -h.Z) // 0
            center + Vector3( h.X, -h.Y, -h.Z) // 1
            center + Vector3( h.X,  h.Y, -h.Z) // 2
            center + Vector3(-h.X,  h.Y, -h.Z) // 3
            center + Vector3(-h.X, -h.Y,  h.Z) // 4
            center + Vector3( h.X, -h.Y,  h.Z) // 5
            center + Vector3( h.X,  h.Y,  h.Z) // 6
            center + Vector3(-h.X,  h.Y,  h.Z) // 7
        |]
        // Jede Fläche ist ein Quad (4 Indizes), CCW von außen gesehen
        let faces = [|
            [| (4, data); (5, data); (6, data); (7, data) |] :> seq<int * obj> // Vorne (+Z)
            [| (1, data); (0, data); (3, data); (2, data) |] :> seq<int * obj> // Hinten (-Z)
            [| (1, data); (5, data); (6, data); (2, data) |] :> seq<int * obj> // Rechts (+X)
            [| (4, data); (0, data); (3, data); (7, data) |] :> seq<int * obj> // Links (-X)
            [| (3, data); (2, data); (6, data); (7, data) |] :> seq<int * obj> // Oben (+Y)
            [| (0, data); (1, data); (5, data); (4, data) |] :> seq<int * obj> // Unten (-Y)
        |]
        let mesh = Mesh.HalfEdgeMesh<obj>()
        mesh.Build(coords, faces)
        mesh
    member Me.getJoysticks() =
        window.GetJoystickInfos()
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

let runMain()=
    let window=new Engine.Window()
    let engine=Python.CreateEngine()
    let dataPath = Path.Combine(Path.GetDirectoryName System.Environment.ProcessPath, "lt.cmdr.data")
    let searchPaths = engine.GetSearchPaths()
    searchPaths.Add(dataPath)
    engine.SetSearchPaths(searchPaths)

    let scope=engine.CreateScope()
    let interfaceInstance = Interface(window, engine)

    let sys = Python.GetSysModule(engine)
    let modules = sys.GetVariable<System.Collections.Generic.IDictionary<string, obj>>("modules")
    modules.["Interface"] <- interfaceInstance

    try
        engine.ExecuteFile(Path.Combine(dataPath, "main.py"), scope) |> ignore
    with ex ->
        printfn "Fatal error loading main.py:\n%s" (interfaceInstance.FormatPyError ex)
        exit 1

    let onRender(size:Vector2i)=
        GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)            
        interfaceInstance.DoRender(size)

    let onKeyDown k = interfaceInstance.DoInput("onKeyDown", [| k |])
    let onKeyUp k = interfaceInstance.DoInput("onKeyUp", [| k |])
    let onJoystickButtonDown id name btn = interfaceInstance.DoInput("onJoystickButtonDown", [| id; name; btn |])
    let onJoystickButtonUp id name btn = interfaceInstance.DoInput("onJoystickButtonUp", [| id; name; btn |])
    let onJoystickAxis id name axis value = interfaceInstance.DoInput("onJoystickAxis", [| id; name; axis; value |])

    window.Run({ onRender = onRender; onKeyDown = onKeyDown; onKeyUp = onKeyUp;
      onJoystickButtonDown = onJoystickButtonDown; onJoystickButtonUp = onJoystickButtonUp;
      onJoystickAxis = onJoystickAxis})

let run():unit=
    runMain()
    GC.Collect()
    GC.WaitForPendingFinalizers()
    
