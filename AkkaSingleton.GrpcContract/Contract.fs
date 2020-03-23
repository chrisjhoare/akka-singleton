namespace AkkaSingleton.GrpcContract

open System.ServiceModel
open System.Collections.Generic
open ProtoBuf.FSharp

module Contract =

    type UnitDto = { Unit: int } with static member Instance = { Unit = 1 }    

    type StreamEventDto = { Event: string }

    [<ServiceContract(Name = "Streaming.Service")>]
    type IStreamingService =
        abstract member Subscribe: UnitDto -> IAsyncEnumerable<StreamEventDto>


    let registerSerialiser() = 

        Serialiser.defaultModel
            |> Serialiser.registerRecordIntoModel<UnitDto>
            |> Serialiser.registerRecordIntoModel<StreamEventDto>
        