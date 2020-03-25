// Learn more about F# at http://fsharp.org

open System
open AkkaSingleton.GrpcContract
open AkkaSingleton.GrpcContract.Contract
open ProtoBuf.Grpc.Client
open Grpc.Core
open System.Threading
open FSharp.Control

[<EntryPoint>]
let main argv =
    
    Contract.registerSerialiser () |> ignore

    let host = "localhost"
    let port = 8040


    
    let channel = new Channel (host, port, ChannelCredentials.Insecure)

    let service = channel.CreateGrpcService<IStreamingService>()

    //channel.ConnectAsync () |> Async.AwaitTask |> Async.RunSynchronously


    
    let rec work () = async {

        printfn "Connecting..."

        try 
            do! 
                service.Subscribe UnitDto.Instance
                |> AsyncSeq.ofAsyncEnum
                |> AsyncSeq.iter (printfn "%A")
            printfn "stream finished"
            do! Async.Sleep (1000) // let it restart.

        with
        | e ->
            printfn "stream exception %s" e.Message
            do! Async.Sleep (10000)
            
        return! work ()

    }

    let cts = new CancellationTokenSource()
    
    let _ = Async.Start(work (), cts.Token)

    let _ = System.Console.ReadLine ()
    cts.Cancel ()
    printfn "finished"

    0

    

