namespace AkkaSingleton 

open System
open Akka.Actor
open Akka.FSharp
open Akka.Cluster
open Akka.Cluster.Tools.Singleton

module Config = 

    let ports = [
        2551
        2552
        2553
    ]

    let config (id:int) = 
        sprintf """ akka {
               coordinated-shutdown.exit-clr = on
               actor.provider = cluster
                remote {
                    dot-netty.tcp {
                        port = %d
                        hostname = localhost
                    }
                }

                cluster {
                    downing-provider-class = "Akka.Cluster.SplitBrainResolver, Akka.Cluster"
                    split-brain-resolver {
                        active-strategy = static-quorum
                        static-quorum {
                            quorum-size = 2
                        }
                    } 
                   seed-nodes = [
                    "akka.tcp://ClusterSystem@localhost:%d"
                    "akka.tcp://ClusterSystem@localhost:%d"
                    "akka.tcp://ClusterSystem@localhost:%d"
                    ] # address of seed node
                   roles = ["singleton", "logger"] # roles this member is in
                   role.["singleton"].min-nr-of-members = 2 
                }
        }
        """ ports.[id] ports.[0] ports.[1] ports.[2]


[<RequireQualifiedAccess>]
module SingletonActor = 

    let create (launch:unit -> IDisposable) (mailbox:Actor<obj>) =

        let disposable = launch ()
        mailbox.Defer (fun () -> disposable.Dispose())
    
        let rec loop () = actor {
            let! msg = mailbox.Receive()
            Write.inRed <| sprintf "msg received %A" msg 
            match msg with
            | :? PoisonPill -> return ()
            | _ -> return! loop ()
        }
        loop ()

module Program = 

    [<EntryPoint>]
    let main argv =
    
        // Next - try having a lighthouse/witness mode.

        printfn "running with args: %A" argv    

        let system = 

            let id = 
                match argv |> Array.toList with
                | [id] -> System.Int32.Parse id
                | _ -> 0

            let config = Configuration.parse <| Config.config id
            ActorSystem.Create ("ClusterSystem", config)

        let createProps (actorFn:Actor<'Message> -> Cont<'Message,'Return>) = 
            let e = Linq.Expression.ToExpression(fun () -> new FunActor<_, _>(actorFn))
            (Props.Create e)

        let singletonLauncher = SingletonActor.create Launcher.launch

        let clusterSingleton = 
            system.ActorOf(ClusterSingletonManager.Props(
                            createProps singletonLauncher,
                            PoisonPill.Instance,
                            ClusterSingletonManagerSettings.Create(system)),
                            "consumer")

        printfn "Running"
        let _ = System.Console.ReadLine ()

        let task = CoordinatedShutdown.Get(system).Run(CoordinatedShutdown.ClrExitReason.Instance)
        let doneTask = task |> Async.AwaitTask |> Async.RunSynchronously

        0
