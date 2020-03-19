﻿namespace AkkaSingleton 

open System
open Akka.Actor
open Akka.FSharp
open Akka.Cluster

open Akka.Cluster.Tools.Singleton

open AkkaSingleton.Common.Cluster
open AkkaSingleton.Common
open AkkaSingleton.Common.Util.AkkaExtensions

module Program = 


    [<EntryPoint>]
    let main argv =
    
        printfn "running with args: %A" argv    

        let system = 

            let config =
                let index = 
                    match argv |> Array.toList with
                    | [id] -> System.Int32.Parse id
                    | _ -> 0

                { baseConfig with 
                                MemberHostPort = baseConfig.SeedPorts.[index]
                                MemberRoles = [SingletonHost] 
                            }
            
            ActorSystem.Create (config.ClusterName, config.ToAkkaConfig())


        let singletonLauncher = SingletonActor.create Launcher.launch

        let clusterSingleton = 
            system.ActorOf(ClusterSingletonManager.Props(
                            Props.CreateFromActorFn singletonLauncher,
                            PoisonPill.Instance,
                            ClusterSingletonManagerSettings.Create(system).WithRole(SingletonHost.ToString())),
                            "consumer")

        printfn "Running"
        let _ = System.Console.ReadLine ()

        
        0
