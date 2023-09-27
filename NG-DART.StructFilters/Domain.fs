module Domain
open System
open Newtonsoft.Json

[<CLIMutable>]
type ImplementedStruct = {
    Name: string
    [<DefaultValue>] Gml: bool
    [<DefaultValue>] Cml: bool
    [<DefaultValue>] CmlChangeField: bool
    [<DefaultValue>] CmlEntity: bool
}

type FieldId = FieldId of Guid

type IsGmlSelected_t = bool

type MessageType = { 
    ID: int
    Name: string
}

type FieldData = { // Leaf data
    Id: FieldId
    Name: string
    Type: string
    ParentType: string
    [<JsonIgnore>] IsUnion: bool
    IsGml: bool
    IsCml: bool
    IsCmlChangeField: bool
    IsCmlEntity: bool
    IsContextMenuOpen: bool
}

type Model = { 
    MsgType: MessageType
    DummyRoot: RoseTree<FieldData>
    ExpandLevel: int
    MaxLevel: int
    Search_Text: string
    SearchEnterText: string
    SearchFoundId: FieldId option
}
    with 
        member this.ParentStruct = this.DummyRoot.Fields.Head
        member this.IsEmpty = 
            this.DummyRoot.Fields.IsEmpty || this.ParentStruct.Data.Type = ""

type StructData = {
    Version: string
    Struct: RoseTree<FieldData>
}
