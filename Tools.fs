module Tools

open System.Collections.Concurrent
open System

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
