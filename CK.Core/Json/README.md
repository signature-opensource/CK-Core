# Json serialization

For performance reasons we use as much as possible the low-level `Utf8JsonReader` and `Utf8JsonWriter`
types. And we don't use async context for object serialization: using async methods for read/write negatively
impacts the majority of the scenario (95%?) in which input data fits in memory. This is the case for small
messages and even for size-prefixed protocol (the message payload is in almost always in memory).

## Extending Utf8JsonReader
Helpers for readers provided by CK.Core support the pattern described [here](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-utf8jsonreader#read-from-a-stream-using-utf8jsonreader).
They enable the `Utf8JsonReader` to read from a stream (or any other memory manager that avoids loading the whole
data input in memory) with the help of an extra [`IUtf8JsonReaderContext`](IUtf8JsonReaderContext.cs) parameter
that can provide more data as needed.

The [`Utf8JsonStreamReaderContext`](Utf8JsonStreamReaderContext.cs) implements the [`IUtf8JsonReaderContext`](IUtf8JsonReaderContext.cs)
interface for the standard Stream type.

This extended pattern requires Json reader functions to accept one more parameter than the minimal pattern that
only takes the `ref Utf8JsonReader reader`. The [`Utf8JsonReaderDelegate`](Utf8JsonReaderDelegate.cs) delegates capture
this signature for typed reads, the second one for a specialized reader context:
```csharp
public delegate T? Utf8JsonReaderDelegate<T>( ref Utf8JsonReader r, IUtf8JsonReaderContext context );

public delegate T? Utf8JsonReaderDelegate<T, TReadContext>( ref Utf8JsonReader r, TReadContext context ) where TReadContext : class, IUtf8JsonReaderContext;
```

The pattern to use it is to replace all calls to `r.Read()` with
`if( !r.Read() ) context.ReadMoreData( ref r );` and all `r.Skip()` calls with
`if( !r.TrySkip() ) context.SkipMoreData( ref r );`, or use the extension methods (`r.ReadWithMoreData( context )`
and `r.SkipWithMoreData( context )`).

The `context` can also be used to provide additional options to reader functions (by implementing a wrapper context)
like a model description (for model binding), strategies to resolve types to handle polymorphism, or any kind of
hooks that may be required while reading Json.

When no context is required (the input data is in memory - in a `ReadOnlySpan<byte>` or a `ReadOnlySequence<byte>` -
and the reader function doesn't require any option), the `IUtf8JsonReaderContext.Empty` singleton can be used.

Static factories are available on the `Utf8JsonStreamReaderContext` that can create context and a `Utf8JsonReader`
for a `ReadOnlySequence<byte>`, for a stream that must not be a `RecyclableMemoryStream` and for any kind of stream:
```csharp
IUtf8JsonReaderContext Create( ReadOnlySequence<byte> sequence, JsonReaderOptions options, out Utf8JsonReader r ) { ... }
Utf8JsonStreamReaderContext CreateStreamReader( Stream stream,
                                                JsonReaderOptions options,
                                                out Utf8JsonReader r,
                                                bool leaveOpened = false,
                                                int initialBufferSize = 512,
                                                int maxBufferSize = int.MaxValue ) { ... }
IDisposableUtf8JsonReaderContext Create( Stream stream,
                                         JsonReaderOptions options,
                                         out Utf8JsonReader r,
                                         bool leaveOpened = false,
                                         // The following are ignored when stream is RecyclableMemoryStream.
                                         int initialBufferSize = 512, 
                                         int maxBufferSize = int.MaxValue )
```

## Don't forget the comments!
Comments can be forbidden or skipped at the `Utf8JsonReader` level. However when writing library code,
you should handle them: the library code has no control of the `JsonReaderOptions.CommentHandling` that has
been used to initialize the reader. If for any reason, the caller used `JsonCommentHandling.Allow`, and your
code doesn't skip the comment tokens, it will miserably fail to parse the data.

You can use the extension method `SkipComments` after each `ReadWithMoreData` or `SkipWithMoreData`:
```csharp
/// <summary>
/// Skips 0 or more <see cref="JsonTokenType.Comment"/>.
/// </summary>
/// <param name="r">This reader.</param>
/// <param name="context">The reader context.</param>
[MethodImpl( MethodImplOptions.AggressiveInlining )]
public static void SkipComments( this ref Utf8JsonReader r, IUtf8JsonReaderContext context )
{
    while( r.TokenType == JsonTokenType.Comment ) r.ReadWithMoreData( context );
}
```

This allows your code to parse `jsonc` as well as `json`.

## This is NOT a simple, easy to use, idiot-proof API!
Actually it is the opposite but it provides a strong control of how write and read are done and can handle
any strange edge case.

Maximal care should be taken when writing such code by hand: this is in practice mainly used by code generation.

## What about write helpers?
No writer helper exists: writing doesn't need specific memory management other than the already
existing solutions (streams can be the target of a `Utf8JsonWriter`).

Any _ad hoc_ context or options can always be provided to writer functions. 
