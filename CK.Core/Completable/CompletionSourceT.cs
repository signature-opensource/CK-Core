using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Core;

/// <summary>
/// This is a <see cref="TaskCompletionSource{TResult}"/>-like object that allows exceptions or cancellations
/// to be ignored (see <see cref="ICompletable{TResult}"/> OnError and OnCanceled protected methods).
/// </summary>
public class CompletionSource<TResult> : ICompletion<TResult>, ICompletionSource
{
    readonly TaskCompletionSource<TResult> _tcs;
    readonly ICompletable<TResult> _holder;
    volatile Exception? _exception;
    int _state;
    const int StateSucces = 1;
    const int StateCancel = 2;
    const int StateFailed = 3;

    /// <summary>
    /// Creates a <see cref="CompletionSource{TResult}"/>.
    /// </summary>
    /// <param name="holder">The completion's holder.</param>
    public CompletionSource( ICompletable<TResult> holder )
    {
        Throw.CheckNotNullArgument( holder );
        _holder = holder;
        _tcs = new TaskCompletionSource<TResult>( TaskCreationOptions.RunContinuationsAsynchronously );
        // Continuation that handles the error (if any): this prevent the UnobservedTaskException to
        // be raised during GC (Task's finalization).
        _ = _tcs.Task.ContinueWith( r => r.Exception!.Handle( e => true ),
                                    default,
                                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                                    TaskScheduler.Default );
    }

    /// <summary>
    /// Gets the this completion' holder.
    /// See note in <see cref="CompletionSource"/>.
    /// </summary>
    protected ICompletable<TResult> Holder => _holder;

    /// <summary>
    /// This is required to expose a covariant TResult.
    /// </summary>
    sealed class Awaitable : IAwaitable<TResult>
    {
        readonly TaskAwaiter<TResult> _awaiter;

        public Awaitable( TaskAwaiter<TResult> awaiter )
        {
            _awaiter = awaiter;
        }

        public bool IsCompleted => _awaiter.IsCompleted;

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
        public TResult GetResult() => _awaiter.GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits

        public void OnCompleted( Action continuation ) => _awaiter.OnCompleted( continuation );

        public void UnsafeOnCompleted( Action continuation ) => _awaiter.UnsafeOnCompleted( continuation );
    }

    /// <summary>
    /// Gets the task that will be resolved when the command completes.
    /// </summary>
    public IAwaitable<TResult> GetAwaiter() => new Awaitable( _tcs.Task.GetAwaiter() );

    TaskAwaiter ICompletion.GetAwaiter() => ((Task)_tcs.Task).GetAwaiter();

    /// <inheritdoc />
    public Task<TResult> Task => _tcs.Task;

    /// <inheritdoc />
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
    public TResult Result => _tcs.Task.Result;
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits

    Task ICompletion.Task => _tcs.Task;

    /// <inheritdoc />
    public Exception? OriginalException => _tcs.Task.IsCompleted ? _exception : null;

    /// <inheritdoc />
    public bool IsCompleted => _tcs.Task.IsCompleted;

    /// <inheritdoc />
    public bool HasSucceed => _tcs.Task.IsCompleted && Volatile.Read( ref _state ) == StateSucces;

    /// <inheritdoc />
    public bool HasFailed => _tcs.Task.IsCompleted && Volatile.Read( ref _state ) == StateFailed;

    /// <inheritdoc />
    public bool HasBeenCanceled => _tcs.Task.IsCompleted && Volatile.Read( ref _state ) == StateCancel;

    /// <summary>
    /// Attempts to transition the <see cref="Task"/> into the <see cref="TaskStatus.Canceled"/> state.
    /// </summary>
    /// <param name="result">The command result.</param>
    /// <returns>
    /// True if the operation was successful; false if the operation was unsuccessful.
    /// </returns>
    public bool TrySetResult( TResult result )
    {
        if( Interlocked.CompareExchange( ref _state, StateSucces, 0 ) != 0 ) return false;
        _tcs.SetResult( result );
        _holder.OnCompleted();
        return true;
    }

    /// <summary>
    /// Enables <see cref="ICompletable{TResult}"/> OnError to
    /// transform exceptions into successful or canceled completion.
    /// </summary>
    public ref struct OnError
    {
        internal TResult Result;
        internal Exception? ResultError;
        internal bool Called;
        internal bool ResultCancel;

        internal OnError( CompletionSource<TResult> c )
        {
            Result = default!;
            ResultError = default;
            Called = false;
            ResultCancel = false;
        }

        /// <summary>
        /// Sets the original exception (or another one).
        /// </summary>
        /// <param name="ex">The exception to set.</param>
        public void SetException( Exception ex )
        {
            Throw.CheckNotNullArgument( ex );
            if( Called ) CompletionSource.OnError.ThrowMustBeCalledOnlyOnce();
            Called = true;
            ResultError = ex;
        }

        /// <summary>
        /// Sets a successful completion instead of an error.
        /// <para>
        /// Note that the <see cref="CompletionSource{TResult}.HasFailed"/> will be true: the fact that the command
        /// did not properly complete is available.
        /// </para>
        /// </summary>
        public void SetResult( TResult result )
        {
            if( Called ) CompletionSource.OnError.ThrowMustBeCalledOnlyOnce();
            Called = true;
            Result = result;
        }

        /// <summary>
        /// Sets a cancellation completion instead of an error.
        /// <para>
        /// Note that the <see cref="CompletionSource{TResult}.HasFailed"/> will be true: the fact that the command
        /// did not properly complete is available.
        /// </para>
        /// </summary>
        public void SetCanceled()
        {
            if( Called ) CompletionSource.OnError.ThrowMustBeCalledOnlyOnce();
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
        if( !o.Called ) CompletionSource.ThrowOnErrorCalledRequired( _tcs.SetException );
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
            _tcs.SetResult( o.Result );
        }
        _holder.OnCompleted();
        return true;
    }

    /// <summary>
    /// Enables <see cref="ICompletable{TResult}"/> OnCanceled method to
    /// transform exceptions into successful or canceled completion.
    /// </summary>
    public ref struct OnCanceled
    {
        internal TResult Result;
        internal bool Called;
        internal bool ResultCanceled;

        internal OnCanceled( CompletionSource<TResult> c )
        {
            Result = default!;
            Called = false;
            ResultCanceled = false;
        }


        /// <summary>
        /// Sets a successful instead of a cancellation completion.
        /// <para>
        /// Note that the <see cref="HasBeenCanceled"/> will be true: the fact that the command
        /// has been canceled is available.
        /// </para>
        /// </summary>
        public void SetResult( TResult result )
        {
            if( Called ) CompletionSource.OnCanceled.ThrowMustBeCalledOnlyOnce();
            Called = true;
            Result = result;
        }

        /// <summary>
        /// Sets the cancellation completion.
        /// <para>
        /// Note that the <see cref="HasBeenCanceled"/> will be true: the fact that the command
        /// has been canceled is available.
        /// </para>
        /// </summary>
        public void SetCanceled()
        {
            if( Called ) CompletionSource.OnCanceled.ThrowMustBeCalledOnlyOnce();
            Called = true;
            ResultCanceled = true;
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
        if( !o.Called ) CompletionSource.ThrowOnCancelCalledRequired( _tcs.SetException );
        if( o.ResultCanceled )
        {
            _tcs.SetCanceled();
        }
        else
        {
            _tcs.SetResult( o.Result );
        }
        _holder.OnCompleted();
        return true;
    }

    /// <summary>
    /// Overridden to return the current completion status.
    /// </summary>
    /// <returns>The current status.</returns>
    public override string ToString() => CompletionSource.GetStatus( _tcs.Task.Status, _state );

}
