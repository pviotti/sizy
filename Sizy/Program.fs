open System
open System.IO

type Dir = Directory

let rec getSize path =
    try
        let attr = File.GetAttributes path
        if attr.HasFlag FileAttributes.Directory then
            Dir.EnumerateFileSystemEntries path
            |> Seq.map getSize
            |> Seq.sum
        else
            (new FileInfo(path)).Length
    with ex ->
        eprintfn "Error: %s" ex.Message
        0L

let sizeUnits = [ "B"; "KiB"; "MiB"; "GiB"; "TiB"; "PiB"; "EiB" ]
let getSizeString (bytes: int64) =
    if bytes = 0L then
        (0.0, sizeUnits.[0])
    else
        let bytesF = float (bytes)
        let sizeUnitsIdx = Math.Floor(Math.Log(bytesF, 1024.0))
        let num = Math.Round(bytesF / Math.Pow(1024.0, sizeUnitsIdx), 0)
        (num, sizeUnits.[int (sizeUnitsIdx)])

let printFormatted (path: string, size: int64) =
    let name = Array.last (path.Split '/')
    let (newSize, sizeUnit) = getSizeString size
    printfn "%10.0f %-3s %s" newSize sizeUnit name


[<EntryPoint>]
let main argv =
    let path =
        if argv.Length > 0 then argv.[0] else Dir.GetCurrentDirectory()

    let lsF = Dir.EnumerateFiles path
    let lsD = Dir.EnumerateDirectories path
    let sizeF = Seq.map getSize lsF
    let sizeD = Seq.map getSize lsD
    let sizeTot = Seq.append sizeF sizeD |> Seq.sum
    let print ls sizes = Seq.zip ls sizes |> Seq.iter printFormatted
    print lsD sizeD
    print lsF sizeF
    printfn "%s" (String.replicate 14 "-")
    printFormatted ("", sizeTot)
    0