namespace AkkaSingleton.Witness

open System
open Akka.Actor
open Akka.FSharp
open Akka.Cluster

open Akka.Cluster.Tools.Singleton

open AkkaSingleton.Common.Cluster
open AkkaSingleton.Common
open AkkaSingleton.Common.Util.AkkaExtensions
open Microsoft.Extensions.Configuration
open AkkaSingleton.Common.Configuration

module Program = 

    [<EntryPoint>]
    let main argv =
    
        let configuration = 
            let builder = ConfigurationBuilder().AddCommandLine(argv).AddEnvironmentVariables()
            builder.Build()
            

        let clusterConfigBinder = ConfigBinder.mkConfig<ClusterConfig>

        let clusterConfig = clusterConfigBinder configuration

        let system = 

            Cluster.createClusterActorSystem clusterConfig

        printfn "Running"
        let _ = System.Console.ReadLine ()

        system.WhenTerminated.Wait()
           

        printfn "Closing"
        0
