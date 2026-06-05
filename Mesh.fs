namespace Mesh

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

[<Struct>]
type AABB =
    val Min: Vector3
    val Max: Vector3
    new(min, max) = { Min = min; Max = max }
    static member FromPoints (pts: seq<Vector3>) =
        let mutable mi = Vector3(System.Single.PositiveInfinity)
        let mutable ma = Vector3(System.Single.NegativeInfinity)
        for p in pts do
            mi.X <- min mi.X p.X; mi.Y <- min mi.Y p.Y; mi.Z <- min mi.Z p.Z
            ma.X <- max ma.X p.X; ma.Y <- max ma.Y p.Y; ma.Z <- max ma.Z p.Z
        AABB(mi, ma)
    static member Merge (a: AABB) (b: AABB) =
        AABB(Vector3(min a.Min.X b.Min.X, min a.Min.Y b.Min.Y, min a.Min.Z b.Min.Z),
             Vector3(max a.Max.X b.Max.X, max a.Max.Y b.Max.Y, max a.Max.Z b.Max.Z))
    member Me.Intersects (other: AABB) =
        Me.Min.X <= other.Max.X && Me.Max.X >= other.Min.X &&
        Me.Min.Y <= other.Max.Y && Me.Max.Y >= other.Min.Y &&
        Me.Min.Z <= other.Max.Z && Me.Max.Z >= other.Min.Z

type AABBNode<'Data> =
    | AABBLeaf of AABB * HEFace<'Data>[]
    | AABBInternal of AABB * AABBNode<'Data> * AABBNode<'Data>
    member Me.Bounds = match Me with | AABBLeaf(b, _) -> b | AABBInternal(b, _, _) -> b

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

    member private Me.GetFaceVertices(face: HEFace<'Data>) =
        let pts = System.Collections.Generic.List<Vector3>()
        match face.Edge with
        | Some startEdge ->
            let mutable curr = startEdge
            let mutable loop = true
            while loop do
                match curr.Vertex with | Some v -> pts.Add(v.Position) | None -> ()
                match curr.Next with
                | Some next -> if next = startEdge then loop <- false else curr <- next
                | None -> loop <- false
        | None -> ()
        pts

    member Me.CreateAABBTree() =
        let faceData = 
            faces 
            |> Seq.map (fun f -> 
                let verts = Me.GetFaceVertices f
                let bounds = AABB.FromPoints verts
                let center = (bounds.Min + bounds.Max) * 0.5f
                (bounds, center, f))
            |> Seq.toArray

        let rec build (items: (AABB * Vector3 * HEFace<'Data>)[]) =
            let totalBounds = 
                if items.Length = 0 then AABB(Vector3.Zero, Vector3.Zero)
                else 
                    let mutable b = items.[0] |> (fun (x,_,_) -> x)
                    for i in 1 .. items.Length - 1 do
                        let (cur, _, _) = items.[i]
                        b <- AABB.Merge b cur
                    b
            
            if items.Length <= 4 then
                AABBLeaf(totalBounds, items |> Array.map (fun (_, _, f) -> f))
            else
                let size = totalBounds.Max - totalBounds.Min
                let axis = if size.X > size.Y && size.X > size.Z then 0 elif size.Y > size.Z then 1 else 2
                let sorted = 
                    match axis with 
                    | 0 -> items |> Array.sortBy (fun (_, c, _) -> c.X) 
                    | 1 -> items |> Array.sortBy (fun (_, c, _) -> c.Y) 
                    | _ -> items |> Array.sortBy (fun (_, c, _) -> c.Z)
                let mid = sorted.Length / 2
                AABBInternal(totalBounds, build sorted.[..mid-1], build sorted.[mid..])

        if faces.Count = 0 then None else Some (build faceData)

    member Me.CalculateVolume() =
        let mutable volume = 0.0
        for face in faces do
            let pts = Me.GetFaceVertices face
            if pts.Count >= 3 then
                let mutable areaSum = Vector3.Zero
                for i in 0 .. pts.Count - 1 do
                    let pCurrent = pts.[i]
                    let pNext = pts.[(i + 1) % pts.Count]
                    areaSum <- areaSum + Vector3.Cross(pCurrent, pNext)
                // Das vorzeichenbehaftete Volumen des Pyramiden-Segments zum Ursprung (Spatprodukt-Summe)
                volume <- volume + float (Vector3.Dot(pts.[0], areaSum))
        volume / 6.0

    member Me.Map<'NewData>(mapping: HEHalfEdge<'Data> -> 'NewData) : HalfEdgeMesh<'NewData> =
        let nm = HalfEdgeMesh<'NewData>()
        let vMap = System.Collections.Generic.Dictionary<HEVertex<'Data>, HEVertex<'NewData>>()
        let fMap = System.Collections.Generic.Dictionary<HEFace<'Data>, HEFace<'NewData>>()
        let eMap = System.Collections.Generic.Dictionary<HEHalfEdge<'Data>, HEHalfEdge<'NewData>>()

        for v in vertices do
            let nv = HEVertex<'NewData>(v.Position)
            vMap.[v] <- nv
            nm.Vertices.Add(nv)
        for f in faces do
            let nf = HEFace<'NewData>()
            fMap.[f] <- nf
            nm.Faces.Add(nf)
        for e in edges do
            let ne = HEHalfEdge<'NewData>()
            eMap.[e] <- ne
            nm.Edges.Add(ne)

        for e in edges do
            let ne = eMap.[e]
            ne.Data <- mapping e
            ne.Vertex <- e.Vertex |> Option.map (fun v -> vMap.[v])
            ne.Pair <- e.Pair |> Option.map (fun p -> eMap.[p])
            ne.Next <- e.Next |> Option.map (fun n -> eMap.[n])
            ne.Face <- e.Face |> Option.map (fun f -> fMap.[f])

        for v in vertices do vMap.[v].Edge <- v.Edge |> Option.map (fun e -> eMap.[e])
        for f in faces do fMap.[f].Edge <- f.Edge |> Option.map (fun e -> eMap.[e])
        nm

    static member FromSDF(min: Vector3, max: Vector3, res: Vector3i, sdf: Vector3 -> float32 * 'Data) : HalfEdgeMesh<'Data> =
        let dx = (max.X - min.X) / float32 (res.X - 1)
        let dy = (max.Y - min.Y) / float32 (res.Y - 1)
        let dz = (max.Z - min.Z) / float32 (res.Z - 1)
        let step = Vector3(dx, dy, dz)

        // 1. Raum abtasten
        let grid = Array3D.init res.X res.Y res.Z (fun i j k ->
            let p = min + Vector3(float32 i * dx, float32 j * dy, float32 k * dz)
            sdf p)

        let cellToIdx = System.Collections.Generic.Dictionary<int * int * int, int>()
        let coords = System.Collections.Generic.List<Vector3>()
        let cellData = System.Collections.Generic.List<'Data>()

        // 2. Vertices generieren (ein Vertex pro Zelle, die die Oberfläche schneidet)
        for i in 0 .. res.X - 2 do
            for j in 0 .. res.Y - 2 do
                for k in 0 .. res.Z - 2 do
                    let mutable mask = 0
                    let mutable vPos = Vector3.Zero
                    let mutable intersections = 0
                    let mutable minAbsDist = System.Single.MaxValue
                    let mutable bestData = Unchecked.defaultof<'Data>

                    // Prüfe die 8 Ecken der Zelle
                    for ci in 0..1 do
                        for cj in 0..1 do
                            for ck in 0..1 do
                                let d, data = grid.[i+ci, j+cj, k+ck]
                                if d < 0.0f then mask <- mask ||| (1 <<< (ci*4 + cj*2 + ck))
                                if abs d < minAbsDist then
                                    minAbsDist <- abs d
                                    bestData <- data

                    // Wenn die Zelle die Oberfläche schneidet (nicht alle Ecken gleiches Vorzeichen)
                    if mask <> 0 && mask <> 255 then
                        // Berechne Schnittpunkte auf den 12 Kanten der Zelle für bessere Platzierung
                        let cornerPos i j k = min + Vector3(float32 i * dx, float32 j * dy, float32 k * dz)
                        
                        let checkEdge (p1: Vector3) (idx1: Vector3i) (p2: Vector3) (idx2: Vector3i) =
                            let d1, _ = grid.[idx1.X, idx1.Y, idx1.Z]
                            let d2, _ = grid.[idx2.X, idx2.Y, idx2.Z]
                            if (d1 < 0.0f) <> (d2 < 0.0f) then
                                let t = -d1 / (d2 - d1)
                                vPos <- vPos + (p1 + t * (p2 - p1))
                                intersections <- intersections + 1

                        // Kanten in X, Y, Z Richtungen prüfen
                        for cj in 0..1 do for ck in 0..1 do checkEdge (cornerPos i (j+cj) (k+ck)) (Vector3i(i, j+cj, k+ck)) (cornerPos (i+1) (j+cj) (k+ck)) (Vector3i(i+1, j+cj, k+ck))
                        for ci in 0..1 do for ck in 0..1 do checkEdge (cornerPos (i+ci) j (k+ck)) (Vector3i(i+ci, j, k+ck)) (cornerPos (i+ci) (j+1) (k+ck)) (Vector3i(i+ci, j+1, k+ck))
                        for ci in 0..1 do for cj in 0..1 do checkEdge (cornerPos (i+ci) (j+cj) k) (Vector3i(i+ci, j+cj, k)) (cornerPos (i+ci) (j+cj) (k+1)) (Vector3i(i+ci, j+cj, k+1))

                        cellToIdx.[(i,j,k)] <- coords.Count
                        coords.Add(vPos / float32 intersections)
                        cellData.Add(bestData)

        // 3. Faces (Quads) generieren
        let faces = System.Collections.Generic.List<System.Collections.Generic.List<int * 'Data>>()
        
        let getV idx =
            match cellToIdx.TryGetValue(idx) with
            | true, ci -> Some (ci, cellData.[ci])
            | _ -> None

        for i in 0 .. res.X - 1 do
            for j in 0 .. res.Y - 1 do
                for k in 0 .. res.Z - 1 do
                    // X-axis edge crossing check
                    if i < res.X - 1 && j > 0 && k > 0 then
                        let d1, _ = grid.[i, j, k]
                        let d2, _ = grid.[i+1, j, k]
                        if (d1 < 0.0f) <> (d2 < 0.0f) then
                            let v = [ getV (i,j,k); getV (i,j-1,k); getV (i,j-1,k-1); getV (i,j,k-1) ]
                            if v |> List.forall Option.isSome then
                                let face = System.Collections.Generic.List()
                                let list = if d1 < 0.0f then List.rev v else v
                                for x in list do face.Add(x.Value)
                                faces.Add(face)

                    // Y-axis edge crossing check
                    if j < res.Y - 1 && i > 0 && k > 0 then
                        let d1, _ = grid.[i, j, k]
                        let d2, _ = grid.[i, j+1, k]
                        if (d1 < 0.0f) <> (d2 < 0.0f) then
                            let v = [ getV (i,j,k); getV (i-1,j,k); getV (i-1,j,k-1); getV (i,j,k-1) ]
                            if v |> List.forall Option.isSome then
                                let face = System.Collections.Generic.List()
                                let list = if d1 > 0.0f then List.rev v else v
                                for x in list do face.Add(x.Value)
                                faces.Add(face)

                    // Z-axis edge crossing check
                    if k < res.Z - 1 && i > 0 && j > 0 then
                        let d1, _ = grid.[i, j, k]
                        let d2, _ = grid.[i, j, k+1]
                        if (d1 < 0.0f) <> (d2 < 0.0f) then
                            let v = [ getV (i,j,k); getV (i,j-1,k); getV (i-1,j-1,k); getV (i-1,j,k) ]
                            if v |> List.forall Option.isSome then
                                let face = System.Collections.Generic.List()
                                let list = if d1 < 0.0f then List.rev v else v
                                for x in list do face.Add(x.Value)
                                faces.Add(face)

        let mesh = HalfEdgeMesh<'Data>()
        mesh.Build(coords, faces)
        mesh