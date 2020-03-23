namespace AkkaSingleton.Common 

open Akka.Actor
open AkkaSingleton.Common.Util

[<RequireQualifiedAccess>]
module SingletonActor = 

    open System
    open Akka.FSharp
    
    let create (launch:unit -> IDisposable) (mailbox:Actor<PoisonPill>) =

        printfn "Singleton launched"
        let disposable = launch ()
        mailbox.Defer (fun () -> disposable.Dispose())
    
        let rec loop () = actor {
            let! msg = mailbox.Receive()
            Write.inRed <| sprintf "msg received %A" msg 
            return ()
        }
        loop ()

