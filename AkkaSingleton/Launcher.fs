namespace AkkaSingleton  

open System
open AkkaSingleton.Common.Util
open Grpc.Core

open ProtoBuf.Grpc.Server
open System.Collections.Generic
open System.ServiceModel
open FSharp.Control
open ProtoBuf.FSharp
open System.Threading

open AkkaSingleton.GrpcContract.Contract
open AkkaSingleton.GrpcContract
open System.Reactive.Subjects


module StreamingService = 


    let rec eventSequence () = asyncSeq {
        do! Async.Sleep 1000
        let streamEvent:StreamEventDto = { Event = "Event" }
        yield streamEvent
        yield! eventSequence ()
    }

    type StreamingService() = 

        let eventStream = new Subject<StreamEventDto>()

        member __.Publish () = 
            eventStream.OnNext({ Event = "Event"})

        interface IStreamingService 
            with 
                member __.Subscribe (_:UnitDto) = 
                        eventStream
                        |> AsyncSeq.ofObservableBuffered
                        |> AsyncSeq.toAsyncEnum

        interface IDisposable with
            member __.Dispose () = 
                try 
                    eventStream.OnCompleted()
                    eventStream.Dispose()
                with
                | _ -> ()


open StreamingService

[<RequireQualifiedAccess>]
module Launcher =

    let launch (grpcIp, grpcPort) () = 

        Write.inRed "LAUNCHED"

        printfn "grpc server launching on port %s:%d" grpcIp grpcPort

        let streamService = new StreamingService()

        AkkaSingleton.GrpcContract.Contract.registerSerialiser () |> ignore

        let server = 
            let s = new Server () 
            s.Ports.Add (new ServerPort("0.0.0.0", grpcPort, ServerCredentials.Insecure)) |> ignore
            //s.Ports.Add (new ServerPort("localhost", grpcPort, ServerCredentials.Insecure)) |> ignore

            let service = streamService :> IStreamingService

            s.Services.AddCodeFirst (service) |> ignore
            

            s
        
        server.Start()

        let rec timer () = async {
            do! Async.Sleep 1000
            streamService.Publish ()
            printfn "Published event"
            return! timer ()
        }

        let cts = new CancellationTokenSource()

        let _ = Async.StartAsTask (timer(), cancellationToken=cts.Token)

        printfn "grpc server listening on port %s:%d" grpcIp grpcPort
        

        { new IDisposable 
            with member __.Dispose () = 
                    Write.inRed "Disposed" 
                    ((streamService) :> IDisposable).Dispose ()
                    cts.Cancel()
                    server.ShutdownAsync () |> Async.AwaitTask |> Async.RunSynchronously
        }

