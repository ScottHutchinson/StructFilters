module NGDartStructFilters.StructFilters


#nowarn "9" //FSharp.NativeInterop.NativePtr.ofNativeInt can't be verified as safe

open System.Diagnostics
open System.IO
open System.Windows
open System.Windows.Controls
open Elmish
open Elmish.WPF

module App =
    open System.Reflection
    open Newtonsoft.Json
    open Domain

    let exeFolderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

    let mutable window: Window = null
    let mutable waitWindow: Window = null
    let mutable isFilteringEnabled: bool = true
    let mutable implementedStructs = [] // gets loaded once the first time the user shows the StructFiltersWindow

    let loadImplementedStructs () =
        match implementedStructs with
        | [] ->
            let filePath = Path.Combine(exeFolderPath, """Data\MsgTypeDefs\implemented_structs.json""")
            let str = File.ReadAllText filePath
            implementedStructs <- JsonConvert.DeserializeObject<ImplementedStruct list>(str)
        | _ -> ()

    let mutable unmodifiedModel = { 
        MsgType = { ID = 0; Name = "NullMsg" }
        DummyRoot = FieldData.empty |> RoseTree.asLeaf
        ExpandLevel = 2 // TODO: Move outside of the model?
        MaxLevel = 2 // TODO: Move outside of the model?
    }

    let mutable configFolderPath = ""

    let getConfigFolder path =
        let folder = Path.Combine(path, "StructFilters")
        if not (Directory.Exists(folder)) then Directory.CreateDirectory(folder) |> ignore
        folder

    let getConfigFile structName =
        Path.Combine(configFolderPath, sprintf "%s.json" structName)

    let version = "1.0.0" // semantic version of serialization data format

    let deserializeStruct file =
        let str = File.ReadAllText file
        JsonConvert.DeserializeObject<StructData>(str)

    // TODO: @TysonMN recommends replacing this function with RoseTree.map, but I couldn't make that work.
    let rec traverse (projection: RoseTree<FieldData> -> RoseTree<FieldData>) fld =
        let fields' =
            fld.Fields
            |> List.map (fun f -> 
                projection f
                |> traverse projection
            )
        { fld with Fields = fields' }

    let loadSavedChanges dummyRoot = 
        let updateFromFile (field: RoseTree<FieldData>) =
            let updateFields fileFld inMemoryFld =
                //TODO: Need to merge in the existing fields that are missing from the currently loaded struct
                //e.g. the on disk struct may have fields that exist in that version, but not in the current one
                //Not sure how to do this in F#
                inMemoryFld.Fields 
                |> List.map (fun f ->
                    fileFld.Fields
                    |> List.tryFind (fun fld -> fld.Data.Name = f.Data.Name)
                    |> Option.map (fun fld -> { f with Data = fld.Data })
                    |> Option.defaultValue f
                )
            let file = getConfigFile field.Data.Type
            if (not field.Fields.IsEmpty) && File.Exists file then
                let structData = deserializeStruct file
                if isNull structData.Version then // ignore old file missing the version number
                    field
                else
                    // Check the structData.Version number here as needed.
                    let ff = structData.Struct
                    { field with Fields = updateFields ff field }
            else field
        traverse updateFromFile dummyRoot

    /// Returns the specified resource as a string
    let LoadResourceAsString (structHdrResource: System.IntPtr) (length: int) =
        use umm = new UnmanagedMemoryStream( //Convert the HGLOBAL resource stream into a MemoryStream to avoid copying the data
                            FSharp.NativeInterop.NativePtr.ofNativeInt (WinAPI.LockResource structHdrResource), // FS0009, Uses of this construct may result in the generation of unverifiable .NET IL code.
                            System.Convert.ToInt64(length))
        use decompressor = new System.IO.Compression.GZipStream(umm, Compression.CompressionMode.Decompress)
        use rawDecompressedStream = new MemoryStream()
        decompressor.CopyTo(rawDecompressedStream)
        rawDecompressedStream.Seek(0L, SeekOrigin.Begin) |> ignore
        use reader = new System.IO.StreamReader(rawDecompressedStream, System.Text.Encoding.ASCII)
        reader.ReadToEnd()

    let init (structHdrResourceHandle: System.IntPtr) (structHdrDataLength: int) (msgTypeID: int) (msgTypeName: string) (parentStructName: string) measureElapsedTime () =
        let parentStruct = Translation.translate (LoadResourceAsString structHdrResourceHandle structHdrDataLength) parentStructName
        let dummyRoot =
            if parentStruct.Data.Type = "" then
                // Add one child to prevent a StackOverflow in Elmish.WPF.ViewModel.initializeBinding function.
                let dummyChild = FieldData.create "Parent struct not found" "" "" false
                [ dummyChild |> RoseTree.asLeaf ] |> FieldData.asDummyRoot
            else
                [ parentStruct ] |> FieldData.asDummyRoot
        measureElapsedTime "App.init"
        unmodifiedModel <- 
            { MsgType = { ID = msgTypeID; Name = msgTypeName }
              DummyRoot = loadSavedChanges dummyRoot
              ExpandLevel = unmodifiedModel.ExpandLevel
              MaxLevel = unmodifiedModel.MaxLevel
            }
        unmodifiedModel

    type SubtreeMsg =
        | GmlSetChecked of gmlChecked: bool
        | CmlSetChecked of cmlChecked: bool
        | CmlChangeFieldSetChecked of changeChecked: bool
        | CmlEntitySetChecked of entityChecked: bool
        | IsContextMenuOpen of rightClicked: bool
        | ContextCancel

    type SelectionMsg =
        | SelectAncestorMsg of FieldId * SelectionMsg
        | SelectAllMsg

    type SubtreeOutMsg =
        | OutSelectChild of IsGmlSelected_t * SelectionMsg

    [<Struct>]
    type Msg =
        | SelectMsg of IsGmlSelected_t * SelectionMsg
        | SubtreeMsg of RoseTreeMsg<FieldId, SubtreeMsg>
        | Save
        | Cancel
        | GmlSelectAll
        | GmlClearAll
        | IncreaseExpandLevel
        | DecreaseExpandLevel
        | ExpandAll
        | CollapseAll
        | SetFilteringEnabled of isEnabled: bool

    let selectAncestorMsg fieldId msg = SelectAncestorMsg (fieldId, msg)
    let outSelectChild isGmlSelected msg = OutSelectChild (isGmlSelected, msg)
    let selectMsg isGmlSelected msg = SelectMsg (isGmlSelected, msg)

    let private isGmlImplementedForParent fd =
        implementedStructs
        |> List.tryFind (fun s -> s.Name = fd.ParentType && s.Gml)
        |> Option.isSome

    let private isGmlImplementedForStruct fd =
        implementedStructs
        |> List.tryFind (fun s -> s.Name = fd.Type && s.Gml)
        |> Option.isSome

    let private isCmlImplementedForParent fd =
        implementedStructs
        |> List.tryFind (fun s -> s.Name = fd.ParentType && s.Cml)
        |> Option.isSome

    let private isCmlChangeFieldImplementedForParent fd =
        implementedStructs
        |> List.tryFind (fun s -> s.Name = fd.ParentType && s.CmlChangeField)
        |> Option.isSome

    let private isCmlEntityImplementedForParent fd =
        implementedStructs
        |> List.tryFind (fun s -> s.Name = fd.ParentType && s.CmlEntity)
        |> Option.isSome

    let selectOneForGml isGml (field: RoseTree<FieldData>) =
        if isGmlImplementedForParent field.Data then
            let fd = field.Data |> FieldData.setIsGml isGml
            { field with Data = fd }
        else
            field

    let setGmlForAll isGml parent =
        let setGml = selectOneForGml isGml
        let dummyRoot = traverse setGml parent // set GML for all the children, grandchildren, etc.
        if isGml then
            dummyRoot |> setGml // set GML for the parent
        else
            dummyRoot // when clearing children, do not automatically clear the parent.

    let selectAllForGml = setGmlForAll true

    let clearAllForGml = setGmlForAll false

    let getChangedStructs m =
        let isNotEqual (fldA: RoseTree<FieldData>) (fldB: RoseTree<FieldData>) = fldA.Data <> fldB.Data
        // Depth First Search
        let rec diff acc oldFld newFld =
            match oldFld.Fields with
            | [] -> acc
            | fields -> 
                let acc' =
                    if (fields, newFld.Fields) ||> List.exists2 isNotEqual then
                        RoseTree.depthAtMost1 newFld :: acc
                    else
                        acc

                (acc', fields, newFld.Fields)
                |||> List.fold2 (fun accu oldChild newChild -> diff accu oldChild newChild)
        
        diff [] unmodifiedModel.DummyRoot.Fields.Head m.DummyRoot.Fields.Head

    let serializeStructs flds =
        let ser = JsonSerializer()
        ser.Formatting <- Newtonsoft.Json.Formatting.Indented
        flds
        |> List.iter (fun (f: RoseTree<FieldData>) ->
            let file = sprintf "%s.json" f.Data.Type
            let filePath = Path.Combine(configFolderPath, file)
            use writer = File.CreateText(filePath)
            let structData = { Version = version; Struct = f }
            ser.Serialize(writer, structData)
        )

    let getMenuItemHeader menuItemText =
        // use a TextBlock instead of a string to prevent underscores from becoming accelerator keys.
        let textBlock = TextBlock()
        textBlock.Text <- menuItemText
        textBlock

    let updateFieldData = function
        | GmlSetChecked isChecked ->
            //RoseTree.nodesCount <- 1 // just for testing how many nodes are in the tree.
            // Instead of using the mutable RoseTree.nodesCount, pass the tree into RoseTree.size
            isChecked |> FieldData.setIsGml |> RoseTree.mapData
        | CmlSetChecked isChecked ->
            isChecked |> FieldData.setIsCml |> RoseTree.mapData
        | CmlChangeFieldSetChecked isChecked ->
            isChecked |> FieldData.setIsCmlChangeField |> RoseTree.mapData
        | CmlEntitySetChecked isChecked ->
            isChecked |> FieldData.setIsCmlEntity |> RoseTree.mapData
        | IsContextMenuOpen isRightClicked -> fun fd ->
            if not isRightClicked || not fd.Fields.IsEmpty then
                isRightClicked |> FieldData.setIsContextMenuOpen |> RoseTree.mapData <| fd
            else
                fd
        | ContextCancel -> id

    let updateSubtree msg = msg |> updateFieldData

    let hasId id (fd: RoseTree<FieldData>) = fd.Data.Id = id

    let mapDummyRoot f m =
        { m with DummyRoot = m.DummyRoot |> f }

    let updateSubtreeSelection b =
        let rec loop = function
            | SelectAncestorMsg (fieldId, msg) -> fun (m: RoseTree<FieldData>) ->
                // when clearing children, do not automatically clear the ancestors.
                let m' = if b then m |> selectOneForGml b else m
                { 
                    Data = m'.Data
                    Fields = m.Fields |> List.mapFirst (fun (t: RoseTree<FieldData>) -> t.Data.Id = fieldId) (loop msg) 
                }
            | SelectAllMsg -> b |> setGmlForAll 
        loop

    let update msg m =
        match msg with
        | SelectMsg (b, msg) -> 
            msg |> updateSubtreeSelection b |> mapDummyRoot <| m
        | SubtreeMsg msg ->
            msg |> RoseTree.update hasId updateSubtree |> mapDummyRoot <| m
        | Save -> 
            let changedStructs = getChangedStructs m
            serializeStructs changedStructs
            window.DialogResult <- true
            window.Close()
            m
        | Cancel -> 
            window.Close()
            m
        | GmlSelectAll ->
            let rt = selectAllForGml m.DummyRoot
            { m with DummyRoot = rt }
        | GmlClearAll ->
            let rt = clearAllForGml m.DummyRoot
            { m with DummyRoot = rt }
        | IncreaseExpandLevel ->
            { m with ExpandLevel = m.ExpandLevel + 1 }
        | DecreaseExpandLevel ->
            if m.ExpandLevel > 0 then
                { m with ExpandLevel = m.ExpandLevel - 1 }
            else
                m
        | ExpandAll ->
            { m with ExpandLevel = unmodifiedModel.MaxLevel }
        | CollapseAll ->
            { m with ExpandLevel = 0 }
        | SetFilteringEnabled isEnabled ->
            isFilteringEnabled <- isEnabled
            m

    let mapOutMsg = function
        | OutSelectChild (isGmlSelected, msg) -> 
            fun fieldId -> (fieldId, msg) |> SelectAncestorMsg |> outSelectChild isGmlSelected |> OutMsg

    let mapOutMsgRoot = function
        | OutSelectChild (isGmlSelected, msg) -> 
            fun fieldId -> (fieldId, msg) |> SelectAncestorMsg |> selectMsg isGmlSelected

    type SelfWithParent<'a> =
        { Self: 'a
          Parent: 'a }

    module Self =
        let get m = m.Self

    let rec fieldBindings level () =
        if level > unmodifiedModel.MaxLevel then 
            unmodifiedModel <- { unmodifiedModel with MaxLevel = level }

        let inMsgBindings = 
          [
            "Name" |> Binding.oneWay(fun (_, { Self = (s: RoseTree<FieldData>) }) -> s.Data.Name)
            "Type" |> Binding.oneWay(fun (_, { Self = (s: RoseTree<FieldData>) }) -> s.Data.Type)
            "Union" |> Binding.oneWay(fun (_, { Self = (s: RoseTree<FieldData>) }) -> 
                if s.Data.IsUnion then "Union" else "Type"
            )
            "IsGml" |> Binding.twoWay(
                (fun (_, { Self = (s: RoseTree<FieldData>) }) -> s.Data.IsGml),
                (fun v _ -> v |> GmlSetChecked |> LeafMsg)
            )
            "IsCml" |> Binding.twoWay(
                (fun (_, { Self = (s: RoseTree<FieldData>) }) -> s.Data.IsCml),
                (fun v _ -> v |> CmlSetChecked |> LeafMsg)
            )
            "IsCmlChangeField" |> Binding.twoWay(
                (fun (_, { Self = (s: RoseTree<FieldData>) }) -> s.Data.IsCmlChangeField),
                (fun v _ ->  v |> CmlChangeFieldSetChecked |> LeafMsg)
            )
            "IsCmlEntity" |> Binding.twoWay(
                (fun (_, { Self = (s: RoseTree<FieldData>) }) -> s.Data.IsCmlEntity),
                (fun v _ -> v |> CmlEntitySetChecked |> LeafMsg)
            )
            "IsContextMenuOpen" |> Binding.twoWay(
                (fun (_, { Self = (s: RoseTree<FieldData>) }) -> s.Data.IsContextMenuOpen),
                (fun v _ -> v |> IsContextMenuOpen |> LeafMsg)
            )
            "ContextSelectAllHeader" |> Binding.oneWay(fun (_, { Self = (s: RoseTree<FieldData>) }) -> 
                getMenuItemHeader $"Select all fields of {s.Data.Type} for GML")
            "ContextClearAllHeader" |> Binding.oneWay(fun (_, { Self = (s: RoseTree<FieldData>) }) -> 
                getMenuItemHeader $"Clear all fields of {s.Data.Type} for GML")
            "ContextCancel" |> Binding.cmd (LeafMsg ContextCancel)
            "IsEnabled" |> Binding.oneWay(fun ((m: Model), _) -> not m.IsEmpty)
            "IsExpanded" |> Binding.oneWay(fun ((m: Model), _) -> level < m.ExpandLevel)
            (* TODO: Special DX sub-MTs 
               1. Hide the GML checkbox.
               (because that would duplicate functionality on the Special DX tab).
               2. Add the sub-MT description to the display name.
            *)
            "GmlImplementedForStruct" |> Binding.oneWay(fun (_, { Self = (s: RoseTree<FieldData>) }) -> isGmlImplementedForStruct s.Data)
            "GmlImplementedForParent" |> Binding.oneWay(fun (_, { Self = (s: RoseTree<FieldData>) }) -> isGmlImplementedForParent s.Data)
            "CmlImplementedForParent" |> Binding.oneWay(fun (_, { Self = (s: RoseTree<FieldData>) }) -> isCmlImplementedForParent s.Data)
            "CmlChangeFieldImplementedForParent" |> Binding.oneWay(fun (_, { Self = (s: RoseTree<FieldData>) }) -> isCmlChangeFieldImplementedForParent s.Data)
            "CmlEntityImplementedForParent" |> Binding.oneWay(fun (_, { Self = (s: RoseTree<FieldData>) }) -> isCmlEntityImplementedForParent s.Data)
            "CmlEntityImplementedForParent" |> Binding.oneWay(fun (_, { Self = (s: RoseTree<FieldData>) }) -> isCmlImplementedForParent s.Data)
            "ParentStruct"            |> Binding.oneWay(fun (_, { Self = (s: RoseTree<FieldData>) }) -> FieldData.isParentStruct s.Data)
          ] |> Bindings.mapMsg InMsg

        let outMsgBindings =
          [ "ContextSelectAll" |> Binding.cmd (SelectAllMsg |> outSelectChild true)
            "ContextClearAll" |> Binding.cmd (SelectAllMsg |> outSelectChild false)
          ] |> Bindings.mapMsg OutMsg

        outMsgBindings @ inMsgBindings @ [
          "ChildFields"
            |> Binding.subModelSeq (fieldBindings (level + 1), (fun (_, { Self = (fd: RoseTree<FieldData>) }) -> fd.Data.Id))
            |> Binding.mapModel (fun (m, { Self = p }) -> p.Fields |> Seq.map (fun fd -> m, { Self = fd; Parent = p }))
            |> Binding.mapMsg (fun (fieldId, inOutMsg) ->
                  match inOutMsg with
                  | InMsg msg -> (fieldId, msg) |> BranchMsg |> InMsg
                  | OutMsg msg -> fieldId |> mapOutMsg msg |> InOut.mapIn LeafMsg)
        ]

    let rootBindings () : Binding<Model, Msg> list = [
        "IsEnabled" |> Binding.oneWay(fun m -> not m.IsEmpty)
        "IsFilteringEnabled" |> Binding.twoWay(
            (fun _ -> isFilteringEnabled),
            (fun newVal _ -> newVal |> SetFilteringEnabled)
        )
        "MsgType" |> Binding.oneWay(fun m -> m.MsgType)
        "ExpandLevel" |> Binding.oneWay(fun m -> $"{m.ExpandLevel} of {unmodifiedModel.MaxLevel} ")
        "Fields"
          |> Binding.subModelSeq (fieldBindings 0, (fun (_, { Self = c }) -> c.Data.Id))
          |> Binding.mapModel (fun m -> m.DummyRoot.Fields |> Seq.map (fun c -> m, { Self = c; Parent = m.DummyRoot }))
          |> Binding.mapMsg (fun (fieldId, inOutMsg) ->
                match inOutMsg with
                | InMsg msg -> (fieldId, msg) |> BranchMsg |> SubtreeMsg
                | OutMsg msg -> fieldId |> mapOutMsgRoot msg)

        "Save" |> Binding.cmd Save
        "Cancel" |> Binding.cmd Cancel
        "GmlSelectAll" |> Binding.cmd GmlSelectAll
        "GmlClearAll" |> Binding.cmd GmlClearAll
        "IncreaseExpandLevel" |> Binding.cmd IncreaseExpandLevel
        "DecreaseExpandLevel" |> Binding.cmd DecreaseExpandLevel
        "ExpandAll" |> Binding.cmd ExpandAll
        "CollapseAll" |> Binding.cmd CollapseAll
    ]

let designVm = ViewModel.designInstance App.unmodifiedModel (App.rootBindings())

module PublicAPI = 

    let init mainWindow waitWindow =
        App.window <- mainWindow
        App.waitWindow <- waitWindow

    let loadWindow (structHdrResourceHandle: System.IntPtr) (structHdrDataLength: int) (msgTypeID: int)
        (msgTypeName: string) (parentStructName: string) (configFolder: string)
        (isSuccessful: byref<bool>) (isFilteringEnabled: byref<bool>) =
        try
            App.loadImplementedStructs ()
            App.isFilteringEnabled <- isFilteringEnabled
            App.configFolderPath <- App.getConfigFolder configFolder
            let mutable timeMeasurementString = ""
            let watch = Stopwatch.StartNew()
            let measureElapsedTime label =
                let elapsed = watch.Elapsed // <-- explicit copy prevents level 5 warning FS0052.
                timeMeasurementString <- sprintf "%s | %s Elapsed Time: %.1f seconds" 
                    timeMeasurementString label elapsed.TotalSeconds
                watch.Restart()

            App.window.Activated.Add (fun _ -> 
                measureElapsedTime "TreeView loading"
                App.waitWindow.Close()
                )
            let init = App.init structHdrResourceHandle structHdrDataLength msgTypeID msgTypeName parentStructName measureElapsedTime
            let showDialogWithConfig (window: Window) program =
                App.waitWindow.Show()
                WpfProgram.startElmishLoop window program
                window.ShowDialog ()

            let isAccepted = 
                WpfProgram.mkSimple init App.update App.rootBindings
                |> showDialogWithConfig (App.window)

            if isAccepted.HasValue && isAccepted.Value then // user clicked the Save button
                isFilteringEnabled <- App.isFilteringEnabled // return the checkbox value

            isSuccessful <- true

            // Return diagnostic information in a string.
            sprintf "StructFilters Tree View (MT 0x%04X)%s" 
                msgTypeID timeMeasurementString
        with ex ->
            isSuccessful <- false
            sprintf "StructFilters loadWindow Exception: %s" ex.Message

    let tryGetFilters (structName: string) (configFolder: string) (fields: byref<string array>) (isGML: bool) =
        App.configFolderPath <- App.getConfigFolder configFolder
        let file = App.getConfigFile structName
        if File.Exists file then
            let structData = App.deserializeStruct file
            if isNull structData.Version then // ignore old file missing the version number
                false
            else
                // Check the structData.Version number here as needed.
                let fld = structData.Struct
                let outputType (field : Domain.FieldData) (isGML: bool) = 
                    if isGML then
                        field.IsGml
                    else
                        field.IsCml
                fields <-
                    (
                        fld.Fields
                        |> List.toArray
                        |> Array.choose (fun f -> if (outputType f.Data isGML) then Some f.Data.Name else None )
                    )
                true
        else false
