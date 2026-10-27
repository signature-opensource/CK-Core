using Microsoft.IO;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace CK.Core;

/// <summary>
/// Provides a reading buffer on a Utf8 stream of bytes. 
/// The Utf8 Byte Order Mask (BOM: 0xEF, 0xBB, 0xBF) that may exist at the start is
/// kindly handled.
/// <para>
/// The buffer comes from the <see cref="ArrayPool{T}.Shared"/> of bytes: this
/// reader MUST be disposed to return the current buffer to the pool.
/// </para>
/// <para>
/// The pattern to use it is to replace all <c>Read()</c> calls with:
/// <code>
/// if( !reader.Read() ) streamReader.ReadMoreData( ref reader );
/// </code>
/// And all <c>Skip()</c> calls with:
/// <code>
/// if( !reader.TrySkip() ) streamReader.SkipMoreData( ref reader );
/// </code>
/// </para>
/// <para>
/// This implements the pattern described here:
/// https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-utf8jsonreader#read-from-a-stream-using-utf8jsonreader
/// </para>
/// </summary>
public sealed class Utf8JsonStreamReaderContext : IDisposableUtf8JsonReaderContext
{
    readonly Stream _stream;
    byte[] _buffer;
    int _initialOffset;
    readonly bool _leaveOpened;
    int _count;
    readonly int _maxBufferSize;

    Utf8JsonStreamReaderContext( Stream stream, byte[] buffer, int count, int initialOffset, bool leaveOpened, int maxBufferSize )
    {
        _stream = stream;
        _buffer = buffer;
        _count = count;
        _initialOffset = initialOffset;
        _leaveOpened = leaveOpened;
        _maxBufferSize = maxBufferSize;
    }

    /// <summary>
    /// Returns <see cref="IUtf8JsonReaderContext.Empty"/> and a Utf8 reader
    /// bound to the <paramref name="sequence"/>. This handles a potential
    /// Utf8 Byte Order Mask (BOM: 0xEF, 0xBB, 0xBF) that may exist at the start
    /// of the sequence.
    /// </summary>
    /// <param name="sequence">The in memory bytes.</param>
    /// <param name="options">The Json reader options.</param>
    /// <param name="r">The reader.</param>
    /// <returns>An empty context.</returns>
    public static IUtf8JsonReaderContext Create( ReadOnlySequence<byte> sequence,
                                                 JsonReaderOptions options,
                                                 out Utf8JsonReader r )
    {
        r = CreateUtf8JsonReader( sequence, options );
        return IUtf8JsonReaderContext.Empty;
    }

    static Utf8JsonReader CreateUtf8JsonReader( ReadOnlySequence<byte> sequence, JsonReaderOptions options )
    {
        Utf8JsonReader r;
        var s = sequence.FirstSpan;
        if( s.Length >= 3 && s[0] == 0xEF && s[1] == 0xBB && s[2] == 0xBF )
        {
            sequence = sequence.Slice( 3 );
        }
        r = new Utf8JsonReader( sequence, true, new JsonReaderState( options ) );
        return r;
    }

    sealed class EmptyStreamContext : IDisposableUtf8JsonReaderContext
    {
        readonly Stream _stream;

        public EmptyStreamContext( Stream stream )
        {
            _stream = stream;
        }

        public void Dispose() => _stream.Dispose();

        public void ReadMoreData( ref Utf8JsonReader reader ) { }

        public void SkipMoreData( ref Utf8JsonReader reader ) { }
    }

    /// <summary>
    /// Creates a context reader and a <see cref="Utf8JsonReader"/> that applies to any stream
    /// including <see cref="RecyclableMemoryStream"/>.
    /// <para>
    /// When the stream is recyclable, the Utf8 reader is bound to the <see cref="RecyclableMemoryStream.GetReadOnlySequence()"/>
    /// and an empty context (that disposes the stream if <paramref name="leaveOpened"/> is false) is returned.
    /// In this case, <paramref name="initialBufferSize"/> and <paramref name="maxBufferSize"/> are ignored.
    /// </para>
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="options">The reader options.</param>
    /// <param name="r">The initial reader.</param>
    /// <param name="leaveOpened">
    /// True to let the stream opened when disposing the returned <see cref="IDisposableUtf8JsonReaderContext"/>.
    /// By default, the stream is disposed.
    /// </param>
    /// <param name="initialBufferSize">
    /// (Ignored for RecyclableMemoryStream.) Initial buffer size (will grow as needed). The buffer size is only driven by
    /// the longest token (plus some white space) to read.
    /// Currently, the <c>ArrayPool&lt;byte&gt;.Shared</c> that is used returns at least 16 bytes: the
    /// initial buffer size will at least be 16.
    /// </param>
    /// <param name="maxBufferSize">
    /// (Ignored for RecyclableMemoryStream.) The maximal buffer size to use (for the bigger token).</param>
    /// <returns>A read context that must be disposed.</returns>
    public static IDisposableUtf8JsonReaderContext Create( Stream stream,
                                                           JsonReaderOptions options,
                                                           out Utf8JsonReader r,
                                                           bool leaveOpened = false,
                                                           int initialBufferSize = 512,
                                                           int maxBufferSize = int.MaxValue )
    {
        if( stream is RecyclableMemoryStream recycle )
        {
            r = CreateUtf8JsonReader( recycle.GetReadOnlySequence(), options );
            return leaveOpened
                    ? IUtf8JsonReaderContext.Empty
                    : new EmptyStreamContext( stream );
        }
        return CreateStreamReader( stream, options, out r, leaveOpened, initialBufferSize, maxBufferSize );
    }


    /// <summary>
    /// Creates a new <see cref="Utf8JsonStreamReaderContext"/> and an initial reader.
    /// <para>
    /// The Utf8 Byte Order Mask (BOM: 0xEF, 0xBB, 0xBF) that may exist at the start is
    /// kindly handled.
    /// </para>
    /// <para>
    /// The <paramref name="stream"/> MUST NOT be a <see cref="RecyclableMemoryStream"/> otherwise an <see cref="ArgumentException"/>
    /// is thrown: "Please use the Create( Stream stream,... ) that adapts to the stream type.".
    /// </para>
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="options">The Json reader options.</param>
    /// <param name="r">The initial reader.</param>
    /// <param name="leaveOpened">
    /// True to let the stream opened when disposing the returned <see cref="Utf8JsonStreamReaderContext"/>.
    /// By default, the stream is disposed.
    /// </param>
    /// <param name="initialBufferSize">
    /// Initial buffer size (will grow as needed). The buffer size is only driven by
    /// the longest token (plus some white space) to read.
    /// Currently, the <c>ArrayPool&lt;byte&gt;.Shared</c> that is used returns at least 16 bytes: the
    /// initial buffer size will at least be 16.
    /// </param>
    /// <param name="maxBufferSize">The maximal buffer size to use (for the bigger token).</param>
    /// <returns>A read context that must be disposed.</returns>
    public static Utf8JsonStreamReaderContext CreateStreamReader( Stream stream,
                                                                  JsonReaderOptions options,
                                                                  out Utf8JsonReader r,
                                                                  bool leaveOpened = false,
                                                                  int initialBufferSize = 512,
                                                                  int maxBufferSize = int.MaxValue )
    {
        Throw.CheckNotNullArgument( stream );
        // initialBufferSize may be 0 or even negative. It is normalized to at least 4 in ReadFirstBuffer.
        Throw.CheckArgument( maxBufferSize >= initialBufferSize );
        Throw.CheckArgument( "Please use the Create( Stream stream,... ) that adapts to the stream type.", stream is not RecyclableMemoryStream );
        if( ReadFirstBuffer( stream, out var buffer, out var count, out var initialOffset, initialBufferSize ) )
        {
            r = new Utf8JsonReader( buffer.AsSpan( initialOffset, count - initialOffset ), false, new JsonReaderState( options ) );
        }
        else
        {
            Debug.Assert( initialOffset == 0 );
            count = 0;
            r = new Utf8JsonReader( ReadOnlySpan<byte>.Empty, options );
        }
        return new Utf8JsonStreamReaderContext( stream, buffer, count, initialOffset, leaveOpened, maxBufferSize );

        static bool ReadFirstBuffer( Stream stream, out byte[] buffer, out int count, out int offset, int initialBufferSize )
        {
            // ArrayPool gives us 16 bytes at least but 4 is the required min size to handle the BOM.
            buffer = ArrayPool<byte>.Shared.Rent( Math.Max( initialBufferSize, 4 ) );
            offset = 0;
            int lenRead = count = stream.Read( buffer );
            if( lenRead == 0 ) return false;
            if( buffer[0] == 0xEF ) // Start of Utf8 BOM that is: 0xEF, 0xBB, 0xBF
            {
                // If not all the BOM is found, we let the data as-is: the reader will fail.
                // We must ensure at least one "real" byte: we need at least 4 bytes.
                while( count < 4 )
                {
                    lenRead = stream.Read( buffer.AsSpan( count ) );
                    // If we cannot read 4 bytes with a leading BOM, consider it
                    // as an empty data since it is not Json.
                    if( lenRead == 0 ) return false;
                    count += lenRead;
                }
                if( buffer[1] == 0xBB && buffer[2] == 0xBF )
                {
                    offset = 3;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Gets whether this reader is disposed.
    /// </summary>
    public bool IsDisposed => _buffer == null;

    /// <summary>
    /// Gets the whole current buffer.
    /// </summary>
    public ReadOnlySpan<byte> RawBuffer
    {
        get
        {
            Throw.CheckState( !IsDisposed );
            return _buffer;
        }
    }

    /// <summary>
    /// Gets the unread available bytes in the buffer.
    /// This uses the <see cref="Utf8JsonReader.BytesConsumed"/>, the
    /// current count of read bytes in the buffer and ignores the leading
    /// Utf8 Byte Order Mask if any. 
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The unread available bytes.</returns>
    public ReadOnlySpan<byte> GetUnreadBytes( ref Utf8JsonReader reader )
    {
        Throw.CheckState( !IsDisposed );
        int offset = _initialOffset + (int)reader.BytesConsumed;
        return _buffer.AsSpan( offset, _count - offset );
    }

    /// <summary>
    /// Disposes this reader.
    /// </summary>
    public void Dispose()
    {
        var b = _buffer;
        if( b != null )
        {
            _buffer = null!;
            ArrayPool<byte>.Shared.Return( b, clearArray: true );
            if( !_leaveOpened ) _stream.Dispose();
        }
    }

    /// <inheritdoc />
    public void ReadMoreData( ref Utf8JsonReader reader ) => ReadMoreData( ref reader, false );

    /// <inheritdoc />
    public void SkipMoreData( ref Utf8JsonReader reader )
    {
        // TrySkip returns always true when TokenType is not a PropertyName, a StartObject or StartArray.
        // If we want to be aggressive here we could require that the caller does this check:
        //      Throw.CheckState( reader.TokenType == JsonTokenType.PropertyName || reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray );
        // But this is not how this API has been designed: a Skip/TrySkip requires a subsequent Read() call that does skip the token if it's not one of these.
        if( reader.TokenType == JsonTokenType.PropertyName )
        {
            ReadMoreData( ref reader, false );
        }
        if( reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray )
        {
            if( !reader.TrySkip() ) ReadMoreData( ref reader, true );
        }
    }

    void ReadMoreData( ref Utf8JsonReader reader, bool skip )
    {
        int bytesConsumed = (int)reader.BytesConsumed + _initialOffset;
        // Not needed anymore (only for the BOM of the first buffer).
        _initialOffset = 0;
        // This is for skip handling (if skip parameter is true).
        int initialDepth = reader.CurrentDepth;

        retry:
        if( reader.IsFinalBlock ) return;
        int bytesRead;
        int unread = _count - bytesConsumed;
        if( unread > 0 )
        {
            if( bytesConsumed == 0 )
            {
                if( _count == _buffer.Length )
                {
                    if( _buffer.Length == _maxBufferSize )
                    {
                        reader.ThrowJsonException( $"A token requires more than MaxBufferSize = {_maxBufferSize} bytes." );
                    }
                    byte[] newBuffer = ArrayPool<byte>.Shared.Rent( (_buffer.Length < (_maxBufferSize / 2)) ? _buffer.Length * 2 : _maxBufferSize );
                    Buffer.BlockCopy( _buffer, bytesConsumed, newBuffer, 0, unread );
                    ArrayPool<byte>.Shared.Return( _buffer, clearArray: true );
                    _buffer = newBuffer;
                }
            }
            else
            {
                Buffer.BlockCopy( _buffer, bytesConsumed, _buffer, 0, unread );
            }
            bytesRead = _stream.Read( _buffer.AsSpan( unread ) );
            _count = unread + bytesRead;
        }
        else
        {
            _count = bytesRead = _stream.Read( _buffer );
        }
        reader = new Utf8JsonReader( _buffer.AsSpan( 0, _count ), isFinalBlock: bytesRead == 0, reader.CurrentState );
        if( reader.Read()
            && (!skip
                 || initialDepth == reader.CurrentDepth
                 || SkipUntil( ref reader, initialDepth )) )
        {
            // We have read a token and we are not skipping or the skip is over.
            return;
        }
        bytesConsumed = (int)reader.BytesConsumed;
        goto retry;

        static bool SkipUntil( ref Utf8JsonReader reader, int initialDepth )
        {
            do
            {
                if( !reader.Read() ) return false;
            }
            while( initialDepth != reader.CurrentDepth );
            return true;
        }
    }
}

