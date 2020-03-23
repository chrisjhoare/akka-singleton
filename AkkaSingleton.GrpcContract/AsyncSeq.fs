namespace AkkaSingleton.GrpcContract

[<RequireQualifiedAccess>]
module AsyncSeqConvert = 

    open FSharp.Control
    open System
    open System.Threading.Tasks
    open System.Threading

    let ofAsyncEnum (source: Collections.Generic.IAsyncEnumerable<_>) = asyncSeq {
          let! ct = Async.CancellationToken
          let e = source.GetAsyncEnumerator(ct)
          use _ = 
              { new IDisposable with 
                  member __.Dispose() = 
                      e.DisposeAsync().AsTask() |> Async.AwaitTask |> Async.RunSynchronously }

          let mutable currentResult = true
          while currentResult do
              let! r = e.MoveNextAsync().AsTask() |> Async.AwaitTask
              currentResult <- r
              if r then yield e.Current
    }

    let toAsyncEnum (source: AsyncSeq<'a>) = {
        new Collections.Generic.IAsyncEnumerable<'a> with
              member __.GetAsyncEnumerator(cancellationToken: CancellationToken) = 
                    let mutable current = Unchecked.defaultof<_>
                    let enumerator = source.GetEnumerator()
                    { new Collections.Generic.IAsyncEnumerator<'a> with
                        member __.Current = current
                        member __.MoveNextAsync() = 
                            let moveNextAsync = async {
                                let! enumerationResult = enumerator.MoveNext()
                                match enumerationResult with
                                | Some(v) -> 
                                    current <- v
                                    return true
                                | _ -> return false
                            }

                            Async.StartAsTask(moveNextAsync, cancellationToken = cancellationToken) |> ValueTask<bool>
                        member __.DisposeAsync() =
                            enumerator.Dispose()
                            ValueTask()
                    }
      }

