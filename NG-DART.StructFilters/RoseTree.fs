[<AutoOpen>]
module RoseTree
(* From https://github.com/elmish/Elmish.WPF/issues/236# 
contributed by Tyson Williams https://github.com/bender2k14
*)

    type RoseTree<'model> =
        { Data: 'model
          Fields: RoseTree<'model> list }

    type RoseTreeMsg<'a, 'msg> =
        | BranchMsg of 'a * RoseTreeMsg<'a, 'msg>
        | LeafMsg of 'msg

    type InOutMsg<'a, 'b> =
        | InMsg of 'a
        | OutMsg of 'b

    module InOut =
        let cata f g = function
            | InMsg a -> f a
            | OutMsg c -> g c

        let map f g = cata (f >> InMsg) (g >> OutMsg)

        let mapIn f = map f id

    module Option =
        let set a = Option.map (fun _ -> a)

    module Func =
        let flip f b a = f a b

    module FuncOption =
        let inputIfNone f a = a |> f |> Option.defaultValue a
        let map (f: 'b -> 'c) (mb: 'a -> 'b option) =
            mb >> Option.map f
        let bind (f: 'b -> 'a -> 'c) (mb: 'a -> 'b option) a =
            mb a |> Option.bind (fun b -> Some(f b a))

    let map get set f a =
        a |> get |> f |> Func.flip set a

    module RoseTree =

        // From https://blog.ploeh.dk/2019/09/16/picture-archivist-in-f/
        let cata f =
            let rec cataRec t =
                t.Fields |> List.map cataRec |> f t.Data
            cataRec

        let getData t = t.Data
        let setData (d: 'a) (t: RoseTree<'a>) = { t with Data = d }
        let mapData f = map getData setData f

        let getChildren t = t.Fields
        let setChildren c t = { t with Fields = c }
        let mapChildren f = map getChildren setChildren f

        let branchMsg a t = BranchMsg (a, t)

        let asLeaf a =
            { Data = a
              Fields = [] }

        let addSubtree t = t |> List.cons |> mapChildren
        let addChildData a = a |> asLeaf |> addSubtree

        let update p (f: 'msg -> RoseTree<'model> -> RoseTree<'model>) =
            let rec updateRec = function
            | BranchMsg (a, msg) -> msg |> updateRec |> List.mapFirst (p a) |> mapChildren
            | LeafMsg msg -> msg |> f
            updateRec

        let depthAtMost1 x = x |> ([] |> setChildren |> List.map |> mapChildren)

        let size t = t |> cata (fun _ c -> c |> List.length |> (+) 1)

        let map f =
            let rec loop t =
                { Data = f t.Data
                  Fields = t.Fields |> List.map loop }
            loop
