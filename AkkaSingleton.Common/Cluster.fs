namespace AkkaSingleton.Common

open Akka.Actor
open Akka.FSharp

module Cluster = 

    

    type ClusterRole = 
        | SingletonHost
        | Witness
        with override x.ToString() = sprintf "%A" x

    type ClusterConfig = {
        ClusterName: string
        SeedPorts: int list
        MemberHostPort: int
        MemberRoles: ClusterRole list
    } with 
        member config.ToAkkaConfig () = 
        
            
            let seedNodes = 
                (config.SeedPorts 
                |> List.map (sprintf "akka.tcp://%s@localhost:%d" config.ClusterName)
                |> sprintf "%A").Replace(";", "\n")
                

            let memberRoles = 
                config.MemberRoles 
                |> List.distinct 
                |> List.map (sprintf "%A")
                |> sprintf "%A"

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
                       role.["%A"].min-nr-of-members = 2
                    }
                }
                """ <| config.MemberHostPort 
                    <| seedNodes
                    <| memberRoles 
                    <| SingletonHost

            Configuration.parse configString


    let baseConfig:ClusterConfig = { 
        ClusterName = "cluster"
        SeedPorts = 
            [ 2551
              2552
              2553 ]
        MemberHostPort = 0
        MemberRoles = []
    }