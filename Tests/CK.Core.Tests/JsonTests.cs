using CK.Core.Json;
using Shouldly;
using Microsoft.IO;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace CK.Core.Tests;

[TestFixture]
public class JsonTests
{
    // This reproduces the 2 JsonIdempotenceCheck methods that is available on CK.Testing.IBasicTestHelper.
    T JsonIdempotenceCheck<T>( T o,
                               Action<Utf8JsonWriter, T> write,
                               Utf8JsonReaderDelegate<T> read,
                               IUtf8JsonReaderContext? readerContext = null,
                               Action<string>? jsonText = null )
    {
        readerContext ??= IUtf8JsonReaderContext.Empty;
        // This is safe: a Utf8JsonReaderDelegate<T> is a Utf8JsonReaderDelegate<T,IUtf8JsonReaderContext>.
        return JsonIdempotenceCheckImpl( o, write, Unsafe.As<Utf8JsonReaderDelegate<T, IUtf8JsonReaderContext>>( read ), readerContext, jsonText );
    }

    T JsonIdempotenceCheck<T, TReadContext>( T o,
                                             Action<Utf8JsonWriter, T> write,
                                             Utf8JsonReaderDelegate<T, TReadContext> read,
                                             TReadContext readerContext,
                                             Action<string>? jsonText = null )
        where TReadContext : class, IUtf8JsonReaderContext
    {
        return JsonIdempotenceCheckImpl( o, write, read, readerContext, jsonText );
    }

    T JsonIdempotenceCheckImpl<T, TReadContext>( T o,
                                                 Action<Utf8JsonWriter, T> write,
                                                 Utf8JsonReaderDelegate<T, TReadContext> read,
                                                 TReadContext readerContext,
                                                 Action<string>? jsonText )
        where TReadContext : class, IUtf8JsonReaderContext
    {
        using( var m = (RecyclableMemoryStream)Util.RecyclableStreamManager.GetStream() )
        using( Utf8JsonWriter w = new Utf8JsonWriter( (IBufferWriter<byte>)m ) )
        {
            write( w, o );
            w.Flush();
            string? text1 = Encoding.UTF8.GetString( m.GetReadOnlySequence() );
            jsonText?.Invoke( text1 );
            var reader = new Utf8JsonReader( m.GetReadOnlySequence() );
            Throw.DebugAssert( reader.TokenType == JsonTokenType.None );
            reader.ReadWithMoreData( readerContext );
            var oBack = read( ref reader, readerContext );
            if( oBack == null )
            {
                Throw.CKException( $"A null has been read back from '{text1}' for a non null instance of '{typeof( T ).ToCSharpName()}'." );
            }
            string? text2 = null;
            m.Position = 0;
            using( var w2 = new Utf8JsonWriter( (IBufferWriter<byte>)m ) )
            {
                write( w2, oBack );
                w2.Flush();
                text2 = Encoding.UTF8.GetString( m.GetReadOnlySequence() );
            }
            if( text1 != text2 )
            {
                Throw.CKException( $"""
                            Json idempotence failure between first write:
                            {text1}

                            And second write of the read back {typeof( T ).ToCSharpName()} instance:
                            {text2}

                            """ );
            }
            return oBack;
        }
    }

    [Test]
    public void idempotence_check_for_Event()
    {
        DateTime now = DateTime.UtcNow;
        var e = new Event( now, "Description" );
        var e2 = JsonIdempotenceCheck( e, Write, ReadEvent );
        e2.Time.ShouldBe( now );
        e2.Description.ShouldBe( "Description" );
    }

    [Test]
    public void idempotence_check_for_SuperData()
    {
        var limit = new DateTime( 2023, 9, 14 );
        var d = new SuperData
        {
            Events =
            {
                new Event( Util.UtcMinValue, "Old" ),
                new Event( limit, "Limit" ),
                new Event( DateTime.UtcNow, "Current" )
            }
        };
        var d2 = JsonIdempotenceCheck( d, Write, ReadSuperData, new SuperDataReaderContext( null, Util.UtcMinValue ) );
        d2.Events.Count.ShouldBe( 3 );
        d2.Events.ShouldBeEquivalentTo( d.Events );

        Should.Throw<CKException>( () => JsonIdempotenceCheck( d, Write, ReadSuperData, new SuperDataReaderContext( null, limit ) ) )
            .Message.ShouldStartWith( "Json idempotence failure between" );
    }

    // Simple event.
    readonly record struct Event( DateTime Time, string Description );

    // Simple class that uses a specific reader context.
    class SuperData
    {
        public readonly List<Event> Events = new List<Event>();
    }

    static void Write( Utf8JsonWriter w, SuperData v )
    {
        w.WriteStartObject();
        w.WritePropertyName( "Events"u8 );
        w.WriteStartArray();
        foreach( var e in v.Events ) Write( w, e );
        w.WriteEndArray();
        w.WriteEndObject();
    }

    // To be automatically composable, reader contexts should be used by interface.
    // They should be like IPoco: they should be fully mutable. Their final implementation
    // should be unified but explicit implementations should be used (property name are not
    // shared). A unique final IUtf8JsonReaderContext should be code generated.
    interface ISuperDataReaderContext : IUtf8JsonReaderContext
    {
        DateTime MinEventTime { get; set; }
    }

    class SuperDataReaderContext : ISuperDataReaderContext
    {
        readonly IUtf8JsonReaderContext _inner;

        public SuperDataReaderContext( IUtf8JsonReaderContext? inner, DateTime minEventTime )
        {
            _inner = inner ?? IUtf8JsonReaderContext.Empty;
            MinEventTime = minEventTime;
        }

        public DateTime MinEventTime { get; set; }

        public void ReadMoreData( ref Utf8JsonReader reader ) => _inner.ReadMoreData( ref reader );
        public void SkipMoreData( ref Utf8JsonReader reader ) => _inner.SkipMoreData( ref reader );
    }

    static SuperData ReadSuperData( ref Utf8JsonReader r, ISuperDataReaderContext context )
    {
        r.SkipComments( context );
        if( r.TokenType != JsonTokenType.StartObject ) r.ThrowExpectedJsonException( nameof( SuperData ) );
        r.ReadWithMoreData( context );
        r.SkipComments( context );
        // "Events" property is required.
        if( r.TokenType != JsonTokenType.PropertyName || !r.ValueTextEquals( "Events"u8 ) )
        {
            r.ThrowExpectedJsonException( "SuperData.Events" );
        }
        r.ReadWithMoreData( context );
        var result = new SuperData();
        if( r.TokenType != JsonTokenType.StartArray ) r.ThrowExpectedJsonException( nameof( JsonTokenType.StartArray ) );
        r.ReadWithMoreData( context );
        r.SkipComments( context );
        while( r.TokenType != JsonTokenType.EndArray )
        {
            var e = ReadEvent( ref r, context );
            if( e.Time >= context.MinEventTime ) result.Events.Add( e );
        }
        r.ReadWithMoreData( context );
        return result;
    }

    static void Write( Utf8JsonWriter w, Event v )
    {
        w.WriteStartObject();
        w.WritePropertyName( "Time"u8 );
        w.WriteStringValue( v.Time );
        w.WriteString( "Description"u8, v.Description );
        w.WriteEndObject();
    }

    static Event ReadEvent( ref Utf8JsonReader r, IUtf8JsonReaderContext context )
    {
        r.SkipComments( context );
        if( r.TokenType != JsonTokenType.StartObject ) r.ThrowExpectedJsonException( nameof( Event ) );
        r.ReadWithMoreData( context );
        r.SkipComments( context );
        // This pattern allows extra properties to be skipped.
        DateTime? time = null;
        string? description = null;
        while( r.TokenType != JsonTokenType.EndObject )
        {
            Throw.DebugAssert( r.TokenType == JsonTokenType.PropertyName );
            if( r.ValueTextEquals( "Time"u8 ) )
            {
                r.ReadWithMoreData( context );
                time = r.GetDateTime();
                r.ReadWithMoreData( context );
            }
            else if( r.ValueTextEquals( "Description"u8 ) )
            {
                r.ReadWithMoreData( context );
                description = r.GetString();
                r.ReadWithMoreData( context );
            }
            else r.SkipWithMoreData( context );
            r.SkipComments( context );
        }
        r.ReadWithMoreData( context );
        if( description == null ) r.ThrowExpectedJsonException( "Non null Event.Description" );
        if( !time.HasValue ) r.ThrowExpectedJsonException( "Event.Time" );
        return new Event( time.Value, description );
    }
}
