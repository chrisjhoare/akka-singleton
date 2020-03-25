namespace AkkaSingleton.Common.Util

open System
open System.ComponentModel
open TypeShape.Core


module AkkaExtensions = 

    open Akka.Actor
    open Akka.FSharp

    type Props with
        static member CreateFromActorFn (actorFn:Actor<'Message> -> Cont<'Message,'Return>) =
            let e = Linq.Expression.ToExpression(fun () -> new FunActor<_, _>(actorFn))
            Props.Create e
    

[<RequireQualifiedAccess>]
module Write =

    open System
    
    let inRed msg = 
        System.Console.BackgroundColor <- ConsoleColor.Red
        printfn "%s" msg
        Console.ResetColor();


[<AutoOpen>]
module ConfigurationExtensions = 

    open Microsoft.Extensions.Configuration

    open TypeShape
    open TypeShape.HKT
    open System

    type IConfigurationTypesBuilder<'F,'G> =
        inherit IFSharpRecordBuilder<'F,'G>
        inherit IStringBuilder<'F>
        inherit IFSharpOptionBuilder<'F>
        inherit IInt32Builder<'F>

    let mkGenericProgram (builder:IConfigurationTypesBuilder<'F,'G>) =
        { 
            new IGenericProgram<'F> with
                member this.Resolve<'a>() : App<'F, 'a> = 
                    match shapeof<'a> with
                    | Fold.FSharpOption builder this r -> r
                    | Fold.FSharpRecord builder this r -> r
                    | Fold.String builder r -> r
                    | Fold.Int32 builder r -> r
                    | _ -> failwithf  "unsupported type %O" typeof<'a> 
        }

    type Cloner =
        static member Assign(_ : App<Cloner, 'a>, _ : string -> IConfiguration -> 'a) = ()

    type FieldCloner =
        static member Assign(_ : App<FieldCloner, 'a>, _ : (IConfiguration * string) -> 'a -> 'a) = ()

    type IConfiguration with
        member this.Keys = 
            this.AsEnumerable()
            |> Seq.map (fun kv -> kv.Key)
            |> Set

        static member GetOrFail (path:string) (config:IConfiguration) = 
                if config.Keys.Contains path then
                    config.GetValue<'T>(path)
                else 
                    failwithf "Missing config path %s" path
                


    let configBuilder = 
        { new IConfigurationTypesBuilder<Cloner,FieldCloner> with
                
                member __.Int32() = HKT.pack IConfiguration.GetOrFail 
                member __.String() = HKT.pack IConfiguration.GetOrFail
                        
                member __.Option (HKT.Unpack fc) = 

                    HKT.pack (fun path config -> 
                        
                        if config.Keys.Contains path then
                            Some <| fc path config
                        else
                            None
                    )
                            

                member __.Field shape (HKT.Unpack fc) =
                        
                    HKT.pack (fun (config, prefix) src -> 
                        let path = sprintf "%s%s" prefix shape.Label
                        shape.Set src <| fc path config)

                member __.Record shape (HKT.Unpacks fields) =
                        
                    HKT.pack(fun f config -> 

                        let prefix = 
                            if String.IsNullOrEmpty f then String.Empty 
                            else sprintf "%s:" f

                        let mutable t' = shape.CreateUninitialized()
                        for f in fields do t' <- f (config, prefix) t'
                        t')
              
                
                
                //member __.Field shape (HKT.Unpack fc) = 
                    
                //    HKT.pack(fun src tgt -> shape.Set tgt (fc (shape.Get src)))


                // member __.Record shape (HKT.Unpacks fields) = 

                

               
            }

    //let prettyPrint<'t> = (mkGenericProgram configBuilder).Resolve<'t> () |> HKT.unpack

    open System.Collections.Generic

    let x = 

        let entries = 
            [ 
                KeyValuePair.Create("Field", "item")
                KeyValuePair.Create("Section:Item1", "item")
                KeyValuePair.Create("Section:Item2", "item")
            ] |> Seq.toList

        let config = ConfigurationBuilder().AddInMemoryCollection(entries)
        config.Build() :> IConfiguration
 
    type ConfigSection = {
        Item1: string
        Item2: string
        
    }

    type Config = {
        Field: string
        Temp: string option
        Section: ConfigSection
        OptionalInt: int32 option
        OptionalSection: ConfigSection option
    }

    let mkConfig<'t> = 
        let program = (mkGenericProgram configBuilder).Resolve<'t> () |> HKT.unpack
        fun (c:IConfiguration) -> program String.Empty c

    let testing () =
    
        let configParser = mkConfig<Config>
        let parsed = configParser x
        parsed
    
