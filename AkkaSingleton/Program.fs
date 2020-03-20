namespace AkkaSingleton 

open System
open Akka.Actor
open Akka.FSharp
open Akka.Cluster

open Akka.Cluster.Tools.Singleton

open AkkaSingleton.Common.Cluster
open AkkaSingleton.Common
open AkkaSingleton.Common.Util.AkkaExtensions
open Microsoft.Extensions.Configuration

module Program = 

    [<EntryPoint>]
    let main argv =
    
        let configuration = 
            let builder = ConfigurationBuilder().AddCommandLine(argv).AddEnvironmentVariables()
            builder.Build()

        let system = 

            let clusterConfig = ClusterConfig.Empty
            configuration.Bind (clusterConfig)

            Cluster.createClusterActorSystem clusterConfig


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
