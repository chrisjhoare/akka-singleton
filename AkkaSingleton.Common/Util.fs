namespace AkkaSingleton.Common.Util

open System


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

