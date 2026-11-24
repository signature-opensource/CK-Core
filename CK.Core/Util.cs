using Microsoft.Extensions.Primitives;
using Microsoft.IO;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Core;

/// <summary>
/// Utility class.
/// Offers useful functions, constants, singletons and delegates.
/// </summary>
public static partial class Util
{
    /// <summary>
    /// Represents the smallest possible value for a <see cref="DateTime"/> in <see cref="DateTimeKind.Utc"/>.         
    /// </summary>
    static public readonly DateTime UtcMinValue = new DateTime( 0L, DateTimeKind.Utc );

    /// <summary>
    /// Represents the largest possible value for a <see cref="DateTime"/> in <see cref="DateTimeKind.Utc"/>.         
    /// </summary>
    static public readonly DateTime UtcMaxValue = new DateTime( 0x2bca2875f4373fffL, DateTimeKind.Utc );

    /// <summary>
    /// Centralized <see cref="IDisposable.Dispose"/> action call: it adapts an <see cref="IDisposable"/> interface to an <see cref="Invokable"/>.
    /// Can be safely called if <paramref name="obj"/> is null. 
    /// See <see cref="CreateDisposableAction"/> to wrap an action in a <see cref="IDisposable"/> interface.
    /// </summary>
    /// <param name="obj">The disposable object to dispose (can be null).</param>
    public static void ActionDispose( IDisposable obj ) => obj?.Dispose();

    sealed class DisposableAction : IDisposable
    {
        public Action? A;
        public void Dispose()
        {
            Action? a = A;
            if( a != null && Interlocked.CompareExchange( ref A, null, a ) == a ) a();
        }
    }

    /// <summary>
    /// Wraps an action in a <see cref="IDisposable"/> interface
    /// Can be safely called if <paramref name="a"/> is null (the dispose call will do nothing) and in multi threaded context:
    /// the call to action will be done once and only once by the first call to dispose.
    /// See <see cref="ActionDispose"/> to adapt an IDisposable interface to an <see cref="Invokable"/>.
    /// </summary>
    /// <param name="a">The action to call when <see cref="IDisposable.Dispose"/> is called.</param>
    public static IDisposable CreateDisposableAction( Action? a ) => new DisposableAction() { A = a };

    sealed class VoidDisposable : IDisposable { public void Dispose() { } }

    /// <summary>
    /// A void, immutable, <see cref="IDisposable"/> that does absolutely nothing.
    /// </summary>
    public static readonly IDisposable EmptyDisposable = new VoidDisposable();

    /// <summary>
    /// Consider a lambda expression as a <see cref="System.Action"/>.
    /// <para>
    /// This does nothing but converting the lambda without having to instantiate a new object:
    /// <code>
    /// new Action( () => ... )
    /// </code>. 
    /// </para>
    /// </summary>
    /// <param name="action">The lamdda.</param>
    public static Action Invokable( Action action ) => action;

    /// <summary>
    /// Consider a lambda expression that returns a result as a <see cref="System.Func{T}"/>.
    /// <para>
    /// This does nothing but converting the lambda without having to instantiate a new object
    /// and writing the <typeparamref name="T"/>:
    /// <code>
    /// new Func&lt;T&gt;( () => ... )
    /// </code>. 
    /// </para>
    /// </summary>
    /// <param name="func">The lamdda.</param>
    public static Func<T> Invokable<T>( Func<T> func ) => func;

    /// <summary>
    /// Consider a lambda expression that returns a Task as a Func&lt;Task&gt;.
    /// <para>
    /// This does nothing but converting the lambda without having to instantiate a new object:
    /// <code>
    /// new Func&lt;Task&gt;( () => ... )
    /// </code>
    /// </para>
    /// </summary>
    public static Func<Task> Awaitable( Func<Task> awaitable ) => awaitable;

    /// <summary>
    /// Consider a lambda expression that returns a Task with a result as a <c>Func&lt;Task&lt;T&gt;&gt;</c>.
    /// <para>
    /// This does nothing but converting the lambda without having to instantiate a new object
    /// and writing the <typeparamref name="T"/>:
    /// <code>
    /// new Func&lt;Task&lt;T&gt;&gt;( () => ... )
    /// </code>
    /// </para>
    /// </summary>
    public static Func<Task<T>> Awaitable<T>( Func<Task<T>> awaitable ) => awaitable;

    sealed class NopChangeToken : IChangeToken
    {
        public bool HasChanged => false;
        public bool ActiveChangeCallbacks => false;
        public IDisposable RegisterChangeCallback( Action<object?> callback, object? state ) => EmptyDisposable;
    }

    /// <summary>
    /// An empty change token that is never signaled and never raise any change callbacks.
    /// </summary>
    public static readonly IChangeToken NoChangeToken = new NopChangeToken();

    /// <summary>
    /// Sql Server Epoch (1st of January 1900): this is the 0 legacy date time, the default value, even if
    /// datetime2 is like the .Net DateTime (0001-01-01 through 9999-12-31, 100ns step).
    /// Its <see cref="DateTimeKind.Unspecified"/> since this is what the Sql client returns.
    /// </summary>
    public static readonly DateTime SqlServerEpoch = new DateTime( 599266080000000000, DateTimeKind.Unspecified );

    /// <summary>
    /// The <see cref="RecyclableStreamManager"/> is using 128 KiB blocks (small pool).
    /// </summary>
    public const int RecyclableStreamBlockSize = 128 * 1024;

    /// <summary>
    /// The <see cref="RecyclableStreamManager"/> large pool starts with 256 KiB buffers doubling up to 8 MiB (<see cref="RecyclableStreamMaximumBufferSize"/>):
    /// there will be 6 large buffers of 256 KiB, 512 KiB, 1 MiB, 2 MiB, 4 MiB, and 8 MiB.
    /// </summary>
    public const int RecyclableStreamLargeBufferMultiple = 256 * 1024;

    /// <summary>
    /// The <see cref="RecyclableStreamManager"/> will not keep buffers bigger than 8 MiB (large pool).
    /// </summary>
    public const int RecyclableStreamMaximumBufferSize = 8 * 1024 * 1024;

    /// <summary>
    /// The <see cref="RecyclableStreamManager"/> doubles the size of its buffers (large pool).
    /// </summary>
    public const bool RecyclableStreamUseExponentialLargeBuffer = true;

    /// <summary>
    /// Gets a default instance of <see cref="RecyclableMemoryStreamManager"/>. This manager is configured
    /// with at most 256 blocks of 128 KiB for the small pool and at most 32 MiB for its large pool.
    /// <para>
    /// This configuration should be fine as long as not too many big streams are required. However,
    /// there's no "one size fits all" here: the allocation and pool usage should be monitored when possible.
    /// </para>
    /// <para>
    /// The <see cref="RecyclableStreamMaximumSmallPoolFreeBytes"/> and <see cref="RecyclableStreamMaximumLargePoolFreeBytes"/>
    /// can be changed at any time to adjust the pool size. All other settings are immutable.
    /// </para>
    /// <para>
    /// Calling <see cref="RecyclableMemoryStream.ToArray()"/> is allowed (<see cref="RecyclableMemoryStreamManager.Options.ThrowExceptionOnToArray"/> is let to false
    /// and should not be set tot true): small serializations into small buffers must often result in final byte array.
    /// ToArray should NOT be called on large payload...
    /// </para>
    /// </summary>
    public static RecyclableMemoryStreamManager RecyclableStreamManager = new RecyclableMemoryStreamManager(
       new RecyclableMemoryStreamManager.Options(
           blockSize: RecyclableStreamBlockSize,
           largeBufferMultiple: RecyclableStreamLargeBufferMultiple,
           maximumBufferSize: RecyclableStreamMaximumBufferSize,
           maximumSmallPoolFreeBytes: 256 * RecyclableStreamBlockSize,
           maximumLargePoolFreeBytes: 32 * 1024 * 1024 )
       {
           UseExponentialLargeBuffer = RecyclableStreamUseExponentialLargeBuffer
       } );

    /// <summary>
    /// Gets or sets <see cref="RecyclableMemoryStreamManager.Options.MaximumSmallPoolFreeBytes"/> of the default <see cref="RecyclableStreamManager"/>.
    /// Defaults to 256 * <see cref="RecyclableStreamBlockSize"/> (256 * 128 KiB).
    /// </summary>
    public static long RecyclableStreamMaximumSmallPoolFreeBytes
    {
        get => RecyclableStreamManager.Settings.MaximumSmallPoolFreeBytes;
        set => RecyclableStreamManager.Settings.MaximumSmallPoolFreeBytes = value;
    }

    /// <summary>
    /// Gets or sets <see cref="RecyclableMemoryStreamManager.Options.MaximumLargePoolFreeBytes"/> of the default <see cref="RecyclableStreamManager"/>.
    /// Defaults to 32 MiB.
    /// </summary>
    public static long RecyclableStreamMaximumLargePoolFreeBytes
    {
        get => RecyclableStreamManager.Settings.MaximumLargePoolFreeBytes;
        set => RecyclableStreamManager.Settings.MaximumLargePoolFreeBytes = value;
    }

    /// <summary>
    /// The 0.0.0.0 Version.
    /// </summary>
    public static readonly Version EmptyVersion = new Version( 0, 0, 0, 0 );

    /// <summary>
    /// Creates a base64 url string using <see cref="System.Security.Cryptography.RandomNumberGenerator.Fill(Span{byte})"/>.
    /// </summary>
    /// <param name="len">Length of the random string.</param>
    /// <returns>A random string.</returns>
    public static string GetRandomBase64UrlString( int len )
    {
        const int MaxStackSize = 128;

        Throw.CheckArgument( len >= 0 );
        if( len == 0 ) return string.Empty;

        var requiredEntropy = 3 * len / 4 + 1;
        var safeSize = Base64.GetMaxEncodedToUtf8Length( requiredEntropy );

        byte[]? fromPool = null;
        Span<byte> buffer = safeSize > MaxStackSize
                            ? (fromPool = ArrayPool<byte>.Shared.Rent( safeSize )).AsSpan( 0, safeSize )
                            : stackalloc byte[safeSize];
        try
        {
            System.Security.Cryptography.RandomNumberGenerator.Fill( buffer.Slice( 0, requiredEntropy ) );
            Base64.EncodeToUtf8InPlace( buffer, requiredEntropy, out int bytesWritten );
            Base64UrlHelper.UncheckedBase64ToUrlBase64NoPadding( buffer, ref bytesWritten );
            Debug.Assert( bytesWritten > len );
            return Encoding.ASCII.GetString( buffer.Slice( 0, len ) );
        }
        finally
        {
            if( fromPool != null ) ArrayPool<byte>.Shared.Return( fromPool );
        }
    }

    /// <summary>
    /// Centralized void action call for any type. 
    /// This method is one of the safest method never written in the world. 
    /// It does absolutely nothing.
    /// </summary>
    /// <param name="obj">Any object.</param>
    [ExcludeFromCodeCoverage]
    public static void ActionVoid<T>( T obj )
    {
    }

    /// <summary>
    /// Centralized void action call for any pair of types. 
    /// This method is one of the safest method never written in the world. 
    /// It does absolutely nothing.
    /// </summary>
    /// <param name="o1">Any object.</param>
    /// <param name="o2">Any object.</param>
    [ExcludeFromCodeCoverage]
    public static void ActionVoid<T1, T2>( T1 o1, T2 o2 )
    {
    }

    /// <summary>
    /// Centralized void action call for any 3 types. 
    /// This method is one of the safest method never written in the world. 
    /// It does absolutely nothing.
    /// </summary>
    /// <param name="o1">Any object.</param>
    /// <param name="o2">Any object.</param>
    /// <param name="o3">Any object.</param>
    [ExcludeFromCodeCoverage]
    public static void ActionVoid<T1, T2, T3>( T1 o1, T2 o2, T3 o3 )
    {
    }

    /// <summary>
    /// Centralized identity function for any type.
    /// </summary>
    /// <typeparam name="T">Type of the function parameter and return value.</typeparam>
    /// <param name="value">Any value returned unchanged.</param>
    /// <returns>The <paramref name="value"/> provided is returned as-is.</returns>
    public static T FuncIdentity<T>( T value ) => value;


    static bool? _isGlobalizationInvariantMode;

    /// <summary>
    /// Whether the CultureInfo will always be the <see cref="CultureInfo.InvariantCulture"/>.
    /// See https://github.com/dotnet/runtime/blob/main/docs/design/features/globalization-invariant-mode.md.
    /// <para>
    /// This ugly code is required: see https://stackoverflow.com/questions/75298957/how-to-detect-globalization-invariant-mode
    /// </para>
    /// </summary>
    /// <returns>True if we are running in Invariant Mode, false otherwise.</returns>
    public static bool IsGlobalizationInvariantMode
    {
        get
        {
            return _isGlobalizationInvariantMode ??= TryIt();

            static bool TryIt()
            {
                try
                {
                    return CultureInfo.GetCultureInfo( "en-US" ).NumberFormat.CurrencySymbol == "¤";
                }
                catch( CultureNotFoundException )
                {
                    return true;
                }
            }
        }
    }

}
