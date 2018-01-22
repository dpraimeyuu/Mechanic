module Mechanic.SymbolGraph
open System.IO
open Utils.Namespace
open Mechanic.Utils

let getDependencies files =
    let depsData = files |> List.map SymbolGetter.getSymbols
    let allDefsMap = 
        depsData |> Seq.collect (fun (f,defs,_) -> defs |> List.map (fun d -> lastPart d, (d, f)))
        |> Seq.groupBy fst |> Seq.map (fun (k, xs) -> k, xs |> Seq.map snd |> Seq.toList) |> Map.ofSeq
    let depsData = 
        depsData |> List.map (fun (f,defs,opens) -> 
            f, defs, opens |> List.map (fun o -> 
                { o with UsedSymbols = o.UsedSymbols |> List.filter (fun u -> allDefsMap |> Map.containsKey (lastPart u)) } ))
        |> List.collect (fun (f2, defs2, opens2) -> opens2 |> List.map (fun o -> f2, defs2, o.Opens, o.UsedSymbols))
    // depsData |> Seq.iter (fun (f,defs,opens,uses) -> 
    //     printfn "File: %A" f
    //     printfn "Def: %A" defs
    //     printfn "Opens: %A" opens
    //     printfn "Used: %A" uses
    // )
    let deps =
        depsData |> List.collect (fun (f2, _, opens2, uses2) ->
            // Concat two list and merge same part on end of first list and start of second list.
            // Ex: merge ["A";"B";"C"] ["C";"D"] = ["A";"B";"C";"D"]
            let rec merge l1 l2 =
                let len1 = List.length l1
                let len2 = List.length l2
                let l = min len1 len2
                [0..l] |> List.tryFind (fun i -> 
                    let l1' = l1 |> List.skip i |> List.take (len1-i)
                    let l2' = l2 |> List.take (len2-i)
                    if l1' = [] || l2' = [] then false else Seq.forall2 (fun x y -> x = y) l1' l2')
                |> Option.map (fun i -> l1 @ (List.skip (min len2 (len1-i)) l2))
                |> Option.defaultValue (l1 @ l2)
            let opensVariants s = ("" :: opens2) |> List.map (fun o -> merge (splitByDot o) (splitByDot s) |> joinByDot)
            //printfn "%A" allDefsMap
            let tryFindDef s = 
                allDefsMap |> Map.tryFind (lastPart s)
                |> Option.bind (fun g -> 
                    let r = opensVariants s |> List.tryPick (fun o -> g |> List.tryFind (fun (d,_) -> o=d))
                    match r with
                    | None -> 
                        //printfn "No match: %s -- %A -- %A" f2 (opensVariants s) g
                        None
                    | Some _ -> 
                        //printfn "Find match: %A -- %s" r f2
                        r)
                |> Option.map (fun (d,f) -> f, f2, d)
            uses2 |> List.choose tryFindDef
        )
        |> List.groupBy (fun (f1, f2, _) -> f1, f2) |> List.map (fun ((f1, f2), xs) -> f1, f2, xs |> List.map (fun (_,_,x) -> x))
    //printfn "%A" deps
    deps

let solveOrder files =
    let deps = getDependencies files
    let edges = deps |> List.map (fun (f1,f2,_) -> f1, f2)
    GraphAlg.topologicalOrder files edges

let solveOrderFromPattern root filePattern =
    Directory.EnumerateFiles(root,filePattern) |> Seq.toList
    |> solveOrder