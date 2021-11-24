namespace Cluster

open System
open Akka.Actor
open Akka.FSharp
open Microsoft.Extensions.Configuration
open Akka
open Akka.Cluster.Sharding
open Akka.Cluster.Tools.PublishSubscribe
open Akka.Cluster.Tools.Singleton

[<AutoOpen>]
module Utils = 

    type Props with
        static member CreateFromActorFn (actorFn:Actor<'Message> -> Cont<'Message,'Return>) =
            let e = Linq.Expression.ToExpression(fun () -> new FunActor<_, _>(actorFn))
            Props.Create e

module Common = 

    type ClusterRole = SeedNode | Worker
    

    let workerNodes = [
          "127.0.0.1", 5000
          "127.0.0.1", 5001
          "127.0.0.1", 5002
    ]

    type ClusterConfig = {
        ClusterName: string
        SeedNodes: (string * int32) list
        NodeIp: string
        NodePort: int
        Roles: ClusterRole list
    } with static member Create ((nodeIp, nodePort), roles) = {
            ClusterName = "cluster"
            SeedNodes = [workerNodes |> List.last]
            NodeIp = nodeIp
            NodePort = nodePort
            Roles = roles
        }
    
    let private createAkkaConfig (config:ClusterConfig) = 
            
        let publichost = 
            config.NodeIp
            |> Option.ofObj
            |> Option.bind (fun s -> if System.String.IsNullOrEmpty s then None else Some s)
            |> Option.defaultWith (fun () -> System.Net.Dns.GetHostName())
    
    
        let seedNodes = 
            (config.SeedNodes
            |> Seq.map (fun (host, ip) -> sprintf "akka.tcp://%s@%s:%d" config.ClusterName host ip)
            |> Seq.toList
            |> sprintf "%A").Replace(";", "\n")
                    
    
        let memberRoles = 
            config.Roles
            |> Seq.map (sprintf "%A" >> sprintf "%A")
            |> fun s -> System.String.Join(",", s)
            |> sprintf "[%s]"
    
       
        ////min-nr-of-members = 2
        let configString = 
            sprintf """ akka {
                
                actor {
                    serializers {
                        hyperion = "Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion"
                    }
                    serialization-bindings {
                        "System.Object" = hyperion
                    }
                }

                coordinated-shutdown.exit-clr = on
                actor.provider = cluster
                remote {
                    dot-netty.tcp {
                        port = %d
                        public-hostname = %s
                        hostname = 0.0.0.0
                    }
                }
                cluster {
                    downing-provider-class = "Akka.Cluster.SBR.SplitBrainResolverProvider, Akka.Cluster"
                    split-brain-resolver {
                        active-strategy = keep-majority
                    } 
                    sharding { 
                        remember-entities = true
                        state-store-mode = ddata
                        waiting-for-state-timeout = 10s
                    }
                    seed-nodes = []
                    roles = []
                    
                }
            }
            """ <| config.NodePort 
                <| publichost
               // <| seedNodes
                //  <| memberRoles 
    
        printfn "ClusterConfig: %s" configString
        Configuration.parse configString
    
    
    let createClusterActorSystem (config:ClusterConfig) = 
    
        let akkaConfig = config |> createAkkaConfig

        let akkaConfig = 
            akkaConfig
                .WithFallback(ClusterSharding.DefaultConfig())
                .WithFallback(DistributedPubSub.DefaultConfig())
                .WithFallback(ClusterSingletonManager.DefaultConfig())

        ActorSystem.Create (config.ClusterName, akkaConfig)
    
[<AutoOpen>]
module Messages = 

    type FixtureId = private FixtureId of string
        with 
            static member Create (s:string) = FixtureId.FixtureId s
            member x.Id = 
                let (FixtureId.FixtureId id) = x
                id

    type UpdateType = 
        | Loaded 
        | Appended

    type EventUpdate = {
        Id: string
        
        Events: string list
    }

    type AggregateMessage<'C> = 
        | Load 
        | HandleCommand of 'C

    type Command = 
        | PriceUpdate
        | SomeOtherUpdate

[<RequireQualifiedAccess>]
module AggregateWorker = 
    
    type ShardMessage<'C> = {
        FixtureId: FixtureId
        Message: AggregateMessage<'C>
    }

    let createMessageExtractor<'T>() = 

        { new IMessageExtractor  with
              member this.EntityId(message: obj): string = 
                    match message with 
                    | :? ShardMessage<'T> as s -> s.FixtureId.Id
                    | :? ShardRegion.StartEntity as s -> s.EntityId
                    | _ -> failwith "invalid msg type"

              member this.EntityMessage(message: obj) =
                    match message with 
                    | :? ShardMessage<'T> as s -> s.Message
                    | _ -> failwith "invalid msg type"

              member this.ShardId(message: obj): string = 
                    match message with 
                    | :? ShardMessage<'T> as s -> s.FixtureId.Id
                    | :? ShardRegion.StartEntity as s -> s.EntityId
                    | _ -> failwith "invalid msg type"
        }

    let createAggregateActor<'Command> (entityId:string) (nodeIndex:int32) (mailbox:Actor<AggregateMessage<'Command>>) = 
        
        printfn $"ACTIVATED:{entityId}"

        let rec loop () = 
            actor {
                let! (msg:AggregateMessage<'Command>) = mailbox.Receive()
                match msg with 
                | Load ->
                    printfn "loading actor"
                    let loaded:EventUpdate = {
                        Id = entityId
                        Events = ["Some"; "someother"; "Another event"]
                    }
                    mailbox.Context.System.EventStream.Publish loaded
                | AggregateMessage.HandleCommand c ->
                    
                    printfn "received command"
                    let command:EventUpdate = {
                        Id = entityId
                        Events = ["Some"; "someother"; "Some cmd event"]
                    }
                    mailbox.Context.System.EventStream.Publish command
                
                return! loop ()
            }

        mailbox.Context.Self.Tell (AggregateMessage<'Command>.Load)

        loop ()

    let createEventListener (mailbox:Actor<EventUpdate>) = 
        
        let rec loop () = 
            actor {
                let! (msg:EventUpdate) = mailbox.Receive()
                printfn "%A" msg
            }
        mailbox.Context.System.EventStream.Subscribe(mailbox.Self, typeof<EventUpdate>) |> ignore
        loop ()


    let createWorkerNode (index:int32) = 

        let address = Common.workerNodes[index]

        let config = Common.ClusterConfig.Create (address, [Common.ClusterRole.Worker])
        let system = Common.createClusterActorSystem (config)

        let mutable aggregate:IActorRef = ActorRefs.Nobody

        let listener = spawn system "event-listener" createEventListener

        let cluster = Cluster.Cluster.Get(system)
        cluster.Join(cluster.SelfAddress)

        cluster.RegisterOnMemberUp(fun () -> 

            let aggregateRegion = 
                ClusterSharding.Get(system).Start(
                    "worker", 
                    (fun entityId -> Props.CreateFromActorFn (createAggregateActor<Command> entityId index)), 
                    ClusterShardingSettings.Create(system), //.WithRole("Worker"),
                    createMessageExtractor<Command>())

            aggregate <- aggregateRegion
            printfn "Cluster member up"
            
        )

        let mutable running = true

        while running do
            printfn "enter a fixture or q to exit"
            let fixtureId = System.Console.ReadLine()
            match fixtureId with 
            | "q" | "Q" -> running <- false
            | _ -> aggregate.Tell ({ FixtureId = FixtureId.Create fixtureId; Message = AggregateMessage.HandleCommand Command.PriceUpdate})
        
        cluster.Leave(cluster.SelfAddress)
        
        0


module Program = 

    let [<Literal>] seed = "seed"
    
    [<EntryPoint>]
    let main args = 
        
        match args |> Array.toList with 
        | [index]     -> AggregateWorker.createWorkerNode (Int32.Parse index)
        | _ ->
            printfn "invalid args"
            0
            


    
