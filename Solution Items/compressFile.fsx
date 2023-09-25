#r "System.Security.Cryptography.dll"
open System
open System.IO
open System.Linq

let compressFile sourceFile compressedFilePath  = 
    //Compress with GZIP
    use outputFileStream = new FileStream(compressedFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)
    use compressedFileStream = new Compression.GZipStream(outputFileStream, Compression.CompressionLevel.Optimal)
    use inputFileStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
    inputFileStream.Seek(0L, SeekOrigin.Begin) |> ignore
    inputFileStream.CopyTo(compressedFileStream)

let getFileHash fileName =
    // Use a FIPS compatible hashing algorithm
    // See https://learn.microsoft.com/en-us/dotnet/core/compatibility/cryptography/5.0/instantiating-default-implementations-of-cryptographic-abstractions-not-supported
    use csp = System.Security.Cryptography.SHA256.Create()
    use fstrm = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)
    fstrm.Seek(0L, SeekOrigin.Begin) |> ignore
    csp.ComputeHash(fstrm)

let AreFilesSame leftFileName rightFileName =
    //Files can't be the same if one doesn't exist
    if File.Exists(leftFileName) && File.Exists(rightFileName) then
        getFileHash(leftFileName).SequenceEqual(getFileHash(rightFileName))
    else
        false

let args : string array = fsi.CommandLineArgs |> Array.tail

if args.Length < 2 then
    printf "Error: Not Enough arguments: need to pass the source and destination filenames.\n"
    exit 1

if System.IO.File.Exists(args.[0]) <> true then
    printf $"Error: source file '{args.[0]}' does not exist.\n"
    exit 2

let tempfile = System.IO.Path.GetTempFileName()
let sourceFile = args.[0]
let compressedFile = args.[1]

compressFile sourceFile tempfile
if AreFilesSame compressedFile tempfile <> true then
    File.Delete(compressedFile)
    File.Move(tempfile, compressedFile)
    printf $"Updated compressed file {compressedFile}\n"
else
    File.Delete(tempfile)
