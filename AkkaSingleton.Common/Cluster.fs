namespace AkkaSingleton.Common

open Akka.Actor
open Akka.FSharp
open Microsoft.Extensions.Configuration

module Cluster = 

    type ClusterRole = SingletonHost | Witness


    [<CLIMutable>]
    type ClusterConfig = {
        ClusterName: string
        SeedNodes: ResizeArray<string>
        NodeIp: string
        NodePort: int
        IsHost: bool

    } with static member Empty = { ClusterName = ""; NodeIp = ""; SeedNodes = ResizeArray<_>(); NodePort = 0; IsHost = false }

    let private createAkkaConfig (config:ClusterConfig) = 
        
        let publichost = 
            config.NodeIp
            |> Option.ofObj
            |> Option.bind (fun s -> if System.String.IsNullOrEmpty s then None else Some s)
            |> Option.defaultWith (fun () -> System.Net.Dns.GetHostName())


        let seedNodes = 
            (config.SeedNodes
            |> Seq.map (sprintf "akka.tcp://%s@%s" config.ClusterName)
            |> Seq.toList
            |> sprintf "%A").Replace(";", "\n")
                

        let memberRoles = 
            seq {
                if config.IsHost then yield SingletonHost
                yield Witness
            }
            |> Seq.map (sprintf "%A" >> sprintf "%A")
            |> fun s -> System.String.Join(",", s)
            |> sprintf "[%s]"

        let configString = 
            sprintf """ akka {
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
                    downing-provider-class = "Akka.Cluster.SplitBrainResolver, Akka.Cluster"
                    split-brain-resolver {
                        active-strategy = static-quorum
                        static-quorum {
                            quorum-size = 2
                        }
                    } 
                    seed-nodes = %s
                    roles = %s
                    min-nr-of-members = 2
                }
            }
            """ <| config.NodePort 
                <| publichost
                <| seedNodes
                <| memberRoles 

        printfn "ClusterConfig: %s" configString
        Configuration.parse configString


    let createClusterActorSystem (config:ClusterConfig) = 

        let akkaConfig = config |> createAkkaConfig
        ActorSystem.Create (config.ClusterName, akkaConfig)
        

        
        
            


    