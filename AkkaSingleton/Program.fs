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

    open System.ComponentModel
    open Util.ConfigurationExtensions

    [<EntryPoint>]
    let main argv =
    
        let v = testing()
        

        let configuration = 
            let builder = ConfigurationBuilder().AddCommandLine(argv).AddEnvironmentVariables()
            builder.Build()

        
        let clusterConfig = configuration.Get<ClusterConfig>()

        let test = configuration.Get<TestConfig>()

        let system = 


            Cluster.createClusterActorSystem clusterConfig


        let singletonLauncher = SingletonActor.create (Launcher.launch (clusterConfig.NodeIp, 8060))

        let clusterSingleton = 
            system.ActorOf(ClusterSingletonManager.Props(
                            Props.CreateFromActorFn singletonLauncher,
                            PoisonPill.Instance,
                            ClusterSingletonManagerSettings.Create(system).WithRole(SingletonHost.ToString())),
                            "consumer")

        printfn "Running"
        
        system.WhenTerminated.Wait()
           
        
        0
