namespace AkkaSingleton.Common

module Configuration =

    open Microsoft.Extensions.Configuration
    open System.Collections.Generic
    open TypeShape
    open TypeShape.Core
    open TypeShape.HKT
    open System

    /// Subset of types we want to compose our Config records from. Eg no POCOs
    type IConfigurationTypesBuilder<'F,'G> =
        inherit IFSharpRecordBuilder<'F,'G>
        inherit IStringBuilder<'F>
        inherit IFSharpOptionBuilder<'F>
        inherit IInt32Builder<'F>
        inherit IFSharpListBuilder<'F>
        inherit IBoolBuilder<'F>

    let internal mkGenericProgram (builder:IConfigurationTypesBuilder<'F,'G>) =
        { 
            new IGenericProgram<'F> with
                member this.Resolve<'a>() : App<'F, 'a> = 
                    match shapeof<'a> with
                    | Fold.FSharpOption builder this r -> r
                    | Fold.FSharpRecord builder this r -> r
                    | Fold.String builder r -> r
                    | Fold.Int32 builder r -> r
                    | Fold.FSharpList builder this r -> r
                    | Fold.Bool builder r -> r
                    | _ -> failwithf  "unsupported type %O" typeof<'a> 
        }

    module ConfigValueBuilder = 

        type ConfigValueBuilder =
            static member Assign(_ : App<ConfigValueBuilder, 'a>, _ : Map<string,string> -> string -> 'a -> Map<string,string>) = ()

        type FieldConfigBuilder =
            static member Assign(_ : App<FieldConfigBuilder, 'a>, _ : Map<string,string> -> string -> 'a -> Map<string,string>) = ()

        let private configValueBuilder = 
            { 
                new IConfigurationTypesBuilder<ConfigValueBuilder,FieldConfigBuilder> with

                    member __.List (HKT.Unpack fcs) =

                        HKT.pack (fun kv path items -> 
                                    
                            let fold state (index, item) = 

                                let path = sprintf "%s:%d" path index
                                fcs state path item

                            items |> List.indexed |> List.fold fold kv)
                        

                    member __.Field shape (HKT.Unpack fc) =
                        
                            HKT.pack (fun kv path item -> 
                            
                                let newPath = 
                                
                                    let prefix = 
                                        if String.IsNullOrEmpty path then String.Empty 
                                        else sprintf "%s:" path

                                    sprintf "%s%s" prefix shape.Label

                                let field = shape.Get item
                                fc kv newPath field)
                      
                  
                    member this.Option (HKT.Unpack fc) =
                        HKT.pack (fun kv path item ->  
                                    match item with
                                    | Some v -> fc kv path v
                                    | None -> kv)

                    member this.Record shape (HKT.Unpacks fcs) = 

                        HKT.pack (fun kv path record ->

                            let folder (state:Map<string,string>) item :Map<string,string> =
                                let next = item state path record
                                next

                            fcs |> Array.fold folder kv
                            )
                            

                    member this.String() = HKT.pack (fun kv path item -> kv.Add (path, sprintf "%s" item))
                    member this.Int32() = HKT.pack (fun kv path item -> kv.Add (path, sprintf "%d" item))
                    member this.Bool() = HKT.pack (fun kv path item -> kv.Add (path, sprintf "%O" item))
            }
        
        let mkConfigBuilder<'t> = 
            let program = (mkGenericProgram configValueBuilder).Resolve<'t> () |> HKT.unpack
            fun (t:'t) -> 
                program Map.empty String.Empty t
                |> Map.toList
                |> List.map KeyValuePair.Create
                  
                  
        let buildValidConfiguration (t:'t) = 
            
            let configuration = new ConfigurationBuilder()
            let values = t |> mkConfigBuilder 
            configuration.AddInMemoryCollection(values).Build() :> IConfiguration


    module ConfigBinder = 

        type IConfiguration with
            member this.Keys = this.AsEnumerable() |> Seq.map (fun kv -> kv.Key) |> Set
            static member GetOrFail (path:string) (config:IConfiguration) = 
                    if config.Keys.Contains path then
                        config.GetValue<'T>(path)
                    else 
                        failwithf "Missing config path %s" path

        type ConfigBinder =
            static member Assign(_ : App<ConfigBinder, 'a>, _ : string -> IConfiguration -> 'a) = ()

        type FieldConfigBinder =
            static member Assign(_ : App<FieldConfigBinder, 'a>, _ : (IConfiguration * string) -> 'a -> 'a) = ()

        let private configBuilder = 
            { new IConfigurationTypesBuilder<ConfigBinder,FieldConfigBinder> with
                
                    member __.List (HKT.Unpack fcs) =

                        HKT.pack (fun path config -> 
                                config.GetSection(path).GetChildren()
                                |> Seq.indexed 
                                |> Seq.map (fst >> sprintf "%s:%d" path)
                                |> Seq.map (fun path -> fcs path config)
                                |> Seq.toList
                            )


                    member __.Int32() = HKT.pack IConfiguration.GetOrFail 
                    member __.String() = HKT.pack IConfiguration.GetOrFail
                    member __.Bool() = HKT.pack IConfiguration.GetOrFail
                        
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
                }
        
        let mkConfig<'t> = 
            let program = (mkGenericProgram configBuilder).Resolve<'t> () |> HKT.unpack
            fun (c:IConfiguration) -> program String.Empty c

    


