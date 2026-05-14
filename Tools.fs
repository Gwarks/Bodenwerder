module Tools

open System.Collections.Concurrent
open System
open System.Dynamic
open System.Reflection
open System.Runtime.InteropServices

module WeakSingleton =
    let private instances = ConcurrentDictionary<Type, WeakReference>()
    let get<'T when 'T : (new : unit -> 'T)> () : 'T =
        match instances.TryGetValue typeof<'T> with
        | true, weakRef ->
            match weakRef.Target with
            | :? 'T as existing ->
                existing
            | _ ->
                let newInstance = new 'T()
                instances.[typeof<'T>] <- WeakReference(newInstance)
                newInstance
        | false, _ ->
            let newInstance = new 'T()
            instances.[typeof<'T>] <- WeakReference(newInstance)
            newInstance

// ----------------------------
// Hilfsfunktionen für Overload Resolution (Modul-Ebene)
// ----------------------------
let private scoreMethod (m: MethodBase) (args: obj[]) =
    let ps = m.GetParameters()
    if ps.Length <> args.Length then
        None
    else
        let mutable score = 0
        let mutable valid = true
        for i in 0 .. ps.Length - 1 do
            let pType = ps.[i].ParameterType
            let arg = args.[i]
            if isNull arg then
                if pType.IsValueType then valid <- false
            else
                let aType = arg.GetType()
                if pType = aType then score <- score + 3
                elif pType.IsAssignableFrom(aType) then score <- score + 2
                else
                    try
                        let _ = Convert.ChangeType(arg, pType)
                        score <- score + 1
                    with _ -> valid <- false
        if valid then Some score else None

let private findBestMethod (methods: seq<MethodBase>) (args: obj[]) =
    methods
    |> Seq.choose (fun m -> scoreMethod m args |> Option.map (fun s -> m, s))
    |> Seq.sortByDescending snd
    |> Seq.tryHead
    |> Option.map fst

let private getArgDescription (args: obj[]) =
    args 
    |> Array.map (fun a -> if isNull a then "None" else a.GetType().Name)
    |> String.concat ", "

let private getMethodDescription (m: MethodBase) =
    let ps = m.GetParameters()
    let pDesc = ps |> Array.map (fun p -> sprintf "%s %s" p.ParameterType.Name p.Name) |> String.concat ", "
    sprintf "(%s)" pDesc

let private failWithNoMatch (name: string) (methods: seq<MethodBase>) (args: obj[]) =
    let actual = getArgDescription args
    let candidates = 
        methods 
        |> Seq.map (fun m -> "  - " + getMethodDescription m)
        |> String.concat "\n"
    failwithf "No matching overload found for '%s'.\nProvided: (%s)\nCandidates:\n%s" name actual candidates

let private convertArgs (ps: ParameterInfo[]) (args: obj[]) =
    Array.mapi (fun i arg ->
        let targetType = ps.[i].ParameterType
        if isNull arg then null
        elif targetType.IsAssignableFrom(arg.GetType()) then arg
        else Convert.ChangeType(arg, targetType)
    ) args

// ----------------------------
// Wrapper für Methodengruppen (Overloads)
// ----------------------------
type MethodGroupWrapper(name: string, methods: seq<MethodBase>) =
    inherit DynamicObject()
    override _.TryInvoke(binder: InvokeBinder, args: obj[], [<Out>] result: obj byref) =
        match findBestMethod methods args with
        | Some m ->
            let ps = m.GetParameters()
            let converted = convertArgs ps args
            result <- m.Invoke(null, converted)
            true
        | None ->
            failWithNoMatch name methods args

// ----------------------------
// Wrapper für .NET Typen
// ----------------------------
type TypeWrapper(t: Type) =
    inherit DynamicObject()

    // ----------------------------
    // __call__ → Konstruktor
    // ----------------------------
    override _.TryInvoke(binder: InvokeBinder, args: obj[], [<Out>] result: obj byref) =
        let ctors = t.GetConstructors() |> Seq.cast<MethodBase>
        match findBestMethod ctors args with
        | Some ctor ->
            let ps = ctor.GetParameters()
            let converted = convertArgs ps args
            result <- (ctor :?> ConstructorInfo).Invoke(converted)
            true
        | None ->
            failWithNoMatch t.Name ctors args

    // ----------------------------
    // Zugriff: obj.X
    // ----------------------------
    override _.TryGetMember(binder: GetMemberBinder, [<Out>] result: obj byref) =
        let name = binder.Name

        // Property?
        let prop = t.GetProperty(name, BindingFlags.Public ||| BindingFlags.Static)
        if not (isNull prop) then
            result <- prop.GetValue(null)
            true
        else
            // Feld?
            let field = t.GetField(name, BindingFlags.Public ||| BindingFlags.Static)
            if not (isNull field) then
                result <- field.GetValue(null)
                true
            else
                // Methoden → als callable zurückgeben
                let methods =
                    t.GetMethods(BindingFlags.Public ||| BindingFlags.Static)
                    |> Array.filter (fun m -> m.Name = name)
                    |> Seq.cast<MethodBase>

                if not (Seq.isEmpty methods) then
                    result <- MethodGroupWrapper(name, methods)
                    true
                else
                    result <- null
                    false

    // ----------------------------
    // Direkter Methodenaufruf: obj.Method(...)
    // ----------------------------
    override Me.TryInvokeMember(binder: InvokeMemberBinder, args: obj[], [<Out>] result: obj byref) =
        let methods =
            t.GetMethods(BindingFlags.Public ||| BindingFlags.Static)
            |> Array.filter (fun m -> m.Name = binder.Name)
            |> Seq.cast<MethodBase>

        match findBestMethod methods args with
        | Some m ->
            let ps = m.GetParameters()
            let converted = convertArgs ps args
            result <- m.Invoke(null, converted)
            true
        | None ->
            failWithNoMatch binder.Name methods args