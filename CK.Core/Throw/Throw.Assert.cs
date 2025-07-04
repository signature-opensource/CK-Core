using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CK.Core;

public partial class Throw
{
    /// <summary>
    /// Assertion that captures the expression, file path and line number and throws a <see cref="CKException"/>.
    /// <para>
    /// This is mainly to be used in unit test.
    /// </para>
    /// <para>
    /// This SHOULD NOT be used in regular code: <see cref="Throw.DebugAssert(bool, string?, string?, int)"/> should
    /// always be used instead.
    /// </para>
    /// </summary>
    /// <param name="valid">Must be true.</param>
    /// <param name="exp">Automatically captures <paramref name="valid"/> expression source text.</param>
    /// <param name="filePath">Automatically captures the source code file path.</param>
    /// <param name="lineNumber">Automatically captures the source code line number.</param>
    /// <exception cref="CKException">Whenever <paramref name="valid"/> is false.</exception>
    [StackTraceHidden]
    public static void Assert( [DoesNotReturnIf( false )] bool valid,
                               [CallerArgumentExpression( "valid" )] string? exp = null,
                               [CallerFilePath] string? filePath = null,
                               [CallerLineNumber] int lineNumber = 0 )
    {
        if( !valid )
        {
            Throw.CKException( $"Debug Assertion failed '{exp}', {filePath}@{lineNumber}." );
        }
    }

    /// <summary>
    /// Assertion that captures the expression, file path and line number and throws a <see cref="CKException"/>.
    /// <para>
    /// This is to be used in unit test: TUnit Assert.That is asynchronous (it must be <c>await</c>ed). But as TUnit
    /// also handles synchronous tests, this can be used in simple, synchronous, tests.
    /// </para>
    /// <para>
    /// This SHOULD NOT be used in regular code: <see cref="Throw.DebugAssert(string, bool, string?, string?, int)"/> should
    /// always be used instead.
    /// </para>
    /// </summary>
    /// <param name="message">Additional message that will appear in the exception.</param>
    /// <param name="valid">Must be true.</param>
    /// <param name="exp">Automatically captures <paramref name="valid"/> expression source text.</param>
    /// <param name="filePath">Automatically captures the source code file path.</param>
    /// <param name="lineNumber">Automatically captures the source code line number.</param>
    /// <exception cref="CKException">Whenever <paramref name="valid"/> is false.</exception>
    [StackTraceHidden]
    public static void Assert( string message,
                               [DoesNotReturnIf( false )] bool valid,
                               [CallerArgumentExpression( "valid" )] string? exp = null,
                               [CallerFilePath] string? filePath = null,
                               [CallerLineNumber] int lineNumber = 0 )
    {
        if( !valid )
        {
            Throw.CKException( $"Debug Assertion failed: {message} - '{exp}', {filePath}@{lineNumber}." );
        }
    }
}
