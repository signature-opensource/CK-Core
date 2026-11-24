// These helpers are copied from CK.Testing.
// CK.Testingreferences Shouldly and exposes these helpers.

using CK.Core;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

/// <summary>
/// This class is exceptionnaly defined in the global namespace. As such, Roslyn selects its methonds
/// other any definition in explicit namespaces.
/// <para>
/// This is bad and must remain exceptional. But, here, it enables to use Shouldly and "override" some
/// of its definitions.
/// </para>
/// </summary>
#pragma warning disable CA1050 // Declare types in namespaces
[ShouldlyMethods]
public static class CKShouldlyGlobalOverrideExtensions
{
    /// <summary>
    /// Fix https://github.com/shouldly/shouldly/issues/934.
    /// <para>
    /// Note that this change the semantics and this is intended: Shouldy's
    /// non generic ShouldThrow doesn't honor the Lyskov Substitution Principle (as opposed to the generic one),
    /// the <paramref name="exceptionType"/> must be the exact type of the exception (like our
    /// <see cref="CKShouldlyExtensions.ShouldThrowExactly(Delegate, Type, string?)"/> does).
    /// </para>
    /// </summary>
    /// <remarks>
    /// This Delegate based method is chosen because it is in the global::CKShouldlyGlobalOverrideExtensions.
    /// </remarks>
    /// <param name="actual">The action code that should throw.</param>
    /// <param name="exceptionType">The expected exception type.</param>
    /// <param name="customMessage">Optional message.</param>
    /// <returns>The exception instance.</returns>
    public static Exception ShouldThrow( this Delegate actual, Type exceptionType, string? customMessage = null )
    {
        return CKShouldlyExtensions.ThrowInternal( actual, customMessage, exceptionType, exactType: false );
    }
}
#pragma warning restore CA1050 // Declare types in namespaces

namespace Shouldly
{

    /// <summary>
    /// Extends Shouldly with useful helpers.
    /// <para>
    /// This is tested in CK-Core/Tests/CK.Core.Tests.
    /// </para>
    /// </summary>
    [ShouldlyMethods]
    public static class CKShouldlyExtensions
    {
        /// <summary>
        /// Fix https://github.com/shouldly/shouldly/issues/934.
        /// </summary>
        /// <remarks>
        /// This Delegate based method is chosen over the original Should's extension method
        /// that takes a <c>Func&lt;object?&gt</c> parameter (that is the cause of the issue).  
        /// </remarks>
        /// <typeparam name="TException">The expected exception type.</typeparam>
        /// <param name="actual">The action code that should throw.</param>
        /// <param name="customMessage">Optional message.</param>
        /// <returns>The exception instance.</returns>
        public static TException ShouldThrow<TException>( this Delegate actual, string? customMessage = null )
            where TException : Exception
        {
            return (TException)ThrowInternal( actual, customMessage, typeof( TException ), exactType: false );
        }

        /// <summary>
        /// Fix https://github.com/shouldly/shouldly/issues/934.
        /// <para>
        /// Note that this change the semantics and this is intended: Shouldy's
        /// non generic ShouldThrow doesn't honor the Lyskov Substitution Principle (as opposed to the generic one),
        /// the <paramref name="exceptionType"/> must be the exact type of the exception (like our
        /// <see cref="ShouldThrowExactly(Delegate, Type, string?)"/> does).
        /// </para>
        /// </summary>
        /// <remarks>
        /// This Delegate based method is chosen over the original Should's extension method
        /// that takes a <c>Func&lt;object?&gt</c> parameter (that is the cause of the issue).  
        /// </remarks>
        /// <param name="actual">The action code that should throw.</param>
        /// <param name="exceptionType">The expected exception type.</param>
        /// <param name="customMessage">Optional message.</param>
        /// <returns>The exception instance.</returns>
        public static Exception ShouldThrow( this Delegate actual, Type exceptionType, string? customMessage = null )
        {
            return ThrowInternal( actual, customMessage, exceptionType, exactType: false );
        }

        /// <summary>
        /// Fix https://github.com/shouldly/shouldly/issues/934.
        /// </summary>
        /// <remarks>
        /// This Delegate based method is chosen over the original Should's extension method
        /// that takes a <c>Func&lt;object?&gt</c> parameter (that is the cause of the issue).  
        /// </remarks>
        /// <param name="actual">The action code that should throw.</param>
        /// <param name="exceptionType">The exact expected exception type.</param>
        /// <param name="customMessage">Optional message.</param>
        /// <returns>The exception instance.</returns>
        public static TException ShouldThrowExactly<TException>( this Delegate actual, string? customMessage = null )
            where TException : Exception
        {
            return (TException)ThrowInternal( actual, customMessage, typeof( TException ), exactType: true );
        }

        /// <summary>
        /// Fix https://github.com/shouldly/shouldly/issues/934.
        /// </summary>
        /// <remarks>
        /// This Delegate based method is chosen over the original Should's extension method
        /// that takes a <c>Func&lt;object?&gt</c> parameter (that is the cause of the issue).  
        /// </remarks>
        /// <param name="actual">The action code that should throw.</param>
        /// <param name="exceptionType">The exact expected exception type.</param>
        /// <param name="customMessage">Optional message.</param>
        /// <returns>The exception instance.</returns>
        public static Exception ShouldThrowExactly( this Delegate actual, Type exceptionType, string? customMessage = null )
        {
            return ThrowInternal( actual, customMessage, exceptionType, exactType: true );
        }

        internal static Exception ThrowInternal( Delegate actual,
                                                 string? customMessage,
                                                 Type expectedExceptionType,
                                                 bool exactType,
                                                 [CallerMemberName] string? shouldlyMethod = null )
        {
            // Handle composite delegates: consider the invocation list if any.
            var multi = actual.GetInvocationList();
            if( multi.Length == 0 )
            {
                foreach( var d in multi )
                {
                    CheckNoParameters( d );
                }
            }
            else
            {
                CheckNoParameters( actual );
            }

            static void CheckNoParameters( Delegate d )
            {
                var parameters = d.Method.GetParameters();
                if( parameters.Length > 0 )
                {
                    var parametersDesc = parameters.Select( p => $"{p.ParameterType.Name} {p.Name}" );
                    throw new ArgumentException( $"""
                        ShouldThrow can only be called on a delegate without parameters.
                        Found method {d.Method.DeclaringType?.Name}.{d.Method.Name}( {string.Join( ", ", parametersDesc )} )
                        """ );
                }
            }
            try
            {
                if( multi.Length == 0 )
                {
                    Execute( actual );
                }
                else
                {
                    foreach( var d in multi )
                    {
                        Execute( d );
                    }
                }

                static void Execute( Delegate d ) => d.Method.Invoke( d.Target, BindingFlags.DoNotWrapExceptions, null, null, null );
            }
            catch( Exception ex )
            {
                if( ex.GetType() == expectedExceptionType
                    || (!exactType && expectedExceptionType.IsAssignableFrom( ex.GetType() )) )
                {
                    return ex;
                }
                throw new ShouldAssertException( new ShouldlyThrowMessage( expectedExceptionType, ex.GetType(), customMessage, shouldlyMethod! ).ToString(), ex );
            }
            throw new ShouldAssertException( new ShouldlyThrowMessage( expectedExceptionType, customMessage: customMessage, shouldlyMethod! ).ToString() );
        }

        /// <summary>
        /// Predicate overload for ShouldBe.
        /// This is CK specific.
        /// </summary>
        /// <typeparam name="T">This type.</typeparam>
        /// <param name="actual">This instance.</param>
        /// <param name="elementPredicate">The predicate that muts be satisfied.</param>
        /// <param name="customMessage">Optional message.</param>
        /// <returns>This instance.</returns>
        [MethodImpl( MethodImplOptions.NoInlining )]
        public static T ShouldBe<T>( this T actual, Expression<Func<T, bool>> elementPredicate, string? customMessage = null )
        {
            Throw.CheckNotNullArgument( elementPredicate );
            var condition = elementPredicate.Compile();
            if( !condition( actual ) )
                throw new ShouldAssertException( new ExpectedActualShouldlyMessage( elementPredicate.Body, actual, customMessage ).ToString() );
            return actual;
        }

        /// <summary>
        /// Apply an action to each item that should be one or more Shouldly expectation.
        /// This is CK specific.
        /// </summary>
        /// <typeparam name="T">Th type of the enumerable.</typeparam>
        /// <param name="actual">This enumerable.</param>
        /// <param name="action">The action to apply.</param>
        /// <returns>This enumerable.</returns>
        [MethodImpl( MethodImplOptions.NoInlining )]
        public static IEnumerable<T> ShouldAll<T>( this IEnumerable<T> actual, Action<T> action )
        {
            Throw.CheckNotNullArgument( action );
            int idx = 0;
            try
            {
                foreach( var e in actual )
                {
                    action( e );
                    ++idx;
                }
            }
            catch( ShouldAssertException aEx )
            {
                var prefix = Environment.NewLine + "  | ";
                // Shouldly's message has "\r\n" on every platform.
                var offsetMessage = aEx.Message.ReplaceLineEndings( prefix );
                throw new ShouldAssertException( $"ShouldAll failed for item n°{idx}.{prefix}{offsetMessage}" );
            }
            return actual;
        }

        /// <summary>
        /// Explicit override to allow implict cast from string.
        /// </summary>
        /// <param name="actual">This normalized path.</param>
        /// <param name="expected">The expected path.</param>
        /// <param name="customMessage">Optional message.</param>
        public static NormalizedPath ShouldBe( this NormalizedPath actual, NormalizedPath expected, string? customMessage = null )
        {
            actual.AssertAwesomely( actual => actual == expected, actual, expected, customMessage );
            return actual;
        }

        public static short ShouldBe( this short actual, short expected, string? customMessage = null )
        {
            actual.AssertAwesomely( actual => actual == expected, actual, expected, customMessage );
            return actual;
        }

        public static ushort ShouldBe( this ushort actual, ushort expected, string? customMessage = null )
        {
            actual.AssertAwesomely( actual => actual == expected, actual, expected, customMessage );
            return actual;
        }

        public static sbyte ShouldBe( this sbyte actual, sbyte expected, string? customMessage = null )
        {
            actual.AssertAwesomely( actual => actual == expected, actual, expected, customMessage );
            return actual;
        }

        public static byte ShouldBe( this byte actual, byte expected, string? customMessage = null )
        {
            actual.AssertAwesomely( actual => actual == expected, actual, expected, customMessage );
            return actual;
        }


    }
}
