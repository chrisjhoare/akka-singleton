namespace AkkaSingleton

module AkkaExtensions = 

    open Akka.Actor
    open Akka.FSharp

    type Props with
        static member Create (actorFn:Actor<'Message> -> Cont<'Message,'Return>) =
            let e = Linq.Expression.ToExpression(fun () -> new FunActor<_, _>(actorFn))
            Props.Create e
    
        