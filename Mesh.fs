module Mesh

open OpenTK.Mathematics

type HEVertex<'Data>(position: Vector3) =
    member val Position = position with get, set
    member val Edge: HEHalfEdge<'Data> option = None with get, set

and HEFace<'Data>() =
    member val Edge: HEHalfEdge<'Data> option = None with get, set

and HEHalfEdge<'Data>() =
    member val Vertex: HEVertex<'Data> option = None with get, set
    member val Pair: HEHalfEdge<'Data> option = None with get, set
    member val Next: HEHalfEdge<'Data> option = None with get, set
    member val Face: HEFace<'Data> option = None with get, set
    member val Data: 'Data = Unchecked.defaultof<'Data> with get, set

type HalfEdgeMesh<'Data>() =
    let vertices = System.Collections.Generic.List<HEVertex<'Data>>()
    let faces = System.Collections.Generic.List<HEFace<'Data>>()
    let edges = System.Collections.Generic.List<HEHalfEdge<'Data>>()

    member Me.Vertices = vertices
    member Me.Faces = faces
    member Me.Edges = edges

    member Me.Build(coords: seq<Vector3>, indices: seq<#seq<int * 'Data>>) =
        vertices.Clear()
        faces.Clear()
        edges.Clear()
        
        for p in coords do
            vertices.Add(HEVertex(p))

        let edgeMap = System.Collections.Generic.Dictionary<int * int, HEHalfEdge<'Data>>()
        
        for faceIndices in indices do
            let face = HEFace()
            faces.Add(face)
            
            let fInds = Seq.toList faceIndices
            let count = fInds.Length
            let faceEdges = System.Collections.Generic.List<HEHalfEdge<'Data>>()
            
            for i in 0 .. count - 1 do
                let (u, _) = fInds.[i]
                let (v, vData) = fInds.[(i + 1) % count]
                
                let he = HEHalfEdge()
                he.Vertex <- Some vertices.[v]
                he.Face <- Some face
                he.Data <- vData
                
                if vertices.[u].Edge.IsNone then
                    vertices.[u].Edge <- Some he
                
                faceEdges.Add(he)
                edges.Add(he)
                
                if not (edgeMap.ContainsKey((u,v))) then
                    edgeMap.Add((u,v), he)
            
            for i in 0 .. count - 1 do
                faceEdges.[i].Next <- Some faceEdges.[(i + 1) % count]
            
            if faceEdges.Count > 0 then
                face.Edge <- Some faceEdges.[0]

        for kvp in edgeMap do
            let (u, v) = kvp.Key
            if edgeMap.ContainsKey((v, u)) then
                kvp.Value.Pair <- Some edgeMap.[(v, u)]

    member Me.Triangulate() =
        seq {
            for face in faces do
                match face.Edge with
                | Some startEdge ->
                    let poly = System.Collections.Generic.List()
                    let mutable curr = startEdge
                    let mutable loop = true
                    let mutable safe = 0
                    while loop && safe < 10000 do
                        safe <- safe + 1
                        match curr.Vertex with
                        | Some v -> poly.Add((v.Position, curr.Data))
                        | None -> ()
                        
                        match curr.Next with
                        | Some next ->
                            if next = startEdge then loop <- false else curr <- next
                        | None -> loop <- false

                    if poly.Count >= 3 then
                        let count = poly.Count
                        let mutable nx, ny, nz = 0.0f, 0.0f, 0.0f
                        for i in 0 .. count - 1 do
                            let (p0, _) = poly.[i]
                            let (p1, _) = poly.[(i + 1) % count]
                            nx <- nx + (p0.Y - p1.Y) * (p0.Z + p1.Z)
                            ny <- ny + (p0.Z - p1.Z) * (p0.X + p1.X)
                            nz <- nz + (p0.X - p1.X) * (p0.Y + p1.Y)
                        
                        let points2D = System.Collections.Generic.List<int * Vector2>()
                        let ax, ay, az = abs nx, abs ny, abs nz
                        if ax > ay && ax > az then
                            for i in 0 .. count - 1 do let (p, _) = poly.[i] in points2D.Add((i, Vector2(p.Y, p.Z)))
                        else if ay > az then
                            for i in 0 .. count - 1 do let (p, _) = poly.[i] in points2D.Add((i, Vector2(p.X, p.Z)))
                        else
                            for i in 0 .. count - 1 do let (p, _) = poly.[i] in points2D.Add((i, Vector2(p.X, p.Y)))
                            
                        let mutable area = 0.0f
                        for i in 0 .. count - 1 do
                             let (_, p1) = points2D.[i]
                             let (_, p2) = points2D.[(i + 1) % count]
                             area <- area + (p1.X * p2.Y - p2.X * p1.Y)
                        if area < 0.0f then points2D.Reverse()

                        let mutable remaining = points2D.Count
                        let mutable iterations = 0
                        while remaining >= 3 && iterations < count * count do
                            iterations <- iterations + 1
                            let mutable earFound = false
                            let mutable i = 0
                            while not earFound && i < remaining do
                                let prevIdx, nextIdx = (i + remaining - 1) % remaining, (i + 1) % remaining
                                let (_, pPrev), (_, pCurr), (_, pNext) = points2D.[prevIdx], points2D.[i], points2D.[nextIdx]
                                let edge1, edge2 = pCurr - pPrev, pNext - pCurr
                                if (edge1.X * edge2.Y - edge1.Y * edge2.X) > -1e-5f then
                                    let mutable containsPoint = false
                                    for k in 0 .. remaining - 1 do
                                        if not containsPoint && k <> prevIdx && k <> i && k <> nextIdx then
                                            let (_, pK) = points2D.[k]
                                            let v0, v1, v2 = pNext - pPrev, pCurr - pPrev, pK - pPrev
                                            let d00, d01, d02 = Vector2.Dot(v0, v0), Vector2.Dot(v0, v1), Vector2.Dot(v0, v2)
                                            let d11, d12 = Vector2.Dot(v1, v1), Vector2.Dot(v1, v2)
                                            let invDenom = 1.0f / (d00 * d11 - d01 * d01)
                                            let u, v = (d11 * d02 - d01 * d12) * invDenom, (d00 * d12 - d01 * d02) * invDenom
                                            if (u >= 0.0f) && (v >= 0.0f) && (u + v < 1.0f) then containsPoint <- true
                                    if not containsPoint then
                                        let (idx0, _), (idx1, _), (idx2, _) = points2D.[prevIdx], points2D.[i], points2D.[nextIdx]
                                        let (v0, d0), (v1, d1), (v2, d2) = poly.[idx0], poly.[idx1], poly.[idx2]
                                        yield (v0.X, v0.Y, v0.Z, d0)
                                        yield (v1.X, v1.Y, v1.Z, d1)
                                        yield (v2.X, v2.Y, v2.Z, d2)
                                        points2D.RemoveAt(i)
                                        remaining <- remaining - 1
                                        earFound <- true
                                i <- i + 1
                | None -> ()
        }
