module Sizy.Program

open Sizy.Config

open System
open System.IO.Abstractions
open System.Collections.Generic
open System.Collections.Concurrent
open FSharp.Collections.ParallelSeq

let SizeUnits = [ "B"; "k"; "M"; "G"; "T"; "P"; "E" ]

type Entry(path: string, size: int64, isDir: bool, sep: char) =

    member this.name =
        Array.last (path.Split sep) + if isDir then string sep else ""

    member this.size = size
    member this.isDir = isDir

let rec getSize (fs: IFileSystem) (fsEntries: IDictionary<_, _>) (errors: IDictionary<_, _>) path =
    try
        let attr = fs.File.GetAttributes path

        let size, isDir =
            if attr.HasFlag System.IO.FileAttributes.Directory
            then fs.Directory.EnumerateFileSystemEntries path |> Seq.sumBy (getSize fs fsEntries errors), true
            else fs.FileInfo.FromFileName(path).Length, false
        fsEntries.[path] <- Entry(path, size, isDir, fs.Path.DirectorySeparatorChar)
        size
    with ex ->
        errors.[path] <- ex.Message
        0L

let sizyMain (fs: IFileSystem, path: string) =
    let fsEntries = ConcurrentDictionary<string, Entry>()
    let errors = ConcurrentDictionary<string, string>()
    let ls = fs.Directory.EnumerateFileSystemEntries path
    let sizes = PSeq.map (getSize fs fsEntries errors) ls
    let totSize = PSeq.sum sizes
    ls, fsEntries, totSize, errors

let getSizeUnit2 bytes =
    if bytes <= 0L then
        0.0, SizeUnits.[0]
    else
        let bytesF = float (bytes)
        let sizeUnitsIdx = Math.Floor(Math.Log(bytesF, 1024.0))
        let num = Math.Round(bytesF / Math.Pow(1024.0, sizeUnitsIdx), 0)
        num, SizeUnits.[int (sizeUnitsIdx)]

let getSizeUnit bytes =
    if bytes <= 0L then
        0.0, SizeUnits.[0]
    elif bytes >= 0x1000000000000000L then
        Math.Round(float(bytes >>> 50) / 1024.0, 0), SizeUnits.[6]
    elif bytes >= 0x4000000000000L then
        Math.Round(float(bytes >>> 40) / 1024.0), SizeUnits.[5]
    elif bytes >= 0x10000000000L then
        Math.Round(float(bytes >>> 30) / 1024.0), SizeUnits.[4]
    elif bytes >= 0x40000000L then
        Math.Round(float(bytes >>> 20) / 1024.0), SizeUnits.[3]
    elif bytes >= 0x100000L then
        Math.Round(float(bytes >>> 10) / 1024.0), SizeUnits.[2]
    elif bytes >= 0x400L then
        Math.Round(float(bytes) / 1024.0), SizeUnits.[1]
    else
        float(bytes), SizeUnits.[0]

let getSizeString name size =
    let newSize, sizeUnit = getSizeUnit size
    sprintf "%10.0f %-1s %s\n" newSize sizeUnit name

[<EntryPoint>]
let main argv =
    match Config.getConfiguration argv with
    | Config config ->
        let fs = new FileSystem()

        let path =
            if config.Contains InputPath then config.GetResult InputPath else fs.Directory.GetCurrentDirectory()

        let stopWatch = Diagnostics.Stopwatch.StartNew()
        let (ls, fsEntries, totSize, errors) = sizyMain (fs, path)

        let print f =
            PSeq.filter f ls
            |> PSeq.sort
            |> Seq.iter (fun p -> printf "%s" (getSizeString fsEntries.[p].name fsEntries.[p].size))
        print (fun x -> fsEntries.ContainsKey x && fsEntries.[x].isDir)
        print (fun x -> fsEntries.ContainsKey x && not fsEntries.[x].isDir)

        let totSize, totSizeUnit = getSizeUnit totSize
        printfn "%s\n%10.0f %-1s" (String.replicate 12 "-") totSize totSizeUnit

        Seq.iter (fun x ->
            eprintfn "\n\t%s - %s" x errors.[x]) errors.Keys

        eprintfn "Exec time: %f" stopWatch.Elapsed.TotalMilliseconds
        0
    | ReturnVal ret -> ret
