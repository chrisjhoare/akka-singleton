namespace AkkaSingleton  

open System
open AkkaSingleton.Common.Util

[<RequireQualifiedAccess>]
module Launcher =

    let launch () = 

        Write.inRed "LAUNCHED"
        
        { new IDisposable with member __.Dispose () = Write.inRed "Disposed" }

