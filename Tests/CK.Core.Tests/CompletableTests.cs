using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Shouldly;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CK.Core.Tests;


public class CompletableTests
{
    enum CommandAction
    {
        Success,
        Error,
        Canceled
    }

    static readonly Exception RunException = new Exception( "Run exception." );
    static readonly Exception OverriddenException = new Exception( "Overridden exception." );


    class Command : ICompletable
    {
        public Command()
        {
            CompletionSource = new CompletionSource( this );
        }

        public Command( Random r )
            : this()
        {
            RunAction = (CommandAction)r.Next( 3 );
            SuccessOnCancel = r.Next( 2 ) == 0;
            OnErrorHook = (CommandAction)r.Next( 3 );
            if( r.Next( 2 ) == 0 ) OverriddenExceptionOnError = OverriddenException;
            var execTime = r.Next( 300 ) - 150;
            ExecutionTime = execTime < 0 ? 0 : execTime;
        }

        public bool OnCompletedCalled { get; private set; }

        public readonly CompletionSource CompletionSource;

        public CommandAction RunAction { get; set; }

        public bool SuccessOnCancel { get; set; }

        public CommandAction OnErrorHook { get; set; }

        public Exception? OverriddenExceptionOnError { get; set; }

        public int ExecutionTime { get; set; }

        public ICompletion Completion => CompletionSource;

        public void OnCanceled( ref CompletionSource.OnCanceled result )
        {
            if( SuccessOnCancel ) result.SetResult();
            else result.SetCanceled();
        }

        public void OnError( Exception ex, ref CompletionSource.OnError result )
        {
            switch( OnErrorHook )
            {
                case CommandAction.Error: result.SetException( OverriddenExceptionOnError ?? ex ); break;
                case CommandAction.Success: result.SetResult(); break;
                case CommandAction.Canceled: result.SetCanceled(); break;
                default: throw new NotSupportedException();
            }
        }

        void ICompletable.OnCompleted()
        {
            OnCompletedCalled.ShouldBeFalse();
            OnCompletedCalled = true;
        }
    }

    static async Task CommandExecuteAsync( Command c )
    {
        if( c.ExecutionTime > 0 ) await Task.Delay( c.ExecutionTime ).ConfigureAwait( false );
        switch( c.RunAction )
        {
            case CommandAction.Success: c.CompletionSource.TrySetResult(); break;
            case CommandAction.Canceled: c.CompletionSource.TrySetCanceled(); break;
            case CommandAction.Error: c.CompletionSource.TrySetException( RunException ); break;
        }
        // Just to be sure :)
        c.CompletionSource.TrySetException( RunException ).ShouldBeFalse();
        c.CompletionSource.TrySetCanceled().ShouldBeFalse();
        c.CompletionSource.TrySetResult().ShouldBeFalse();
    }

    [TestCase( 100000, 12 )]
    public async Task completable_can_hook_the_task_result_Async( int nb, int seed )
    {
        var random = new Random( seed );

        var commands = Enumerable.Range( 0, nb ).Select( _ => new Command( random ) ).ToArray();
        foreach( var c in commands )
        {
            _ = CommandExecuteAsync( c );
        }
        foreach( var c in commands )
        {
            try
            {
                await c.Completion;
                c.Completion.IsCompleted.ShouldBeTrue();
                c.OnCompletedCalled.ShouldBeTrue();
            }
            catch( OperationCanceledException )
            {
                (c.RunAction == CommandAction.Canceled || (c.RunAction == CommandAction.Error && c.OnErrorHook == CommandAction.Canceled))
                    .ShouldBeTrue();
            }
            catch( Exception ex ) when( ex == RunException )
            {
                c.RunAction.ShouldBe( CommandAction.Error );
                c.OverriddenExceptionOnError.ShouldBeNull();
            }
            catch( Exception ex ) when( ex == OverriddenException )
            {
                c.RunAction.ShouldBe( CommandAction.Error );
                c.OverriddenExceptionOnError.ShouldNotBeNull();
            }
            c.Completion.IsCompleted.ShouldBeTrue();
            c.OnCompletedCalled.ShouldBeTrue();

            switch( c.RunAction )
            {
                case CommandAction.Error:
                    Debug.Assert( c.Completion.OriginalException != null );
                    c.Completion.OriginalException.Message.ShouldBeSameAs( RunException.Message );
                    c.Completion.HasFailed.ShouldBeTrue();
                    switch( c.OnErrorHook )
                    {
                        case CommandAction.Canceled:
                            c.Completion.Task.Status.ShouldBe( TaskStatus.Canceled );
                            break;
                        case CommandAction.Success:
                            c.Completion.Task.Status.ShouldBe( TaskStatus.RanToCompletion );
                            break;
                        case CommandAction.Error:
                            Debug.Assert( c.Completion.Task.Exception != null );
                            c.Completion.Task.Status.ShouldBe( TaskStatus.Faulted );
                            c.Completion.Task.Exception.Message.ShouldContain( (c.OverriddenExceptionOnError ?? RunException).Message );
                            break;
                    }
                    break;
                case CommandAction.Success:
                    c.Completion.HasSucceed.ShouldBeTrue();
                    break;
                case CommandAction.Canceled:
                    c.Completion.HasBeenCanceled.ShouldBeTrue();
                    c.Completion.Task.Status.ShouldBe( c.SuccessOnCancel ? TaskStatus.RanToCompletion : TaskStatus.Canceled );
                    break;
            }
        }
    }

    class CommandWithResult : ICompletable<int>
    {
        public CommandWithResult()
        {
            CompletionSource = new CompletionSource<int>( this );
        }

        public CommandWithResult( Random r )
            : this()
        {
            RunAction = (CommandAction)r.Next( 3 );
            SuccessOnCancel = r.Next( 2 ) == 0;
            OnErrorHook = (CommandAction)r.Next( 3 );
            if( r.Next( 2 ) == 0 ) OverriddenExceptionOnError = OverriddenException;
            var execTime = r.Next( 300 ) - 150;
            ExecutionTime = execTime < 0 ? 0 : execTime;
            UseTrySet = r.Next( 2 ) == 0;
        }

        public readonly CompletionSource<int> CompletionSource;

        public CommandAction RunAction { get; set; }

        public bool SuccessOnCancel { get; set; }

        public CommandAction OnErrorHook { get; set; }

        public Exception? OverriddenExceptionOnError { get; set; }

        public int ExecutionTime { get; set; }

        public bool UseTrySet { get; set; }

        public ICompletion<int> Completion => CompletionSource;

        public void OnCanceled( ref CompletionSource<int>.OnCanceled result )
        {
            if( SuccessOnCancel ) result.SetResult( 2 );
            else result.SetCanceled();
        }

        public volatile bool OnCompletedCalled;

        public void OnError( Exception ex, ref CompletionSource<int>.OnError result )
        {
            switch( OnErrorHook )
            {
                case CommandAction.Error: result.SetException( OverriddenExceptionOnError ?? ex ); break;
                case CommandAction.Success: result.SetResult( 1 ); break;
                case CommandAction.Canceled: result.SetCanceled(); break;
                default: throw new NotSupportedException();
            }
        }

        public void OnCompleted()
        {
            OnCompletedCalled.ShouldBeFalse();
            OnCompletedCalled = true;
        }
    }

    static async Task CommandExecuteAsync( CommandWithResult c )
    {
        if( c.ExecutionTime > 0 ) await Task.Delay( c.ExecutionTime ).ConfigureAwait( false );
        switch( c.RunAction )
        {
            case CommandAction.Success: c.CompletionSource.TrySetResult( 3712 ); break;
            case CommandAction.Canceled: c.CompletionSource.TrySetCanceled(); break;
            case CommandAction.Error: c.CompletionSource.TrySetException( RunException ); break;
        }
        // Just to be sure :)
        c.CompletionSource.TrySetException( RunException ).ShouldBeFalse();
        c.CompletionSource.TrySetCanceled().ShouldBeFalse();
        c.CompletionSource.TrySetResult( 1 ).ShouldBeFalse();
    }

    [TestCase( 100000, 877 )]
    public async Task completable_with_result_can_hook_the_task_result_Async( int nb, int seed )
    {
        var random = new Random( seed );

        var commands = Enumerable.Range( 0, nb ).Select( _ => new CommandWithResult( random ) ).ToArray();
        foreach( var c in commands )
        {
            _ = CommandExecuteAsync( c );
        }
        foreach( var c in commands )
        {
            try
            {
                await c.Completion;
                c.Completion.IsCompleted.ShouldBeTrue();
                c.OnCompletedCalled.ShouldBeTrue();
            }
            catch( OperationCanceledException )
            {
                (c.RunAction == CommandAction.Canceled || (c.RunAction == CommandAction.Error && c.OnErrorHook == CommandAction.Canceled))
                    .ShouldBeTrue();
                c.Completion.Task.Status.ShouldBe( TaskStatus.Canceled );
            }
            catch( Exception ex ) when( ex == RunException )
            {
                c.RunAction.ShouldBe( CommandAction.Error );
                c.OverriddenExceptionOnError.ShouldBeNull();
                c.Completion.Task.Status.ShouldBe( TaskStatus.Faulted );
            }
            catch( Exception ex ) when( ex == OverriddenException )
            {
                c.RunAction.ShouldBe( CommandAction.Error );
                c.OverriddenExceptionOnError.ShouldNotBeNull();
                c.Completion.Task.Status.ShouldBe( TaskStatus.Faulted );
            }
            c.Completion.IsCompleted.ShouldBeTrue();
            c.OnCompletedCalled.ShouldBeTrue();

            switch( c.RunAction )
            {
                case CommandAction.Error:
                    Debug.Assert( c.Completion.OriginalException != null );
                    c.Completion.OriginalException.Message.ShouldBeSameAs( RunException.Message );
                    c.Completion.HasFailed.ShouldBeTrue();
                    switch( c.OnErrorHook )
                    {
                        case CommandAction.Canceled:
                            c.Completion.Task.Status.ShouldBe( TaskStatus.Canceled );
                            break;
                        case CommandAction.Success:
                            c.Completion.Task.Status.ShouldBe( TaskStatus.RanToCompletion );
                            c.Completion.Result.ShouldBe( 1 );
                            break;
                        case CommandAction.Error:
                            Debug.Assert( c.Completion.Task.Exception != null );
                            c.Completion.Task.Status.ShouldBe( TaskStatus.Faulted );
                            c.Completion.Task.Exception.Message.ShouldContain( (c.OverriddenExceptionOnError ?? RunException).Message );
                            break;
                    }
                    break;
                case CommandAction.Success:
                    c.Completion.HasSucceed.ShouldBeTrue();
                    c.Completion.Result.ShouldBe( 3712 );
                    break;
                case CommandAction.Canceled:
                    c.Completion.HasBeenCanceled.ShouldBeTrue();
                    c.Completion.Task.Status.ShouldBe( c.SuccessOnCancel ? TaskStatus.RanToCompletion : TaskStatus.Canceled );
                    if( c.SuccessOnCancel ) c.Completion.Result.ShouldBe( 2 );
                    break;
            }
        }
    }


    class SimpleCommand : ICompletable<int>
    {
        public SimpleCommand()
        {
            CompletionSource = new CompletionSource<int>( this );
        }

        public bool OnCompletedCalled { get; private set; }

        public readonly CompletionSource<int> CompletionSource;

        public ICompletion<int> Completion => CompletionSource;

        void ICompletable<int>.OnCanceled( ref CompletionSource<int>.OnCanceled result ) => result.SetCanceled();

        public void OnError( Exception ex, ref CompletionSource<int>.OnError result )
        {
            // This is how OperationCanceledException can be transformed to "normal" cancellation.
            // if( ex is OperationCanceledException ) result.SetCanceled(); else
            result.SetException( ex );
        }

        void ICompletable<int>.OnCompleted()
        {
            OnCompletedCalled.ShouldBeFalse();
            OnCompletedCalled = true;
        }
    }

    class SimpleCommandNoResult : ICompletable
    {
        public SimpleCommandNoResult()
        {
            CompletionSource = new CompletionSource( this );
        }

        public bool OnCompletedCalled { get; private set; }

        public readonly CompletionSource CompletionSource;

        public ICompletion Completion => CompletionSource;

        void ICompletable.OnCanceled( ref CompletionSource.OnCanceled result ) => result.SetCanceled();

        public void OnError( Exception ex, ref CompletionSource.OnError result )
        {
            // This is how OperationCanceledException can be transformed to "normal" cancellation.
            // if( ex is OperationCanceledException ) result.SetCanceled(); else
            result.SetException( ex );
        }

        void ICompletable.OnCompleted()
        {
            OnCompletedCalled.ShouldBeFalse();
            OnCompletedCalled = true;
        }

    }

    [TestCase( "NoResult", "OperationCanceledException" )]
    [TestCase( "NoResult", "Exception" )]
    [TestCase( "NoResult", "Cancel" )]
    [TestCase( "WithResult", "OperationCanceledException" )]
    [TestCase( "WithResult", "Exception" )]
    [TestCase( "WithResult", "Cancel" )]
    public void unobserved_completion_dont_raise_UnobservedTaskException( string commandType, string error )
    {
        bool raised = false;
        TaskScheduler.UnobservedTaskException += ( sender, e ) => raised = true;

        CreateCompleteAndForgetCommand( commandType, error );

        GC.Collect();
        GC.WaitForPendingFinalizers();

        raised.ShouldBeFalse();
    }

    [MethodImpl( MethodImplOptions.NoInlining )]
    static void CreateCompleteAndForgetCommand( string commandType, string error )
    {
        if( commandType == "WithResult" )
        {
            var cmd = new SimpleCommand();
            if( error == "Cancel" ) cmd.CompletionSource.TrySetCanceled();
            else if( error == "OperationCanceledException" ) cmd.CompletionSource.TrySetException( new OperationCanceledException() );
            else cmd.CompletionSource.TrySetException( new Exception( "Pouf" ) );
            cmd.OnCompletedCalled.ShouldBeTrue();
        }
        else
        {
            var cmd = new SimpleCommandNoResult();
            if( error == "Cancel" ) cmd.CompletionSource.TrySetCanceled();
            else if( error == "OperationCanceledException" ) cmd.CompletionSource.TrySetException( new OperationCanceledException() );
            else cmd.CompletionSource.TrySetException( new Exception( "Pouf" ) );
            cmd.OnCompletedCalled.ShouldBeTrue();
        }
    }

}
