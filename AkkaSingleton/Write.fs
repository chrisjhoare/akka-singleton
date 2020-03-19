namespace AkkaSingleton 

[<RequireQualifiedAccess>]
module Write =

    open System

    
    let inRed msg = 
        System.Console.BackgroundColor <- ConsoleColor.Red
        printfn "%s" msg
        System.Console.BackgroundColor <- ConsoleColor.Black

