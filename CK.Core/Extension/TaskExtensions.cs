using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Core;

/// <summary>
/// Provides the <see cref="WaitForTaskCompletionAsync"/> extension method on <see cref="Task"/>.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Asynchronously waits for an existing task to be completed within a maximum amount of time (and/or as long
    /// as the <paramref name="cancellation"/> is not signaled), whatever the task is and without throwing exceptions.
    /// <para>
    /// Caution! Unlike the standard <see cref="Task{T}.WaitAsync(TimeSpan, CancellationToken)"/>from .Net, this should be used to "query" an existing Task,
    /// that belongs to another "activity" (here the result of the task is not handled).
    /// </para>
    /// </summary>
    /// <param name="task">This task.</param>
    /// <param name="millisecondsTimeout">The timeout in milliseconds to wait before returning false. Use <see cref="Timeout.Infinite"/> to skip timeout.</param>
    /// <param name="cancellation">Optional cancellation token.</param>
    /// <returns>True if <see cref="Task.IsCompleted"/> is true, false if the timeout occurred before or <paramref name="cancellation"/> is signaled.</returns>
    public static async Task<bool> WaitForTaskCompletionAsync( this Task task, int millisecondsTimeout, CancellationToken cancellation = default )
    {
        // Fast path.
        if( task.IsCompleted ) return true;
        Debug.Assert( Timeout.Infinite == -1 );
        Throw.CheckOutOfRangeArgument( millisecondsTimeout >= -1 );

        if( millisecondsTimeout == Timeout.Infinite && !cancellation.CanBeCanceled )
        {
            await task.ConfigureAwait( false );
            return true;
        }
        if( millisecondsTimeout == 0 ) return false;

        var tcsCancel = new TaskCompletionSource();

        static void DoCancel( object? c ) => ((TaskCompletionSource)c!).TrySetCanceled( default );

        CancellationTokenRegistration rCancel1 = cancellation.CanBeCanceled
                                                 ? cancellation.UnsafeRegister( DoCancel, tcsCancel )
                                                 : default;
        CancellationTokenRegistration rCancel2 = default;
        var ctsDelay = millisecondsTimeout > 0 ? new CancellationTokenSource( millisecondsTimeout ) : null;
        if( ctsDelay != null )
        {
            rCancel2 = ctsDelay.Token.UnsafeRegister( DoCancel, tcsCancel );
        }
        try
        {
            var completedTask = await Task.WhenAny( task, tcsCancel.Task ).ConfigureAwait( false );
            if( completedTask == task )
            {
#pragma warning disable VSTHRD103 // Call async methods when in an async method
                ctsDelay?.Cancel();
#pragma warning restore VSTHRD103
                return true;
            }
        }
        finally
        {
            await rCancel1.DisposeAsync();
            await rCancel2.DisposeAsync();
        }
        return task.IsCompleted;
    }

}
