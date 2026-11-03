module Graph

open Microsoft.Msagl.Core.Geometry
open Microsoft.Msagl.Core.Geometry.Curves
open Microsoft.Msagl.Core.Layout
open Microsoft.Msagl.Layout.Layered // Wichtig für SugiyamaLayoutSettings
open Microsoft.Msagl.Miscellaneous

type ShapeDefinition =
    Point list * Point list

type Connection =
    int * int * int * int

let createPolygon (points: Point list) =
    let pLine = Polyline()
    points |> List.iter pLine.AddPoint
    pLine.Closed <- true
    pLine

let createLayout
    (shapes: ShapeDefinition list)
    (connections: Connection list) =

    let graph = GeometryGraph()

    // Shapes in MSAGL-Knoten umwandeln.
    let nodes =
        shapes
        |> List.mapi (fun index (polygon, _ports) ->
            let curve = createPolygon polygon

            let node = Node(curve)
            node.UserData <- box index

            graph.Nodes.Add(node)
            node
        )

    // Verbindungen anlegen.
    for (srcShape, srcPort, dstShape, dstPort) in connections do
        let source = nodes[srcShape]
        let target = nodes[dstShape]

        let edge = Edge(source, target)

        // Die Portindizes werden später an den Kanten gesetzt.
        edge.UserData <- box (srcShape, srcPort, dstShape, dstPort)

        graph.Edges.Add(edge)

    // Layout berechnen.
    let settings = new SugiyamaLayoutSettings()
    
    // Optional: Einstellungen anpassen (z. B. Flussrichtung)
    // settings.Transformation <- PlaneTransformation.Rotation ...

    LayoutHelpers.CalculateLayout(graph, settings, null)

    graph