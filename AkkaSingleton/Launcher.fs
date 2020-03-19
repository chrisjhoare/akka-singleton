namespace AkkaSingleton  

open System

[<RequireQualifiedAccess>]
module Launcher =

    let launch () = 

        Write.inRed "LAUNCHED"
        
        { new IDisposable with member __.Dispose () = Write.inRed "Disposed" }

