module Engine.Tools

open OpenTK.Graphics.OpenGL4

type AttribLayout =
    {
        Locations  : int
        Components : int
        BaseType   : VertexAttribPointerType
        IsInteger  : bool   // true -> VertexAttribIPointer
    }

let attribLayout (t : ActiveAttribType) : AttribLayout =
    let f comps =
        { Locations = 1; Components = comps; BaseType = VertexAttribPointerType.Float; IsInteger = false }

    let i comps =
        { Locations = 1; Components = comps; BaseType = VertexAttribPointerType.Int; IsInteger = true }

    let u comps =
        { Locations = 1; Components = comps; BaseType = VertexAttribPointerType.UnsignedInt; IsInteger = true }

    let mf cols rows =
        { Locations = cols; Components = rows; BaseType = VertexAttribPointerType.Float; IsInteger = false }

    match t with
    // float vectors
    | ActiveAttribType.Float     -> f 1
    | ActiveAttribType.FloatVec2 -> f 2
    | ActiveAttribType.FloatVec3 -> f 3
    | ActiveAttribType.FloatVec4 -> f 4

    // int vectors
    | ActiveAttribType.Int     -> i 1
    | ActiveAttribType.IntVec2 -> i 2
    | ActiveAttribType.IntVec3 -> i 3
    | ActiveAttribType.IntVec4 -> i 4

    // uint vectors
    | ActiveAttribType.UnsignedInt     -> u 1
    | ActiveAttribType.UnsignedIntVec2 -> u 2
    | ActiveAttribType.UnsignedIntVec3 -> u 3
    | ActiveAttribType.UnsignedIntVec4 -> u 4

    // matrices (columns = locations, rows = components)
    | ActiveAttribType.FloatMat2   -> mf 2 2
    | ActiveAttribType.FloatMat3   -> mf 3 3
    | ActiveAttribType.FloatMat4   -> mf 4 4

    | ActiveAttribType.FloatMat2x3 -> mf 2 3
    | ActiveAttribType.FloatMat2x4 -> mf 2 4
    | ActiveAttribType.FloatMat3x2 -> mf 3 2
    | ActiveAttribType.FloatMat3x4 -> mf 3 4
    | ActiveAttribType.FloatMat4x2 -> mf 4 2
    | ActiveAttribType.FloatMat4x3 -> mf 4 3

    | _ ->
        failwithf "Unsupported ActiveAttribType: %A" t