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
        NodePort: int
        IsHost: bool
    } with static member Empty = { ClusterName = ""; SeedNodes = ResizeArray<_>(); NodePort = 0; IsHost = false }

    let private createAkkaConfig (config:ClusterConfig) = 
        
        let seedNodes = 
            (config.SeedNodes
            |> Seq.map (sprintf "akka.tcp://%s@%s" config.ClusterName)
            |> sprintf "%A").Replace(";", "\n")
                

        let memberRoles = 
            seq {
                if config.IsHost then yield SingletonHost
                yield Witness
            }
            |> Seq.map (sprintf "%A")
            |> fun s -> System.String.Join(",", s)
            |> sprintf "[%s]"

        let configString = 
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
                    seed-nodes = %s
                    roles = %s
                    min-nr-of-members = 2
                }
            }
            """ <| config.NodePort 
                <| seedNodes
                <| memberRoles 

        Configuration.parse configString


    let createClusterActorSystem (config:ClusterConfig) = 

        let akkaConfig = config |> createAkkaConfig
        ActorSystem.Create (config.ClusterName, akkaConfig)
        

        
        
            


    