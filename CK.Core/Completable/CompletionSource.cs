using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Core;

/// <summary>
/// This is a TaskCompletionSource-like object that allows exceptions or cancellations
/// to be ignored (see <see cref="ICompletable.OnError(Exception, ref OnError)"/>) and
/// <see cref="ICompletable.OnCanceled(ref OnCanceled)"/>.
/// <para>
/// This is the counterpart of the <see cref="CompletionSource{TResult}"/> that can transform
/// errors or cancellations into specific results.
/// It's much more useful than this one, but for the sake of symmetry, this "no result" class exposes the same functionalities.
/// </para>
/// <para>
/// By design, the completable is not exposed by the completion (it may be an adapter that has no real semantics).
/// If required (for instance if the completable is a Command that should be accessible from its completion), this can be easily
/// done without changing this base implementation: simply create a generic Completion&lt;TCompletable&gt; where TCompletable is ICompletable
/// and expose the protected <see cref="Holder"/> property as a TCompletable Completable property.
/// </para>
/// </summary>
public class CompletionSource : ICompletion, ICompletionSource
{
    readonly TaskCompletionSource _tcs;
    readonly ICompletable _holder;
    volatile Exception? _exception;
    int _state;
    const int StateSuccess = 1;
    const int StateCancel = 2;
    const int StateFailed = 3;

    /// <summary>
    /// Creates a <see cref="CompletionSource"/>.
    /// </summary>
    /// <param name="holder">The completion's holder.</param>
    public CompletionSource( ICompletable holder )
    {
        Throw.CheckNotNullArgument( holder );
        _tcs = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
        _holder = holder;
        // Continuation that handles the error (if any): this prevents the UnobservedTaskException to
        // be raised during GC (Task's finalization).
        _ = _tcs.Task.ContinueWith( static r => r.Exception!.Handle( e => true ),
                                    default,
                                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                                    TaskScheduler.Default );
    }

    /// <summary>
    /// Gets the command that holds this completion.
    /// </summary>
    protected ICompletable Holder => _holder;

    /// <inheritdoc />
    public Task Task => _tcs.Task;

    /// <summary>
    /// Gets an awaiter for this completion.
    /// </summary>
    public TaskAwaiter GetAwaiter() => Task.GetAwaiter();

    /// <inheritdoc />
    public Exception? OriginalException => _tcs.Task.IsCompleted ? _exception : null;

    /// <inheritdoc />
    public bool IsCompleted => _tcs.Task.IsCompleted;

    /// <inheritdoc />
    public bool HasSucceed => _tcs.Task.IsCompleted && Volatile.Read( ref _state ) == StateSuccess;

    /// <inheritdoc />
    public bool HasFailed => _tcs.Task.IsCompleted && Volatile.Read( ref _state ) == StateFailed;

    /// <inheritdoc />
    public bool HasBeenCanceled => _tcs.Task.IsCompleted && Volatile.Read( ref _state ) == StateCancel;

    /// <summary>
    /// Attempts to transition the <see cref="Task"/> into the <see cref="TaskStatus.RanToCompletion"/> state.
    /// </summary>
    /// <returns>
    /// True if the operation was successful; false if the operation was unsuccessful.
    /// </returns>
    public bool TrySetResult()
    {
        if( Interlocked.CompareExchange( ref _state, StateSuccess, 0 ) != 0 ) return false;
        _tcs.SetResult();
        _holder.OnCompleted();
        return true;
    }

    /// <summary>
    /// Enables <see cref="ICompletable.OnError(Exception, ref OnError)"/> to
    /// transform exceptions into successful or canceled completion.
    /// </summary>
    public ref struct OnError
    {
        internal Exception? ResultError;
        internal bool Called;
        internal bool ResultCancel;

        internal OnError( CompletionSource c )
        {
            Called = false;
            ResultError = null;
            ResultCancel = false;
        }

        internal static void ThrowMustBeCalledOnlyOnce()
        {
            throw new InvalidOperationException( "OnError methods must be called only once." );
        }

        /// <summary>
        /// Sets the original exception (or another one).
        /// </summary>
        /// <param name="ex">The exception to set.</param>
        public void SetException( Exception ex )
        {
            Throw.CheckNotNullArgument( ex );
            if( Called ) ThrowMustBeCalledOnlyOnce();
            Called = true;
            ResultError = ex;
        }

        /// <summary>
        /// Sets a successful completion instead of an error.
        /// <para>
        /// Note that the <see cref="CompletionSource.HasFailed"/> will be true: the fact that the command
        /// did not properly complete is available.
        /// </para>
        /// </summary>
        public void SetResult()
        {
            if( Called ) ThrowMustBeCalledOnlyOnce();
            Called = true;
        }

        /// <summary>
        /// Sets a cancellation completion instead of an error.
        /// <para>
        /// Note that the <see cref="CompletionSource.HasFailed"/> will be true: the fact that the command
        /// did not properly complete is available.
        /// </para>
        /// </summary>
        public void SetCanceled()
        {
            if( Called ) ThrowMustBeCalledOnlyOnce();
            Called = true;
            ResultCancel = true;
        }

    }

    /// <inheritdoc />
    public bool TrySetException( Exception exception )
    {
        if( Interlocked.CompareExchange( ref _state, StateFailed, 0 ) != 0 ) return false;

        // The original exception may be observed before the completion.
        // This doesn't really hurt and the OriginalException getter is protected by a check
        // of the Task.IsCompleted anyway.
        _exception = exception;

        var o = new OnError( this );
        try
        {
            _holder.OnError( exception, ref o );
        }
        catch( Exception ex )
        {
            _tcs.SetException( ex );
            throw;
        }
        if( !o.Called ) ThrowOnErrorCalledRequired( _tcs.SetException );
        if( o.ResultError != null )
        {
            _tcs.SetException( o.ResultError );
        }
        else if( o.ResultCancel )
        {
            _tcs.SetCanceled();
        }
        else
        {
            _tcs.SetResult();
        }
        _holder.OnCompleted();
        return true;
    }

    /// <summary>
    /// Enables <see cref="ICompletable.OnCanceled(ref OnCanceled)"/> to
    /// transform cancellations into successful completions.
    /// </summary>
    public ref struct OnCanceled
    {
        internal bool Called;
        internal bool ResultSuccess;

        internal OnCanceled( CompletionSource c )
        {
            Called = false;
            ResultSuccess = false;
        }
        internal static void ThrowMustBeCalledOnlyOnce()
        {
            throw new InvalidOperationException( "OnCanceled methods must be called only once." );
        }

        /// <summary>
        /// Sets a successful completion instead of a cancellation.
        /// <para>
        /// Note that the <see cref="HasBeenCanceled"/> will be true: the fact that the command
        /// has been canceled is available.
        /// </para>
        /// </summary>
        public void SetResult()
        {
            if( Called ) ThrowMustBeCalledOnlyOnce();
            Called = true;
            ResultSuccess = true;
        }

        /// <summary>
        /// Sets the cancellation completion.
        /// </summary>
        public void SetCanceled()
        {
            if( Called ) ThrowMustBeCalledOnlyOnce();
            Called = true;
        }

    }

    /// <inheritdoc />
    public bool TrySetCanceled()
    {
        if( Interlocked.CompareExchange( ref _state, StateCancel, 0 ) != 0 ) return false;
        var o = new OnCanceled( this );
        try
        {
            _holder.OnCanceled( ref o );
        }
        catch( Exception ex )
        {
            _tcs.SetException( ex );
            throw;
        }
        if( !o.Called ) ThrowOnCancelCalledRequired( _tcs.SetException );
        if( o.ResultSuccess )
        {
            _tcs.SetResult();
        }
        else
        {
            _tcs.TrySetCanceled();
        }
        _holder.OnCompleted();
        return true;
    }

    internal static void ThrowOnCancelCalledRequired( Action<InvalidOperationException> tcsSet )
    {
        var ex = new InvalidOperationException( "One of the OnCanceled methods must be called." );
        tcsSet( ex );
        throw ex;
    }

    internal static void ThrowOnErrorCalledRequired( Action<InvalidOperationException> tcsSet )
    {
        var ex = new InvalidOperationException( "One of the OnError methods must be called." );
        tcsSet( ex );
        throw ex;
    }

    /// <summary>
    /// Overridden to return the current completion status.
    /// </summary>
    /// <returns>The current status.</returns>
    public override string ToString() => GetStatus( _tcs.Task.Status, _state );

    static internal string GetStatus( TaskStatus t, int s )
    {
        return t switch
        {
            TaskStatus.RanToCompletion => (s & 2) != 0
                                            ? "Completed (HasFailed)"
                                            : (s & 4) != 0
                                                ? "Completed (HasBeenCanceled)"
                                                : "Success",
            TaskStatus.Canceled => (s & 2) != 0
                                    ? "Canceled (HasFailed)"
                                    : "Canceled",
            TaskStatus.Faulted => "Failed",
            _ => "Waiting"
        };
    }
}
