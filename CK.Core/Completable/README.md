# Completable & Completion

These "futures" or "yet another promises" are based on Task/TaskCompletionSource. They extend the capacities of the
Tasks by bringing:

- Covariance of the completion result.
- Optional extension points to map errors (exceptions or cancellation) to regular results.

The read only and covariant model are the 2 interfaces [`ICompletion`](ICompletion.cs) (no result) and
[`ICompletion<out TResul> : ICompletion`](ICompletionT.cs).

The real stuff are the sources of completion: [CompletionSource](CompletionSource.cs)
and [`CompletionSource<TResult>`](CompletionSourceT.cs).

A [Completable](ICompletable.cs) is not, in itself, the important object here (note that [`ICompletable<TResult>`](ICompletableT.cs)
is not covariant). A completable simply holds a completion and carries the OnError/OnCanceled/OnCompleted extension points,
it is more to be used as an implementation detail than to expose it: the 3 extension points should typically be explicitly implemented.


## Only Try...
Implementation is lock-free. But to be lock-free and also free of race conditions, [CompletionSource](CompletionSource.cs)
and [`CompletionSource<TResult>`](CompletionSourceT.cs) only expose `TrySetResult`,  `TrySetException` and `TrySetCanceled` methods.
The `void SetXXX` variants are not supported and this is for the better, the Set methods are not concurrent friendly anyway and should
be avoided.
