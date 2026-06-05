module Mesh.CSG

open OpenTK.Mathematics
open Mesh

// ─────────────────────────────────────────────────────────────────────────────
// Typen
// ─────────────────────────────────────────────────────────────────────────────

/// Klassifizierung eines Punktes relativ zur Schnittebene / zum anderen Mesh
type PointClass = Inside | Outside | OnSurface

/// CSG-Operation
type CSGOperation = Union | Intersection | Subtraction

/// Interpolationsfunktion: gegebene zwei Datenpunkte und t ∈ [0,1] → neues Data
type Interpolator<'Data> = 'Data -> 'Data -> float32 -> 'Data

// ─────────────────────────────────────────────────────────────────────────────
// Hilfsfunktionen – Geometrie
// ─────────────────────────────────────────────────────────────────────────────

[<Struct>]
type Plane =
    val Normal: Vector3
    val D: float32
    new(n: Vector3, d: float32) = { Normal = n; D = d }
    static member FromTriangle(a: Vector3, b: Vector3, c: Vector3) =
        let n = Vector3.Normalize(Vector3.Cross(b - a, c - a))
        Plane(n, -Vector3.Dot(n, a))
    member Me.DistanceTo(p: Vector3) = Vector3.Dot(Me.Normal, p) + Me.D

/// Klassifiziert einen Punkt zur Ebene (mit Epsilon-Toleranz)
let classifyPoint (eps: float32) (plane: Plane) (p: Vector3) =
    let d = plane.DistanceTo p
    if   d >  eps then Outside
    elif d < -eps then Inside
    else OnSurface

/// Schneidet Segment [a,b] mit Ebene; gibt t zurück sodass P = a + t*(b-a)
let intersectSegmentPlane (plane: Plane) (a: Vector3) (b: Vector3) =
    let da = plane.DistanceTo a
    let db = plane.DistanceTo b
    let denom = da - db
    if abs denom < 1e-6f then None
    else Some (da / denom)

/// Berechnet die Flächennormale eines Polygons (Newell-Methode)
let faceNormal (pts: Vector3 list) : Vector3 =
    let mutable n = Vector3.Zero
    let count = List.length pts
    for i in 0 .. count - 1 do
        let p0 = pts.[i]
        let p1 = pts.[(i + 1) % count]
        n.X <- n.X + (p0.Y - p1.Y) * (p0.Z + p1.Z)
        n.Y <- n.Y + (p0.Z - p1.Z) * (p0.X + p1.X)
        n.Z <- n.Z + (p0.X - p1.X) * (p0.Y + p1.Y)
    Vector3.Normalize n

// ─────────────────────────────────────────────────────────────────────────────
// Polygon-Schnitt gegen eine Ebene (Sutherland–Hodgman für eine Ebene)
// ─────────────────────────────────────────────────────────────────────────────

/// Ein Vertex mit beliebigen Daten
type Vert<'Data> = { Pos: Vector3; Data: 'Data }

/// Teilt ein Polygon mit der Ebene in (innen, außen).
/// "Innen" = auf der negativen Seite der Ebene.
/// Neue Punkte werden mit `lerp` interpoliert.
let splitPolygon
        (eps: float32)
        (plane: Plane)
        (lerp: Interpolator<'Data>)
        (poly: Vert<'Data> list)
        : Vert<'Data> list * Vert<'Data> list =   // (inside, outside)

    if poly.IsEmpty then [], []
    else

    let inside  = System.Collections.Generic.List<Vert<'Data>>()
    let outside = System.Collections.Generic.List<Vert<'Data>>()

    let count = poly.Length

    for i in 0 .. count - 1 do
        let curr = poly.[i]
        let next = poly.[(i + 1) % count]

        let dc = plane.DistanceTo curr.Pos
        let dn = plane.DistanceTo next.Pos

        let currIn = dc <= eps
        let nextIn = dn <= eps

        if currIn then inside.Add curr
        else            outside.Add curr

        // Kante kreuzt die Ebene → Schnittpunkt einfügen
        let crosses =
            (dc > eps && dn < -eps) ||   // outside → inside
            (dc < -eps && dn > eps)      // inside  → outside
        if crosses then
            let t = dc / (dc - dn)
            let t = max 0.0f (min 1.0f t)
            let newPos  = curr.Pos  + t * (next.Pos  - curr.Pos)
            let newData = lerp curr.Data next.Data t
            let v = { Pos = newPos; Data = newData }
            inside.Add v
            outside.Add v

    Seq.toList inside, Seq.toList outside

// ─────────────────────────────────────────────────────────────────────────────
// Punkt-in-Mesh-Test (Ray-Casting gegen AABB-Baum)
// ─────────────────────────────────────────────────────────────────────────────

/// Prüft ob ein Strahl die AABB schneidet
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

/// Moller-Trumbore Raycast gegen Dreieck; gibt t zurück wenn getroffen
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

/// Trianguliert ein Face-Polygon direkt aus der HE-Struktur
let private getFaceTriangles (face: HEFace<'Data>) : (Vector3 * Vector3 * Vector3) list =
    let pts = System.Collections.Generic.List<Vector3>()
    match face.Edge with
    | Some startEdge ->
        let mutable curr = startEdge
        let mutable loop = true
        while loop do
            curr.Vertex |> Option.iter (fun v -> pts.Add v.Position)
            match curr.Next with
            | Some next -> if next = startEdge then loop <- false else curr <- next
            | None -> loop <- false
    | None -> ()
    if pts.Count < 3 then []
    else [ for i in 1 .. pts.Count - 2 -> (pts.[0], pts.[i], pts.[i+1]) ]

/// Zählt Schnittpunkte eines Strahls mit dem Mesh (via AABB-Baum)
let private countRayIntersections
        (tree: AABBNode<'Data>)
        (orig: Vector3)
        (dir: Vector3) =

    let invDir = Vector3(1.0f / dir.X, 1.0f / dir.Y, 1.0f / dir.Z)
    let mutable count = 0

    let rec traverse node =
        match node with
        | AABBLeaf(b, faces) ->
            if rayAABB orig invDir b then
                for face in faces do
                    for (v0, v1, v2) in getFaceTriangles face do
                        if rayTriangle orig dir v0 v1 v2 |> Option.isSome then
                            count <- count + 1
        | AABBInternal(b, left, right) ->
            if rayAABB orig invDir b then
                traverse left
                traverse right

    traverse tree
    count

/// Klassifiziert ob ein Punkt innerhalb des Meshes liegt
/// Drei Strahlen in verschiedene Richtungen abstimmen (Mehrheitsentscheid)
let isPointInsideMesh (tree: AABBNode<'Data>) (p: Vector3) =
    let dirs = [|
        Vector3(1.0f, 0.0f, 0.0f)
        Vector3(0.0f, 1.0f, 0.0f)
        Vector3(0.0f, 0.0f, 1.0f)
    |]
    let votes =
        dirs
        |> Array.sumBy (fun d ->
            if countRayIntersections tree p d % 2 = 1 then 1 else 0)
    votes >= 2

// ─────────────────────────────────────────────────────────────────────────────
// Polygon-Listen aus HalfEdge extrahieren
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
    // Vertices deduplizieren mit Epsilon
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
        else
            allVerts.Add p
            allVerts.Count - 1

    let faceIndices =
        polys |> List.map (fun poly ->
            poly |> List.map (fun v ->
                let idx = findOrAdd v.Pos
                (idx, v.Data))
            |> List.toSeq
        )

    let mesh = HalfEdgeMesh<'Data>()
    mesh.Build(Seq.map id allVerts, faceIndices)
    mesh

// ─────────────────────────────────────────────────────────────────────────────
// Kern-CSG: Polygone eines Meshes gegen das andere Mesh klassifizieren
// ─────────────────────────────────────────────────────────────────────────────

/// Klassifiziert alle Polygone von `meshA` relativ zu `meshB`.
/// Polygone die die Grenze von B schneiden werden aufgeteilt.
/// Rückgabe: (innen, außen) - Listen von Polygonen
let private classifyAndSplit
        (eps: float32)
        (lerp: Interpolator<'Data>)
        (meshA: HalfEdgeMesh<'Data>)
        (treeB: AABBNode<'Data>)
        (meshB: HalfEdgeMesh<'Data>)
        : Vert<'Data> list list * Vert<'Data> list list =

    let inside  = System.Collections.Generic.List<Vert<'Data> list>()
    let outside = System.Collections.Generic.List<Vert<'Data> list>()

    let polygonsA = extractPolygons meshA

    // AABB-Baum von B für schnelle Face-Tests
    let rec collectFacesFromTree node =
        match node with
        | AABBLeaf(_, faces) -> Seq.toList faces
        | AABBInternal(_, l, r) -> collectFacesFromTree l @ collectFacesFromTree r

    for poly in polygonsA do
        if poly.IsEmpty then ()
        else

        // Schritt 1: Prüfe ob das Polygon komplett klassifiziert werden kann
        // indem wir den Schwerpunkt gegen treeB testen
        let centroid =
            let sum = poly |> List.fold (fun acc v -> acc + v.Pos) Vector3.Zero
            sum / float32 poly.Length

        // Schritt 2: Schneide das Polygon nur gegen Faces von B,
        // deren AABB das Polygon-AABB überlappt (nutzt HE-Nachbarschaft)
        let polyAABB =
            let positions = poly |> List.map (fun v -> v.Pos)
            AABB.FromPoints positions

        // Alle Faces von B finden deren AABB die Polygon-AABB schneidet
        let invDir = Vector3(0.0f, 0.0f, 1.0f) // dummy
        let relevantFaces = System.Collections.Generic.List<HEFace<'Data>>()
        let rec collectRelevant node =
            match node with
            | AABBLeaf(b, faces) ->
                if b.Intersects polyAABB then
                    for f in faces do relevantFaces.Add f
            | AABBInternal(b, l, r) ->
                if b.Intersects polyAABB then
                    collectRelevant l
                    collectRelevant r
        collectRelevant treeB

        // Schritt 3: Sukzessiv gegen alle relevanten Ebenen schneiden
        // Dabei wird "inside" die Teile die IN B liegen sammeln
        // Starte mit dem ganzen Polygon als "zu verarbeiten"
        let mutable toProcess = [poly]
        let mutable processedIn  = []
        let mutable processedOut = []

        for face in relevantFaces do
            let tris = getFaceTriangles face
            if not tris.IsEmpty then
                let (v0, v1, v2) = tris.[0]   // Ebene aus erstem Dreieck
                let plane = Plane.FromTriangle(v0, v1, v2)

                let nextToProcess = System.Collections.Generic.List<Vert<'Data> list>()
                for p in toProcess do
                    let (pin, pout) = splitPolygon eps plane lerp p
                    if pin.Length  >= 3 then nextToProcess.Add pin
                    if pout.Length >= 3 then nextToProcess.Add pout
                toProcess <- Seq.toList nextToProcess

        // Schritt 4: Klassifiziere verbliebene Polygone per Ray-Cast
        for p in toProcess do
            let c = p |> List.fold (fun acc v -> acc + v.Pos) Vector3.Zero
            let c = c / float32 p.Length
            if isPointInsideMesh treeB c then
                processedIn  <- p :: processedIn
            else
                processedOut <- p :: processedOut

        inside.AddRange  processedIn
        outside.AddRange processedOut

    Seq.toList inside, Seq.toList outside

// ─────────────────────────────────────────────────────────────────────────────
// Normalen umkehren
// ─────────────────────────────────────────────────────────────────────────────

/// Kehrt die Wicklungsreihenfolge eines Polygons um (→ Normale umkehren)
let private flipPoly (poly: Vert<'Data> list) = List.rev poly

// ─────────────────────────────────────────────────────────────────────────────
// Öffentliche CSG-Funktion
// ─────────────────────────────────────────────────────────────────────────────

/// Führt eine CSG-Operation auf zwei HalfEdgeMeshes durch.
///
/// Parameters:
///   op      – CSGOperation: Union | Intersection | Subtraction
///   lerp    – Interpolationsfunktion (dataA, dataB, t) → newData
///             t=0 entspricht dataA, t=1 entspricht dataB
///   meshA   – Erstes Mesh (wird bei Subtraction beibehalten)
///   meshB   – Zweites Mesh (wird bei Subtraction abgezogen)
///
/// Rückgabe: Neues HalfEdgeMesh<'Data>
///
/// Algorithmus:
///   1. AABB-Bäume für schnelle Raumabfragen erstellen
///   2. Polygone beider Meshes gegen das jeweils andere klassifizieren
///      - Die HE-Nachbarschaft ermöglicht lokale Schnitte (wenige Polygone werden geteilt)
///   3. Je nach Operation die richtigen Teilmengen kombinieren:
///      - Union:        außerhalb(A) + außerhalb(B)
///      - Intersection: innerhalb(A) + innerhalb(B)
///      - Subtraction:  außerhalb(A) + innerhalb(B, invertiert)
let csg
        (op: CSGOperation)
        (lerp: Interpolator<'Data>)
        (meshA: HalfEdgeMesh<'Data>)
        (meshB: HalfEdgeMesh<'Data>)
        : HalfEdgeMesh<'Data> =

    let eps = 1e-4f

    // AABB-Bäume erstellen (nutzt bestehende Struktur aus Mesh.fs)
    let treeA = meshA.CreateAABBTree()
    let treeB = meshB.CreateAABBTree()

    match treeA, treeB with
    | None, _    -> meshB   // A leer → abhängig von Op evtl. anders behandeln
    | _, None    -> meshA
    | Some tA, Some tB ->

    // Klassifiziere Polygone von A relativ zu B
    let (aIn, aOut) = classifyAndSplit eps lerp meshA tB meshB
    // Klassifiziere Polygone von B relativ zu A
    let (bIn, bOut) = classifyAndSplit eps lerp meshB tA meshA

    // Wähle Teilmengen basierend auf der Operation
    let resultPolygons =
        match op with
        | Union ->
            // A außerhalb B  +  B außerhalb A
            aOut @ bOut

        | Intersection ->
            // A innerhalb B  +  B innerhalb A
            aIn @ bIn

        | Subtraction ->
            // A außerhalb B  +  B innerhalb A (Normale von B umkehren!)
            aOut @ (bIn |> List.map flipPoly)

    buildMeshFromPolygons resultPolygons

// ─────────────────────────────────────────────────────────────────────────────
// Komfort-Wrappers
// ─────────────────────────────────────────────────────────────────────────────

/// Vereinigung zweier Meshes (A ∪ B)
let union (lerp: Interpolator<'Data>) meshA meshB =
    csg Union lerp meshA meshB

/// Schnittmenge zweier Meshes (A ∩ B)
let intersection (lerp: Interpolator<'Data>) meshA meshB =
    csg Intersection lerp meshA meshB

/// Subtraktion: A minus B  (A \ B)
let subtraction (lerp: Interpolator<'Data>) meshA meshB =
    csg Subtraction lerp meshA meshB

// ─────────────────────────────────────────────────────────────────────────────
// Beispiel-Interpolatoren
// ─────────────────────────────────────────────────────────────────────────────

/// Interpoliert float32-Daten linear
let lerpFloat32 (a: float32) (b: float32) (t: float32) =
    a + t * (b - a)

/// Interpoliert Vector3-Daten linear (z.B. für Normalen oder Farben)
let lerpVector3 (a: Vector3) (b: Vector3) (t: float32) =
    a + t * (b - a)

/// Interpoliert Vector4-Daten linear (z.B. für RGBA-Farben)
let lerpVector4 (a: Vector4) (b: Vector4) (t: float32) =
    a + t * (b - a)

/// Ignoriert Interpolation, behält immer Daten von A  (t-unabhängig)
let keepA (a: 'Data) (_: 'Data) (_: float32) = a

/// Ignoriert Interpolation, behält immer Daten von B  (t-unabhängig)
let keepB (_: 'Data) (b: 'Data) (_: float32) = b
