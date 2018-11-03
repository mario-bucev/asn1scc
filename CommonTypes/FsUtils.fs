﻿(*
* Copyright (c) 2008-2012 Semantix and (c) 2012-2015 Neuropublic
*
* This file is part of the ASN1SCC tool.
*
* Licensed under the terms of GNU General Public Licence as published by
* the Free Software Foundation.
*
*  For more informations see License.txt file
*)

module FsUtils

open System
open System.Numerics
open Antlr.Runtime.Tree
open Antlr.Runtime



type OptionBuilder() =
    member x.Bind(opt, f) =
        match opt with
        | Some(value) -> f(value)
        | _ -> None
    member x.Return(v) = Some(v)

let option = new OptionBuilder()



let ToC (str:string) =  str.Replace('-','_').Replace('.','_').Replace("#","elm")

let ToC2  =  ToC


type stringL  = (string*int)

type BigIntL  = (BigInteger*int)

let getTreeChildren (tree:ITree) = [for i in 0..tree.ChildCount-1 -> tree.GetChild(i)]

let getChildrenByType (tree:ITree, childType) = getTreeChildren(tree) |> List.filter(fun x -> x.Type=childType)

let getChildByType (tree:ITree, childType) = List.head(getChildrenByType(tree, childType))

let getOptionChildByType (tree:ITree, childType) = 
    getTreeChildren(tree) |> List.tryFind(fun x -> x.Type = childType)

//let getTextLine (tree:ITree) = (tree.Text, tree.Line)

type SrcLoc = 
    { 
        srcFilename : string
        srcLine : int 
        charPos : int
    }
    


let emptyLocation = {srcFilename=""; srcLine=0; charPos = 0}

[<CustomEquality; NoComparison>]
type PrimitiveWithLocation<'T when 'T :equality>  = 
    {
        Value:'T
        Location:SrcLoc
    }
    override x.ToString() = 
        x.Value.ToString()
    member x.AsTupple = (x.Value, x.Location)
    member x.IsEqual(other:PrimitiveWithLocation<'T>) = x.Value.Equals(other.Value)
    member x.IsEqual(other:'T) = x.Value.Equals(other)
    static member ByValue (v:'T) = { Value = v; Location = {srcFilename=""; srcLine=0; charPos=0}}
    override x.Equals(yobj) =
        match yobj with
        | :? PrimitiveWithLocation<'T> as other -> x.Value = other.Value
        | _ -> false
    override x.GetHashCode() = x.Value.GetHashCode()
    member this.GetHashCode2() = (this.Value.GetHashCode()) ^^^ (this.Location.srcFilename.GetHashCode()) ^^^ (this.Location.srcLine.GetHashCode())
//let AsLoc x = PrimitiveWithLocation.ByValue x
let loc<'T when 'T :equality> v = PrimitiveWithLocation<'T>.ByValue v

type StringLoc = PrimitiveWithLocation<string>
type IntLoc = PrimitiveWithLocation<BigInteger>
type DoubleLoc = PrimitiveWithLocation<double>
type BoolLoc = PrimitiveWithLocation<bool>
type ByteLoc = PrimitiveWithLocation<byte>


type System.String with
    member x.AsLoc = StringLoc.ByValue x
    member s1.icompare(s2: string) =
        System.String.Equals(s1, s2, System.StringComparison.CurrentCultureIgnoreCase);;

exception SemanticError of SrcLoc*string
exception UserException of string
exception BugErrorException of string

let dict = System.Collections.Generic.Dictionary<ITree,string>()

type ITree with
    member t.Children = getTreeChildren(t)
    member t.GetChildByType (childType) = getChildByType(t, childType)
    member t.GetChildrenByType(childType) = getChildrenByType(t, childType)
    member t.GetOptChild(childTyep) = 
        match getChildrenByType(t, childTyep) with
        | []            -> None
        | first::_      -> Some(first)
    member t.Root = if t.Parent = null then t else t.Parent.Root
    static member RegisterFiles(files:seq<ITree*string>) =
                    for (i,f) in files do
                        dict.Add(i,f)
    member t.FileName = dict.[t.Root]
    member t.Location = { srcFilename = t.FileName; srcLine = t.Line; charPos = t.CharPositionInLine}

    member t.AllChildren =
        seq {
            yield t
            for ch in t.Children do        
                yield! ch.AllChildren
        } |> Seq.toList

//    member t.TextFL = (t.Text, t.Location)
//    member t.BigIntFL = (BigInteger.Parse(t.Text), t.Location)
//    member t.GetValueFL v = (v, t.Location)

    member t.GetValueL<'T when 'T :equality> (v:'T) = { StringLoc.Value = v; Location = t.Location}
    
    member t.TextL = { StringLoc.Value = t.Text; Location = t.Location}

     

    member t.BigIntL = { IntLoc.Value = t.BigInt; Location = t.Location} 
    
    member t.BigInt = 
        let ret = BigInteger.Parse(t.Text)
        match ret < (BigInteger System.Int64.MinValue) || ret > (BigInteger System.UInt64.MaxValue) with
        | true  when ret > (BigInteger System.Int64.MaxValue) -> raise(SemanticError(t.Location, (sprintf "Integer value of range. Supported values are within range %d to %d" 0 System.UInt64.MaxValue)))
        | true  -> raise(SemanticError(t.Location, (sprintf "Integer value of range. Supported values are within range %d to %d" System.Int64.MinValue System.Int64.MaxValue)))
        | false -> ret

    member t.Double = System.Double.Parse(t.Text, System.Globalization.NumberFormatInfo.InvariantInfo)
    member t.DoubleL = { StringLoc.Value = t.Double; Location = t.Location} 
    


let rec getAsTupples<'T> (list:array<'T>) (empty:'T) =
    let mutable x = 0
    seq {
        while x < list.Length do
            let a = list.[x]
            let b = if x+1 < list.Length then list.[x+1] else empty
            yield (a,b)
            x <- x + 2
    } |> Seq.toList



let getOptionalChildByType (tree:ITree, childType) = 
    match getChildrenByType(tree, childType) with
        | head::tail    -> Some(head)
        | _             -> None



type System.String with
    member this.IsEmptyOrNull = if this = null || this = "" then true else false

let MakeLowerFirst(s:string) =
    if s.IsEmptyOrNull then ""
    else s.Substring(0,1).ToLower() + s.Substring(1)


let tryGetEnvVar (varName:string) =
    let envVars = 
        System.Environment.GetEnvironmentVariables() |> Seq.cast<System.Collections.DictionaryEntry>  |> Seq.map (fun d -> d.Key :?> string, d.Value :?> string)    
    envVars |> Seq.tryFind(fun (nm, vl) -> nm = varName) 

let checkForAdaKeywords () =
    match tryGetEnvVar "ASN1SCC_DISABLE_KEYW_CHECKS" with
    | Some (_,vl)    -> vl <> "1"
    | None      -> true
    

type System.String with
    member s.WithLoc (lc:SrcLoc) = {StringLoc.Value = s; Location=lc}
    member this.L1 = MakeLowerFirst this
    member this.U1 = 
        if this.IsEmptyOrNull then ""
        else this.Substring(0,1).ToUpper() + this.Substring(1)
    member this.RDA =
        if this.IsEmptyOrNull then ""
        else this.Replace('.','_')
    member this.JSEsc =
        if this.IsEmptyOrNull then ""
        else this.Replace("'","\\'").Replace("\"","\\\"")
    member this.IDQ =
        if this.IsEmptyOrNull then "\"\""
        else sprintf "\"%s\"" this
    member this.ISQ =
        if this.IsEmptyOrNull then "''"
        else sprintf "'%s'" this.JSEsc
    member this.EDQ = if this.IsEmptyOrNull then "" else this.Replace("\"","\\\"")
    member this.HtmlEsc =
        if this.IsEmptyOrNull then ""
        else this.
                Replace("&", "&amp;").
                Replace("<","&lt;").
                Replace(">","&gt;").
                Replace("\"","&quot;").
                Replace("'","&apos;");

type Option<'T> with
    member this.orElse other =
        match this with
        | Some v    -> v
        | None      -> other
    //member this.map fnc = Option.map fnc this
        

type System.Int32 with
    member x.AsBigInt = BigInteger x


type System.Collections.Generic.IEnumerable<'T> with
    member t.StrJoin str = 
        if Seq.isEmpty(t) then 
            ""
        else
            t |> Seq.map(fun x -> x.ToString()) |> Seq.reduce(fun agr el -> agr + str + el.ToString())



module Seq =
    let StrJoin str listItems =
        if Seq.isEmpty listItems then 
            ""
        else
            listItems |> Seq.map(fun x -> x.ToString()) |> Seq.reduce(fun agr el -> agr + str + el.ToString())


module List =
    let rec contains item lst =
        match lst with
        | []     -> false
        | x::xs  -> match x = item with
                    | true  -> true
                    | false -> contains item xs
    
    let rec StartsWith  subList biglist=
        match biglist,subList with
        |[],a::rest        -> false
        |_,[]              -> true
        |h1::big, h2::smal -> h1=h2 &&  StartsWith smal big 

    let split func list =
        let l1 = list |> List.filter func
        let l2 = list |> List.filter (fun x -> not (func x))
        (l1,l2)
    
    let Replace listToSearch listToReplace mainList =
        let rec GetSubList smallList bigList =
            match smallList, bigList with
            | [], l -> l
            | _, [] -> raise(BugErrorException "")
            | h1::small, h2::big  ->if h1<>h2 then  raise(BugErrorException "")
                                    else GetSubList small big
        listToReplace @ (mainList |> GetSubList listToSearch)

    let last lst =  lst |> List.rev |> List.head

    let rec keepDuplicates lst =
        match lst with
        | []   -> []
        | x::xs -> 
            let dups = xs |> List.filter ((=) x)
            match dups with
            | []    -> keepDuplicates xs
            | _     -> x::(keepDuplicates (xs |> List.filter ((<>) x)))

    let rec keepDuplicatesI (lst:string list) =
        match lst with
        | []   -> []
        | x::xs -> 
            let dups = xs |> List.filter (fun l -> l.icompare x)
            match dups with
            | []    -> keepDuplicatesI xs
            | _     -> x::(keepDuplicatesI (xs |> List.filter (fun l -> not (l.icompare x))))

    let addIf condition itm lst =
        match condition with
        | true  -> itm::lst
        | false -> lst

let mutable _globalID = 1000
let GetGlobalID() =
    _globalID <- _globalID + 1
    _globalID

let IntegerSize() = 8I

let WordSize() = 8I*IntegerSize()

let MaxInt() = if WordSize()=64I then BigInteger(System.Int64.MaxValue) else BigInteger(System.Int32.MaxValue)
let MinInt() = if WordSize()=64I then BigInteger(System.Int64.MinValue) else BigInteger(System.Int32.MinValue)

let GetNumberOfBitsForNonNegativeInteger(a:BigInteger) = 
    BigInteger( System.Math.Ceiling(BigInteger.Log(a+BigInteger(1),2.0)) )

let GetChoiceUperDeterminantLengthInBits(nChoiceAlternatives:BigInteger) = 
    match nChoiceAlternatives with
    | _ when nChoiceAlternatives > 0I   -> BigInteger( System.Math.Ceiling(BigInteger.Log(nChoiceAlternatives,2.0)) )
    | _                                 -> 0I


let GetNumberOfBitsForNonNegativeInt(a:int) = 
    int (System.Math.Ceiling(BigInteger.Log(BigInteger(a)+1I,2.0)) )

let toString x = (sprintf "%A" x).Split(' ').[0].Trim()

let BI (i:int) = BigInteger i




//let bitStringValueToByteArray (s:StringLoc) =
//    let s1 = s.Value.ToCharArray() |> Seq.map(fun x -> if x='0' then 0uy else 1uy) |> Seq.toList
//    let rec BitsToNibbles l:list<byte> =
//        match l with
//        | []                   -> []
//        | i1::[]               -> [i1*8uy]
//        | i1::i2::[]           -> [i1*8uy+i2*4uy]
//        | i1::i2::i3::[]       -> [i1*8uy+i2*4uy+i3*2uy]
//        | i1::i2::i3::i4::tail -> (i1*8uy+i2*4uy+i3*2uy+i4)::(BitsToNibbles tail)           
//    let rec NibblesToBytes l:list<byte> =
//        match l with
//        | []                    -> []
//        | i1::[]                -> [i1*16uy]
//        | i1::i2::tail          -> (i1*16uy+i2)::(NibblesToBytes tail)
//    NibblesToBytes (BitsToNibbles s1) |> List.toArray

let bitStringValueToByteArray (s:StringLoc) =
    let s1 = s.Value.ToCharArray() |> Seq.map(fun x -> if x='0' then 0uy else 1uy) |> Seq.toList
    let BitsToNibbles l:list<byte> =
        let rec BitsToNibblesAux l acc :list<byte> =
            match l with
            | []                   -> List.rev acc
            | i1::[]               -> BitsToNibblesAux [] (i1*8uy::acc)
            | i1::i2::[]           -> BitsToNibblesAux [] ((i1*8uy+i2*4uy)::acc)
            | i1::i2::i3::[]       -> BitsToNibblesAux [] ((i1*8uy+i2*4uy+i3*2uy)::acc)
            | i1::i2::i3::i4::tail -> 
                let newAcc = (i1*8uy+i2*4uy+i3*2uy+i4)::acc
                BitsToNibblesAux tail newAcc
        BitsToNibblesAux l []
    let NibblesToBytes l:list<byte> =
        let rec NibblesToBytesAux l acc:list<byte> =
            match l with
            | []                    -> List.rev acc
            | i1::[]                -> NibblesToBytesAux [] ((i1*16uy)::acc)
            | i1::i2::tail          -> NibblesToBytesAux tail ((i1*16uy+i2)::acc)
        NibblesToBytesAux l []
    NibblesToBytes (BitsToNibbles s1) |> List.toArray

let byteArrayToBitStringValue (bytes: byte seq)  =
    let handleOctet (oct:byte) =
        let handleNibble (n:char) =
            match n with
            |'0'-> "0000"
            |'1'-> "0001"
            |'2'-> "0010"
            |'3'-> "0011"
            |'4'-> "0100"
            |'5'-> "0101"
            |'6'-> "0110"
            |'7'-> "0111"
            |'8'-> "1000"
            |'9'-> "1001"
            |'A'-> "1010"
            |'B'-> "1011"
            |'C'-> "1100"
            |'D'-> "1101"
            |'E'-> "1110"
            |'F'-> "1111"
            | _ -> raise(BugErrorException "")
        let octStr = sprintf "%02X" oct
        (handleNibble octStr.[0]) + (handleNibble octStr.[1])
    bytes |> Seq.map handleOctet  |> Seq.StrJoin ""



//let rec DoTopologicalSort2 independentNodes dependentNodes  comparer excToThrow =
//let rec DoTopologicalSort2 
//                (independentNodes: PrimitiveWithLocation<string> list) 
//                (dependentNodes: (PrimitiveWithLocation<string> * PrimitiveWithLocation<string> list) list)  
//                comparer excToThrow =
let rec DoTopologicalSort2b 
                (independentNodes: 'a list) 
                (dependentNodes: ('a * 'b list) list)  
                comparer excToThrow =
    match independentNodes with
    | []          ->  if List.isEmpty dependentNodes then []
                      else  
                        raise(excToThrow dependentNodes) 
    | head::tail  ->
        let dependentNodes2   = dependentNodes  |> List.map(fun (n,list) -> (n, list |>List.filter(fun x -> (not (comparer x head)))  ) ) 
        let newDependentNodes = dependentNodes2 |> List.filter(fun (_,list) -> not(List.isEmpty list))
        let independentNodes2 = dependentNodes2 |> List.filter(fun (_,list) -> List.isEmpty list) |> List.map fst
        let newIndependentNodes = independentNodes2 @ tail
        head::(DoTopologicalSort2b newIndependentNodes newDependentNodes comparer excToThrow) 

let DoTopologicalSort2 
                    (independentNodes: 'a list) 
                    (dependentNodes: ('a * 'b list) list)  
                    comparer excToThrow =
    let rec DoTopologicalSort3_aux 
                    (independentNodes: 'a list) 
                    (dependentNodes: ('a * 'b list) list)  
                    comparer excToThrow ret=
        match independentNodes with
        | []          ->  ret |> List.rev, dependentNodes
        | head::tail  ->
            let dependentNodes2   = dependentNodes  |> List.map(fun (n,list) -> (n, list |>List.filter(fun x -> (not (comparer x head)))  ) ) 
            let newDependentNodes = dependentNodes2 |> List.filter(fun (_,list) -> not(List.isEmpty list))
            let independentNodes2 = dependentNodes2 |> List.filter(fun (_,list) -> List.isEmpty list) |> List.map fst
            let newIndependentNodes = independentNodes2 @ tail
            let newRet = head::ret
            DoTopologicalSort3_aux newIndependentNodes newDependentNodes comparer excToThrow newRet
    let sorted, unsorted = DoTopologicalSort3_aux independentNodes dependentNodes  comparer excToThrow [] 
    match unsorted with
    | []    -> sorted
    | _     -> raise(excToThrow unsorted) 

let ind = ["A";"B";"C"]
let dep = [("E",["D";"C"]); ("D",["A";"B"])]
//DoTopologicalSort ind dep (fun x -> System.Exception "sdfds")
let rec DoTopologicalSort independentNodes dependentNodes  excToThrow =
    DoTopologicalSort2 independentNodes dependentNodes  (=) excToThrow 
    


//let rec DoTopologicalSort independentNodes dependentNodes  excToThrow =
//    DoTopologicalSort2 independentNodes dependentNodes  (=) excToThrow 

//let a1 = ["a";"d";"e"]
//let a2 = [("b",["c"]); ("c",["b"])]
//
//let res = DoTopologicalSort a1 a2 (fun cyclicNones -> Exception(sprintf "cyclic dependencies %A" cyclicNones))


let CheckUniqueName (nameToCheck:StringLoc) (names:seq<StringLoc>) excToThrow =
    for n in names do
        if nameToCheck.Value = n.Value then
            raise (excToThrow n)

let CheckExists (nameToCheck:StringLoc) (names:seq<StringLoc>)  excToThrow =
    match names |> Seq.tryFind (fun x -> x.Value = nameToCheck.Value) with
    | Some(_)       -> ()
    | None          -> raise(excToThrow)

let CheckNotExists (nameToCheck:StringLoc) (names:seq<StringLoc>)  excToThrow =
    match names |> Seq.tryFind (fun x -> x.Value = nameToCheck.Value) with
    | Some(_)       -> raise(excToThrow)
    | None          -> ()

let PrintLocation loc = sprintf "File:%s, line:%d" loc.srcFilename loc.srcLine


/// Generic function which checks an input sequence of strings*location and if there
/// are duplicate values it raises a user exception 
let CheckForDuplicates<'T when 'T :equality>   (sequence:seq<PrimitiveWithLocation<'T>>) =  
    let duplicates = sequence 
                     |> Seq.map(fun x -> x.AsTupple) 
                     |> Seq.groupBy(fun (name,loc) -> name) |> Seq.filter(fun (n,dups) -> Seq.length dups > 1)
    if not(Seq.isEmpty duplicates) then
        let name = fst ( duplicates |> Seq.head)
        let loc = snd ((snd (duplicates |> Seq.head)) |> Seq.head)
        let errMsg = sprintf "Duplicate definition: %s" (name.ToString())
        raise (SemanticError (loc, errMsg))




let CheckForDuplicatesCI asn1ConstructCheck (lst: StringLoc seq) =
    lst |> 
    Seq.groupBy(fun s -> s.Value.ToLower()) |> 
    Seq.filter(fun (n,dups) -> Seq.length dups > 1) |> 
    Seq.iter(fun (_,dups) -> 
        let head = dups |> Seq.head
        let dupStr = dups |> Seq.map(fun z -> "'" + z.Value + "'") |> Seq.StrJoin ", "
        let errMsg = sprintf "Duplicate %s. Values: %s have the same spelling but different case. Use different names to avoid conflicts in case insentive target languages" asn1ConstructCheck dupStr
        match checkForAdaKeywords () with
        | false   -> ()
        | true    -> raise (SemanticError (head.Location, errMsg)) ) 


//it throws excToThrow if list2 contains an element that does not exist in list1
let CompareLists (list1:List<StringLoc>) (list2:List<StringLoc>) excToThrow =
    list2 |> Seq.iter(fun s2 ->match list1 |> Seq.exists(fun s1 -> s1.Value = s2.Value) with
                               | false -> raise (excToThrow s2)
                               | true -> ())
    



//>>> str="a-b-c-d"
//>>> res=["d", "c-d", "b-c-d", "a-b-c-d"]
//let thanos  =
//    let str = "a-b-c-d"
//    let parts = str.Split('-') |> Seq.toList |> List.rev
//    let res = 
//        (parts,[]) |> Seq.unfold (fun (list1,list2)  -> match list1 with
//                                                        | []  -> None
//                                                        | x::xs -> 
//                                                            let lst2 = x::list2
//                                                            let str = lst2 |> Seq.StrJoin "-"
//                                                            Some(str,(xs,lst2)) ) |> Seq.toList
//    let parts = "a-b-c-d".Split('-') |> Seq.toList 
//    let rec aux lst =
//        match lst with
//        | []    -> []
//        | x::xs -> lst::(aux xs)
//    let res2 = "a-b-c-d".Split('-') |> Seq.toList |> aux |> List.rev |> List.map (Seq.StrJoin "-")
//            
//    res
//    


let replaceErrorCodes (initialString:string) (patternToSearch:string) generalErrCode fileIdx initialCount =
    let sw = new System.IO.StringWriter()
    let rec replaceErrorCodes_aux (stringToProcess:string)  initialCount =
        match stringToProcess.Contains(patternToSearch) with
        | false -> sw.Write(stringToProcess)
        | true  ->
            let pos1 = stringToProcess.IndexOf(patternToSearch)
            let str1 = stringToProcess.Substring(0,pos1)
            let str2 = stringToProcess.Substring(pos1 + patternToSearch.Length)
            let strRep = (generalErrCode<<<28) ||| (fileIdx<<<18) ||| initialCount
            sw.Write(str1)    
            sw.Write(strRep.ToString())    
            replaceErrorCodes_aux str2  (initialCount+1)
    replaceErrorCodes_aux initialString  initialCount
    let ret = sw.ToString();
    sw.Dispose()
    ret
    
