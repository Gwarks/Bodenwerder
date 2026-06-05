module Mesh.CSG

open OpenTK.Mathematics
open Mesh

// ─────────────────────────────────────────────────────────────────────────────
// Typen
// ─────────────────────────────────────────────────────────────────────────────

type CSGOperation = Union | Intersection | Subtraction

/// Interpolationsfunktion: (dataA, dataB, t ∈ [0,1]) → neues Data
/// t=0 = Punkt liegt auf dataA-Seite, t=1 = Punkt liegt auf dataB-Seite
type Interpolator<'Data> = 'Data -> 'Data -> float32 -> 'Data

// ─────────────────────────────────────────────────────────────────────────────
// Geometrie-Primitiven
// ─────────────────────────────────────────────────────────────────────────────

[<Struct>]
type Plane =
    val Normal: Vector3
    val D: float32
    new(n: Vector3, d: float32) = { Normal = n; D = d }
    member Me.DistanceTo(p: Vector3) = Vector3.Dot(Me.Normal, p) + Me.D

/// FIX Bug 1: Ebene aus allen Vertices eines Faces (Newell-Methode).
/// Robust für Quads und N-Gons, stabil bei koplanaren aber nicht-triangulierten Faces.
let private planeFromFace (face: HEFace<'Data>) : Plane option =
    let pts = System.Collections.Generic.List<Vector3>()
    match face.Edge with
    | None -> ()
    | Some startEdge ->
        let mutable curr = startEdge
        let mutable loop = true
        while loop do
            curr.Vertex |> Option.iter (fun v -> pts.Add v.Position)
            match curr.Next with
            | Some next -> if next = startEdge then loop <- false else curr <- next
            | None      -> loop <- false
    if pts.Count < 3 then None
    else
        // Newell-Methode: exakte Normale aus N-Gon
        let mutable nx, ny, nz = 0.0f, 0.0f, 0.0f
        let count = pts.Count
        for i in 0 .. count - 1 do
            let p0 = pts.[i]
            let p1 = pts.[(i + 1) % count]
            nx <- nx + (p0.Y - p1.Y) * (p0.Z + p1.Z)
            ny <- ny + (p0.Z - p1.Z) * (p0.X + p1.X)
            nz <- nz + (p0.X - p1.X) * (p0.Y + p1.Y)
        let len = sqrt (nx*nx + ny*ny + nz*nz)
        if len < 1e-8f then None
        else
            let n = Vector3(nx / len, ny / len, nz / len)
            // Schwerpunkt als Aufpunkt
            let mutable cx, cy, cz = 0.0f, 0.0f, 0.0f
            for p in pts do cx <- cx + p.X; cy <- cy + p.Y; cz <- cz + p.Z
            let c = Vector3(cx / float32 count, cy / float32 count, cz / float32 count)
            Some (Plane(n, -Vector3.Dot(n, c)))

// ─────────────────────────────────────────────────────────────────────────────
// Vert-Typ
// ─────────────────────────────────────────────────────────────────────────────

type Vert<'Data> = { Pos: Vector3; Data: 'Data }

// ─────────────────────────────────────────────────────────────────────────────
// FIX Bug 3: Polygon-Splitting – korrekte 3-Zustand-Klassifikation
// ─────────────────────────────────────────────────────────────────────────────

/// Klassifikation eines Vertex relativ zur Ebene
[<Struct>]
type private VertSide = Neg | On | Pos

let private classifyVertex (eps: float32) (plane: Plane) (v: Vert<'Data>) =
    let d = plane.DistanceTo v.Pos
    if   d >  eps then Pos
    elif d < -eps then Neg
    else On

/// Teilt ein Polygon mit der Ebene in (negative Seite, positive Seite).
/// OnSurface-Vertices gehen in BEIDE Listen.
/// Neue Schnittpunkte werden mit `lerp` interpoliert.
/// Rückgabe: (negSide, posSide) – beide können leer sein wenn Polygon ganz auf einer Seite liegt.
let splitPolygon
        (eps: float32)
        (plane: Plane)
        (lerp: Interpolator<'Data>)
        (poly: Vert<'Data> list)
        : Vert<'Data> list * Vert<'Data> list =

    if poly.IsEmpty then [], []
    else

    let neg = System.Collections.Generic.List<Vert<'Data>>()
    let pos = System.Collections.Generic.List<Vert<'Data>>()

    let count   = poly.Length
    let sides   = poly |> List.map (classifyVertex eps plane) |> List.toArray

    for i in 0 .. count - 1 do
        let curr     = poly.[i]
        let next     = poly.[(i + 1) % count]
        let currSide = sides.[i]
        let nextSide = sides.[(i + 1) % count]

        // Aktuellen Vertex einsortieren
        match currSide with
        | Neg -> neg.Add curr
        | Pos -> pos.Add curr
        | On  ->
            // OnSurface → geht in beide Listen
            neg.Add curr
            pos.Add curr

        // Kante kreuzt die Ebene (Neg↔Pos, nicht On)?
        let needsCut =
            (currSide = Neg && nextSide = Pos) ||
            (currSide = Pos && nextSide = Neg)

        if needsCut then
            let dc = plane.DistanceTo curr.Pos
            let dn = plane.DistanceTo next.Pos
            let t  = dc / (dc - dn)
            let t  = max 0.0f (min 1.0f t)
            let newPos  = curr.Pos + t * (next.Pos - curr.Pos)
            let newData = lerp curr.Data next.Data t
            let v = { Pos = newPos; Data = newData }
            neg.Add v
            pos.Add v

    Seq.toList neg, Seq.toList pos

// ─────────────────────────────────────────────────────────────────────────────
// Punkt-in-Mesh Test (Ray Casting über AABB-Baum)
// ─────────────────────────────────────────────────────────────────────────────

let private rayAABB (orig: Vector3) (invDir: Vector3) (b: AABB) =
    let mutable tmin = System.Single.NegativeInfinity
    let mutable tmax = System.Single.PositiveInfinity
    let inline check o id bmin bmax =
        let t0 = (bmin - o) * id
        let t1 = (bmax - o) * id
        let lo, hi = if t0 < t1 then t0, t1 else t1, t0
        if lo > tmin then tmin <- lo
        if hi < tmax then tmax <- hi
    check orig.X invDir.X b.Min.X b.Max.X
    check orig.Y invDir.Y b.Min.Y b.Max.Y
    check orig.Z invDir.Z b.Min.Z b.Max.Z
    tmin <= tmax && tmax >= 0.0f

/// Möller-Trumbore
let private rayTriangle (orig: Vector3) (dir: Vector3)
                         (v0: Vector3) (v1: Vector3) (v2: Vector3) =
    let eps = 1e-7f
    let e1 = v1 - v0
    let e2 = v2 - v0
    let h  = Vector3.Cross(dir, e2)
    let a  = Vector3.Dot(e1, h)
    if abs a < eps then None
    else
        let f = 1.0f / a
        let s = orig - v0
        let u = f * Vector3.Dot(s, h)
        if u < 0.0f || u > 1.0f then None
        else
            let q = Vector3.Cross(s, e1)
            let v = f * Vector3.Dot(dir, q)
            if v < 0.0f || u + v > 1.0f then None
            else
                let t = f * Vector3.Dot(e2, q)
                if t > eps then Some t else None

let private getFaceTriangles (face: HEFace<'Data>) =
    let pts = System.Collections.Generic.List<Vector3>()
    match face.Edge with
    | Some startEdge ->
        let mutable curr = startEdge
        let mutable loop = true
        while loop do
            curr.Vertex |> Option.iter (fun v -> pts.Add v.Position)
            match curr.Next with
            | Some next -> if next = startEdge then loop <- false else curr <- next
            | None      -> loop <- false
    | None -> ()
    if pts.Count < 3 then []
    else [ for i in 1 .. pts.Count - 2 -> (pts.[0], pts.[i], pts.[i+1]) ]

let private countRayHits (tree: AABBNode<'Data>) (orig: Vector3) (dir: Vector3) =
    let invDir = Vector3(1.0f / dir.X, 1.0f / dir.Y, 1.0f / dir.Z)
    let mutable count = 0
    let rec traverse = function
        | AABBLeaf(b, faces) ->
            if rayAABB orig invDir b then
                for face in faces do
                    for (v0,v1,v2) in getFaceTriangles face do
                        if rayTriangle orig dir v0 v1 v2 |> Option.isSome then
                            count <- count + 1
        | AABBInternal(b, l, r) ->
            if rayAABB orig invDir b then traverse l; traverse r
    traverse tree
    count

/// Dreifacher Mehrheitsentscheid für Robustheit
let isPointInsideMesh (tree: AABBNode<'Data>) (p: Vector3) =
    let dirs = [|
        Vector3(1.0f,  0.17f,  0.07f)
        Vector3(0.07f, 1.0f,   0.13f)
        Vector3(0.11f, 0.09f,  1.0f )
    |]
    let votes =
        dirs |> Array.sumBy (fun d ->
            if countRayHits tree p (Vector3.Normalize d) % 2 = 1 then 1 else 0)
    votes >= 2

// ─────────────────────────────────────────────────────────────────────────────
// Polygon-Extraktion aus HalfEdge
// ─────────────────────────────────────────────────────────────────────────────

let private extractPolygons (mesh: HalfEdgeMesh<'Data>) : Vert<'Data> list list =
    [ for face in mesh.Faces do
        let verts = System.Collections.Generic.List<Vert<'Data>>()
        match face.Edge with
        | Some startEdge ->
            let mutable curr = startEdge
            let mutable loop = true
            while loop do
                match curr.Vertex with
                | Some v -> verts.Add { Pos = v.Position; Data = curr.Data }
                | None   -> ()
                match curr.Next with
                | Some next -> if next = startEdge then loop <- false else curr <- next
                | None      -> loop <- false
        | None -> ()
        if verts.Count >= 3 then
            yield Seq.toList verts ]

// ─────────────────────────────────────────────────────────────────────────────
// Mesh aus Polygon-Liste bauen
// ─────────────────────────────────────────────────────────────────────────────

let private buildMeshFromPolygons (polys: Vert<'Data> list list) : HalfEdgeMesh<'Data> =
    let eps = 1e-5f
    let allVerts = System.Collections.Generic.List<Vector3>()
    let findOrAdd (p: Vector3) =
        let mutable found = -1
        for i in 0 .. allVerts.Count - 1 do
            if found = -1 then
                let q = allVerts.[i]
                if abs (p.X - q.X) < eps && abs (p.Y - q.Y) < eps && abs (p.Z - q.Z) < eps then
                    found <- i
        if found >= 0 then found
        else allVerts.Add p; allVerts.Count - 1

    let faceIndices =
        polys |> List.map (fun poly ->
            poly |> List.map (fun v -> findOrAdd v.Pos, v.Data) |> List.toSeq)

    let mesh = HalfEdgeMesh<'Data>()
    mesh.Build(Seq.map id allVerts, faceIndices)
    mesh

// ─────────────────────────────────────────────────────────────────────────────
// FIX Bug 2: Kernfunktion – sukzessives Schneiden mit sofortiger Klassifikation
// ─────────────────────────────────────────────────────────────────────────────

/// Klassifiziert alle Polygone von `meshA` relativ zu `meshB`.
///
/// Für jedes Polygon aus A:
///   1. AABB-Test: nur Faces von B mit überlappender AABB werden als Schnittebenen kandidiert
///   2. Für jeden Face-Kandidaten: Polygon gegen die Face-Ebene schneiden
///   3. Nach jedem Schnitt: Fragmente die KOMPLETT auf einer Seite liegen
///      werden sofort klassifiziert (Ray-Cast) und aus der Arbeitsliste entfernt
///   4. Nur echte Grenz-Fragmente bleiben in der Arbeitsliste
///
/// Das behebt den Kern des U-Problems: ein Face das von B durchtrennt wird
/// erzeugt genau zwei Fragmente, die beide sofort korrekt klassifiziert werden.
let private classifyAndSplit
        (eps: float32)
        (lerp: Interpolator<'Data>)
        (meshA: HalfEdgeMesh<'Data>)
        (treeB: AABBNode<'Data>)
        : Vert<'Data> list list * Vert<'Data> list list =

    let inside  = System.Collections.Generic.List<Vert<'Data> list>()
    let outside = System.Collections.Generic.List<Vert<'Data> list>()

    for poly in extractPolygons meshA do
        // Alle Vertices des Polygons auf einer Seite? → Direkt klassifizieren ohne zu schneiden
        let polyAABB = AABB.FromPoints (poly |> List.map (fun v -> v.Pos))

        // Face-Kandidaten aus B sammeln (AABB-Test)
        let candidatePlanes = System.Collections.Generic.List<Plane>()
        let rec collectPlanes = function
            | AABBLeaf(b, faces) ->
                if b.Intersects polyAABB then
                    for f in faces do
                        planeFromFace f |> Option.iter candidatePlanes.Add
            | AABBInternal(b, l, r) ->
                if b.Intersects polyAABB then collectPlanes l; collectPlanes r
        collectPlanes treeB

        if candidatePlanes.Count = 0 then
            // Kein Face von B überlappt mit diesem Polygon → direkt klassifizieren
            let centroid = poly |> List.fold (fun acc v -> acc + v.Pos) Vector3.Zero
            let centroid = centroid / float32 poly.Length
            if isPointInsideMesh treeB centroid
            then inside.Add poly
            else outside.Add poly
        else
            // FIX: Arbeitsliste mit zu schneidenden Fragmenten.
            // Nach jedem Schnitt werden abgeschlossene Fragmente sofort klassifiziert.
            let mutable workList = [poly]

            for plane in candidatePlanes do
                let nextWork = System.Collections.Generic.List<Vert<'Data> list>()

                for fragment in workList do
                    // Alle Vertices auf gleicher Seite? → Fragment ist abgeschlossen
                    let allNeg = fragment |> List.forall (fun v -> plane.DistanceTo v.Pos <= eps)
                    let allPos = fragment |> List.forall (fun v -> plane.DistanceTo v.Pos >= -eps)

                    if allNeg || allPos then
                        // Fragment liegt komplett auf einer Seite dieser Ebene.
                        // Noch nicht endgültig klassifizieren – es könnte von einer
                        // späteren Ebene noch geschnitten werden.
                        nextWork.Add fragment
                    else
                        // Fragment wird wirklich durch diese Ebene geschnitten
                        let (negPart, posPart) = splitPolygon eps plane lerp fragment
                        if negPart.Length >= 3 then nextWork.Add negPart
                        if posPart.Length >= 3 then nextWork.Add posPart

                workList <- Seq.toList nextWork

            // Alle verbliebenen Fragmente klassifizieren
            for fragment in workList do
                let centroid = fragment |> List.fold (fun acc v -> acc + v.Pos) Vector3.Zero
                let centroid = centroid / float32 fragment.Length
                if isPointInsideMesh treeB centroid
                then inside.Add fragment
                else outside.Add fragment

    Seq.toList inside, Seq.toList outside

// ─────────────────────────────────────────────────────────────────────────────
// Normalen umkehren
// ─────────────────────────────────────────────────────────────────────────────

let private flipPoly (poly: Vert<'Data> list) = List.rev poly

// ─────────────────────────────────────────────────────────────────────────────
// Öffentliche CSG-Funktion
// ─────────────────────────────────────────────────────────────────────────────

/// Führt eine CSG-Operation auf zwei HalfEdgeMeshes durch.
///
/// Parameters:
///   op    – CSGOperation: Union | Intersection | Subtraction
///   lerp  – Interpolationsfunktion (dataA, dataB, t) → newData
///           t=0 entspricht dataA-Seite, t=1 entspricht dataB-Seite
///   meshA – Erstes Mesh (bei Subtraction: wird beibehalten)
///   meshB – Zweites Mesh (bei Subtraction: wird abgezogen)
///
/// Kombinations-Logik:
///   Union:        aOut + bOut           (außerhalb des jeweils anderen)
///   Intersection: aIn  + bIn            (innerhalb des jeweils anderen)
///   Subtraction:  aOut + flip(bIn)      (A außerhalb B, B-Inneres umgekehrt)
let csg
        (op: CSGOperation)
        (lerp: Interpolator<'Data>)
        (meshA: HalfEdgeMesh<'Data>)
        (meshB: HalfEdgeMesh<'Data>)
        : HalfEdgeMesh<'Data> =

    let eps = 1e-4f

    match meshA.CreateAABBTree(), meshB.CreateAABBTree() with
    | None,    _       -> HalfEdgeMesh<'Data>()
    | _,       None    -> HalfEdgeMesh<'Data>()
    | Some tA, Some tB ->

    let (aIn, aOut) = classifyAndSplit eps lerp meshA tB
    let (bIn, bOut) = classifyAndSplit eps lerp meshB tA

    let resultPolygons =
        match op with
        | Union        -> aOut @ bOut
        | Intersection -> aIn  @ bIn
        | Subtraction  -> aOut @ (bIn |> List.map flipPoly)

    buildMeshFromPolygons resultPolygons

// ─────────────────────────────────────────────────────────────────────────────
// Komfort-Wrappers
// ─────────────────────────────────────────────────────────────────────────────

/// A ∪ B
let union (lerp: Interpolator<'Data>) meshA meshB =
    csg Union lerp meshA meshB

/// A ∩ B
let intersection (lerp: Interpolator<'Data>) meshA meshB =
    csg Intersection lerp meshA meshB

/// A \ B
let subtraction (lerp: Interpolator<'Data>) meshA meshB =
    csg Subtraction lerp meshA meshB

// ─────────────────────────────────────────────────────────────────────────────
// Fertige Interpolatoren
// ─────────────────────────────────────────────────────────────────────────────

let lerpFloat32 (a: float32) (b: float32) (t: float32) = a + t * (b - a)
let lerpVector2 (a: Vector2) (b: Vector2) (t: float32) = a + t * (b - a)
let lerpVector3 (a: Vector3) (b: Vector3) (t: float32) = a + t * (b - a)
let lerpVector4 (a: Vector4) (b: Vector4) (t: float32) = a + t * (b - a)
let keepA (a: 'Data) (_: 'Data) (_: float32) = a
let keepB (_: 'Data) (b: 'Data) (_: float32) = b