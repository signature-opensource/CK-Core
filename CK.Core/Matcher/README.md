# ROSpanMatcher: the "Match and Forward" pattern

## Match and Forward
This handy little parsing pattern allows you to combine parsing functions much more easily and efficiently
than the standard TryParse methods. A few explanations below.

Static TryParse methods accept a `string` (and now rather a `ReadOnlySpan<char>`), return a boolean and the out
parameter of the parsed value on success: 

```csharp
public struct UInt32
{
  public static bool TryParse( ReadOnlySpan<char> s, out uint result )
  {
    //...
  }
}
```

It's convenient... but this pattern has a big problem: 
 - the string doesn't support a suffix: TryParse("5454 ", out _ ) fails because of the final little white space.
 - this doesn't tell us how many characters have been matched (because they only match what you provide).

This prohibits chaining these methods together to parse small, not too complex grammars in a simple way: in fact,
they cannot be combined but rather used with "splits" on specific delimiting characters (which, besides limiting the
flexibility of the patterns, systematically introduces several consecutive passes on the string).

The Matcher pattern does the same thing except that the principle is to advance a read head (or at least to be able
to advance a read head knowing the length of the match) in order to chain the calls and "advance in the grammar". 

Below is an example of a simple code that parses a LED state (Color and State) and advances a `ReadOnlySpan<char>` read head
passed by reference (otherwise the head would not advance!).

(If you're not familiar with `ReadOnlySpan` `Span` and other `Memory`, please read this: https://docs.microsoft.com/en-us/archive/msdn-magazine/2018/january/csharp-all-about-span-exploring-a-new-net-mainstay.)

This first method simply handles one character.

```csharp
public static bool TryMatch( ref ReadOnlySpan<char> h, out LEDColor color )
{
    color = default;
    if( h.Length == 0 ) return false;
    switch( h[0] )
    {
        case 'W': color = LEDColor.White; break;
        case 'R': color = LEDColor.Red; break;
        case 'G': color = LEDColor.Green; break;
        case 'B': color = LEDColor.Blue; break;
        case 'Y': color = LEDColor.Yellow; break;
        case 'M': color = LEDColor.Magenta; break;
        case 'C': color = LEDColor.Cyan; break;
        default: return false;
    };
    h = h.Slice( 1 );
    return true;
}
```

Now, a LEDState is a color and a possible combination of Off, Blink and/or Transparent modifiers (in this order separated by commas
without any white space):

```csharp
public static bool TryMatch( ref ReadOnlySpan<char> h, out LEDState state )
{
    state = default;
    if( TryMatch( ref h, out LEDColor color ) )
    {
        state = state.SetColor( color );
        if( h.Length > 3 )
        {
            state = state.Switch( !h.TryMatch( ",Off" ) )
                            .SetBlinking( !h.TryMatch( ",Blink" ) )
                            .SetOpacity( !h.TryMatch( ",Transparent" ) );
        }
        return true;
    }
    return false;
}
```

The model also handles a somewhat special state: the "Background" which is the fact that the LED must be in
the default state defined by the LED strip. In code this is represented by a `Nullable<LEDState>` (a null `LEDState?`):
```csharp
public static bool TryMatch( ref ReadOnlySpan<char> h, out LEDState? state )
{
    state = null;
    if( h.TryMatch( "Background" ) )
    {
        return true;
    }
    if( TryMatch( ref h, out LEDState s ) )
    {
        state = s;
        return true;
    }
    return false;
}
```

Here we can start to see the combinations at work.
What about the standard .Net TryParse? It is simply a match followed by the end of the chain:

```csharp
/// <summary>
/// Tries to parse a nullable <see cref="LEDState"/> (see <see cref="LEDExtensions.ToParseable(LEDState?)"/>).
/// The "Background" string is the null state.
/// </summary>
/// <param name="s">The string to parse.</param>
/// <param name="state">The resulting state.</param>
/// <returns>True on success, false is not matched.</returns>
public static bool TryParse( ReadOnlySpan<char> s, out LEDState? state ) => TryMatch( ref s, out state ) && s.IsEmpty;
```

It's always the same piece of code to write (CodeGeneration here would be great...).
To conclude this sample on composition, a "piece of LED strip" (a pipe separated list of nullable LEDState):

```csharp
/// <summary>
/// Tries to match a pattern: a pipe separated list of <see cref="LEDState?"/> (see <see cref="TryMatch(ref ReadOnlySpan{char}, out LEDState?)"/>).
/// </summary>
/// <param name="h">The head.</param>
/// <param name="pattern">The resulting pattern.</param>
/// <returns>True on success, false is not matched.</returns>
public static bool TryMatch( ref ReadOnlySpan<char> h, [NotNullWhen(true)]out List<LEDState?>? pattern )
{
    pattern = null;
    while( TryMatch( ref h, out LEDState? s ) )
    {
        if( pattern == null ) pattern = new List<LEDState?>();
        pattern.Add( s );
        if( !h.TryMatch( '|' ) ) return true;
    }
    // It is better to return the default on failure.
    pattern = null;
    return false;
}
```

The `bool TryParse( ReadOnlySpan<char> s, out LEDState? state ) => TryMatch( ref s, out state ) && s.IsEmpty;` is not the best
design because the parsed result is created (and lost) if the string has a remainder. This is not necessarilly an issue for value
types but for reference types it's not ideal. 

## Recommended implementation pattern
Types that supports this pattern should implement the actual code in a private `TryMatch` method that accepts a `bool parse` parameter
and relay public `TryParse` and `TryMatch` to it. Below is the complete example of the `DateTimeStamp` implementation:

```csharp
/// <summary>
/// Tries to match a <see cref="DateTimeStamp"/> and forwards the <paramref name="head"/> on success.
/// </summary>
/// <param name="head">This parsing head.</param>
/// <param name="time">Resulting time stamp on successful match; <see cref="DateTimeStamp.Unknown"/> otherwise.</param>
/// <returns>True on success, false otherwise.</returns>
static public bool TryMatch( ref ReadOnlySpan<char> head, out DateTimeStamp time ) => TryMatch( ref head, out time, false );

/// <summary>
/// Tries to parse a <see cref="DateTimeStamp"/>.
/// <para>
/// The extension method <see cref="DateTimeStampExtension.TryMatchDateTimeStamp(ref ROSpanCharMatcher, out DateTimeStamp)"/>
/// is also available.
/// </para>
/// </summary>
/// <param name="s">The string to parse.</param>
/// <param name="time">Resulting time stamp on successful match; <see cref="DateTimeStamp.Unknown"/> otherwise.</param>
/// <returns>True on success, false otherwise.</returns>
static public bool TryParse( ReadOnlySpan<char> s, out DateTimeStamp time ) => TryMatch( ref s, out time, true );

static bool TryMatch( ref ReadOnlySpan<char> head, out DateTimeStamp time, bool parse )
{
    var savedHead = head;
    if( !head.TryMatchFileNameUniqueTimeUtcFormat( out var t ) ) goto error;
    byte uniquifier = 0;
    if( head.TryMatch( '(' ) )
    {
        if( !head.TryMatchInt32( out int u, 0, 255 ) || !head.TryMatch( ')' ) ) goto error;
        uniquifier = (byte)u;
    }
    if( !parse || head.IsEmpty )
    {
        time = new DateTimeStamp( t, uniquifier );
        return true;
    }
    error:
    time = Unknown;
    head = savedHead;
    return false;
}

/// <summary>
/// Parses a <see cref="DateTimeStamp"/> or throws a <see cref="FormatException"/>.
/// </summary>
/// <param name="s">The string to parse.</param>
/// <returns>The DateTimeStamp.</returns>
static public DateTimeStamp Parse( ReadOnlySpan<char> s )
{
    if( !TryParse( s, out var time ) ) Throw.FormatException( $"Invalid DateTimeStamp: '{s}'." );
    return time;
}
```

## Extension methods at will

For well known general purpose types, you may want to expose the `TryMatch` methods as extension methods of the "head" but if you
can change the code, the `public static bool TryMatch(...)` is better located in the type itself (next to `TryParse` and `Parse`).

Extension methods are already defined for .Net types (see [ReadOnlySpanCharExtensions.cs](ReadOnlySpanCharExtensions.cs))
like this one for instance:

```csharp
/// <summary>
/// Tries to parse a boolean "true" or "false" (case insensitive).
/// </summary>
/// <param name="head">This head.</param>
/// <param name="b">The result boolean. False on failure.</param>
/// <returns>True on success, false otherwise.</returns>
public static bool TryMatchBool( this ref ReadOnlySpan<char> head, out bool b )
{
    b = false;
    if( head.Length >= 4 )
    {
        if( head.TryMatch( "false", StringComparison.OrdinalIgnoreCase )
            || (b = head.TryMatch( "true", StringComparison.OrdinalIgnoreCase )) )
        {
            return true;
        }
    }
    return false;
}
```

## More complex grammars: exposing detailed syntax errors

Previous examples are simple. However when combining them, even an error in one Color character may be hard to spot in a complex
grammar. Feedback to the user is crucial in any complex system. Without error management, the readability and maintainability of
the combinations of these small TryMatch functions become unusable in practice (a user cannot be satisfied with a terse "Invalid Syntax",
he must be told where and what is invalid).

To support errors and expectations, the [ROSpanCharMatcher](ROSpanCharMatcher.cs) replaces the mere `ReadOnlySpan<char>`.
Below is an example of the same simple color match that explicit its error:

```csharp
public static bool TryMatch( ref ROSpanCharMatcher m, out LEDColor color )
{
    color = default;
    if( m.Head.Length > 0 )
    {
        switch( m.Head[0] )
        {
            case 'W': color = LEDColor.White; break;
            case 'R': color = LEDColor.Red; break;
            case 'G': color = LEDColor.Green; break;
            case 'B': color = LEDColor.Blue; break;
            case 'Y': color = LEDColor.Yellow; break;
            case 'M': color = LEDColor.Magenta; break;
            case 'C': color = LEDColor.Cyan; break;
            default: goto error; 
        };
        m.Head = m.Head.Slice( 1 );
        return m.SetSuccess();
    }
    error:
    return m.AddExpectation( "Color char: W, R, G, B, Y, M or C." );
}
```

The `ROSpanCharMatcher` exposes:
 - its mutable head directly, so it can be moved freely (and without overhead).
 - a set of methods that manage errors and expectations:
   - `AddExpectations` describes the expected input and always returns false.
   - `AddError` signals an error and always returns false.
   - `OpenExpectation` returns a IDisposable and enables to give a name to a complex subordinated pattern and 
   structures the expectations/errors in a tree-like structure (more on this below).
   - `SetSuccess` that always returns true and must be called to clear any pending errors/expectations 
   (at the current `OpenExpectation` level). 

Just like `TryParse` standard implementations, this new `TryMatch` can rely on the basic `ReadOnlySpan<char>` one so we can
keep both versions without overhead.
Keeping the 2 versions makes sense for small, terminal, matchers but quickly, when the patterns become more complex, only
the `ROSpanCharMatcher` with its error management should be supported. The final implementations become:

```csharp
public static bool TryMatch( ref ReadOnlySpan<char> h, out LEDColor color )
{
    color = default;
    if( h.Length == 0 ) return false;
    switch( h[0] )
    {
        case 'W': color = LEDColor.White; break;
        case 'R': color = LEDColor.Red; break;
        case 'G': color = LEDColor.Green; break;
        case 'B': color = LEDColor.Blue; break;
        case 'Y': color = LEDColor.Yellow; break;
        case 'M': color = LEDColor.Magenta; break;
        case 'C': color = LEDColor.Cyan; break;
        default: return false;
    };
    h = h.Slice( 1 );
    return true;
}

public static bool TryMatch( ref ROSpanCharMatcher m, out LEDColor color )
    => TryMatch( ref m.Head, out color )
        ? m.SetSuccess()
        : m.AddExpectation( "Color char: W, R, G, B, Y, M or C" );
```

This may seem easy but it is not! When not matched, the head MUST be left where it was when the `TryMatch` method enters.
Expectations and errors MUST be cleared by calling `SetSuccess()` on calling. When patterns become complex this quickly
requires rigor... and tests!

### Simple sample and dangerous traps

This sample tries to match any comma separated combination of "First,Last" in 4 languages. It corresponds to the
regular expressions: `(First|Premier|Primero|Erste),(Last|Dernier|Última|Letzter)`

The following method has 2 issues. Can you spot them?

```csharp
static bool TryMatchFirstAndLast( ROSpanCharMatcher m )
{
    if( (m.TryMatch( "First" ) || m.TryMatch( "Premier" ) || m.TryMatch( "Primero" ) || m.TryMatch( "Erste" ))
        && m.TryMatch( ',' )
        && (m.TryMatch( "Last" ) || m.TryMatch( "Dernier" ) || m.TryMatch( "Última" ) || m.TryMatch( "Letzter" )) )
    {
        return m.SetSuccess();
    }
    return false;
}
``` 

First, `ref` has been forgotten: the caller will see the errors and expectations but the head of its matcher will NOT be forwarded
on success. Let's fix this:
```csharp
static bool TryMatchFirstAndLast( ref ROSpanCharMatcher m )
``` 
Now, on success, the head will be forwarded after the pattern BUT on error, the head will be where the match fails. To fix this we
need to "save the head" and restore it on failure:

```csharp
static bool TryMatchFirstAndLast( ref ROSpanCharMatcher m )
{
    var savedHead = m.Head;
    if( (m.TryMatch( "First" ) || m.TryMatch( "Premier" ) || m.TryMatch( "Primero" ) || m.TryMatch( "Erste" ))
        && m.TryMatch( ',' )
        && (m.TryMatch( "Last" ) || m.TryMatch( "Dernier" ) || m.TryMatch( "Última" ) || m.TryMatch( "Letzter" )) )
    {
        return m.SetSuccess();
    }
    m.Head = savedHead;
    return false;
}
``` 
This works.

Now, just for fun: what if we want to allow white spaces before and after the comma? This is rather easy
because `SkipWhiteSpaces` always returns true:

```csharp
static bool TryMatchFirstAndLast( ref ROSpanCharMatcher m )
{
    var savedHead = m.Head;
    if( (m.TryMatch( "First" ) || m.TryMatch( "Premier" ) || m.TryMatch( "Primero" ) || m.TryMatch( "Erste" ))
        && m.SkipWhiteSpaces() && m.TryMatch( ',' ) && m.SkipWhiteSpaces()
        && (m.TryMatch( "Last" ) || m.TryMatch( "Dernier" ) || m.TryMatch( "Última" ) || m.TryMatch( "Letzter" )) )
    {
        return m.SetSuccess();
    }
    m.Head = savedHead;
    return false;
}
``` 

To conclude, we now also allow C or JavaScript-like comments to appear around the comma (it can be /* ... */
or // ... to the end of line). This is where regular expressions show their limits.

```csharp
static bool TryMatchFirstAndLast( ref ROSpanCharMatcher m )
{
    var savedHead = m.Head;
    if( (m.TryMatch( "First" ) || m.TryMatch( "Premier" ) || m.TryMatch( "Primero" ) || m.TryMatch( "Erste" ))
        && m.SkipWhiteSpacesAndJSComments() && m.TryMatch( ',' ) && m.SkipWhiteSpacesAndJSComments()
        && (m.TryMatch( "Last" ) || m.TryMatch( "Dernier" ) || m.TryMatch( "Última" ) || m.TryMatch( "Letzter" )) )
    {
        return m.SetSuccess();
    }
    m.Head = savedHead;
    return false;
}
``` 

### `GetErrorMessage`: detailed errors and positions

The `ROSpanCharMatcher` has a `string GetErrorMessage()` method that formats the errors and expectations.
Using the `TryMatchFirstAndLast` above on invalid inputs give these errors:

<table>
<tr><td>Input</td><td>Error message</td></tr>
<tr><td>"" (empty string)</td>
<td>
<pre>
@1,1 - Expected: String 'First' (TryMatch)
             Or: String 'Premier' (TryMatch)
             Or: String 'Primero' (TryMatch)
             Or: String 'Erste' (TryMatch)
</pre>
</td></tr>
<tr><td>"Erste"</td>
<td>
<pre>
@1,6 - Expected: Character ',' (TryMatch)
</pre>
</td></tr>
<tr><td>"First,"</td>
<td>
<pre>
@1,7 - Expected: String 'Last' (TryMatch)
             Or: String 'Dernier' (TryMatch)
             Or: String 'Última' (TryMatch)
             Or: String 'Letzter' (TryMatch)
</pre>
</td></tr>
<tr><td>"Primero /*a comment*/ , "</td>
<td>
<pre>
@1,25 - Expected: String 'Last' (TryMatch)
              Or: String 'Dernier' (TryMatch)
              Or: String 'Última' (TryMatch)
              Or: String 'Letzter' (TryMatch)
</pre>
</td></tr>
</table>

The line, column of the errors are displayed and the expectation is followed by the name of the method in parentheses.

### `OpenExpectations` samples

Using `OpenExpectations` creates "a depth" in the matching and structure the error message:

```csharp 
static bool TryMatchFirstAndLastWithExpectation( ref ROSpanCharMatcher m )
{
    var savedHead = m.Head;
    using( m.OpenExpectations( "First,Last (in English, French, Spanish or German)" ) )
    {
        if( (m.TryMatch( "First" ) || m.TryMatch( "Premier" ) || m.TryMatch( "Primero" ) || m.TryMatch( "Erste" ))
        && m.SkipWhiteSpacesAndJSComments() && m.TryMatch( ',' ) && m.SkipWhiteSpacesAndJSComments()
        && (m.TryMatch( "Last" ) || m.TryMatch( "Dernier" ) || m.TryMatch( "Última" ) || m.TryMatch( "Letzter" )) )
        {
            return m.SetSuccess();
        }
    }
    m.Head = savedHead;
    return false;
}
``` 
The error for the `"First,"` input becomes:
```
@1,1 - Expected: First,Last (in English, French, Spanish or German) (TryMatchFirstAndLastWithExpectation)
  @1,7 - Expected: String 'Last' (TryMatch)
               Or: String 'Dernier' (TryMatch)
               Or: String 'Última' (TryMatch)
               Or: String 'Letzter' (TryMatch)
```

The `ROSpanCharMatcher` offers a basic support for parsing JSON:

```csharp
/// <summary>
/// Tries to match a { "JSON" : "object" }, a ["JSON", "array"] or a terminal value (string, null, double, true or false) 
/// and any combination of them.
/// White spaces and JS comments (//... or /* ... */) are skipped.
/// </summary>
/// <param name="this">This <see cref="StringMatcher"/>.</param>
/// <param name="value">
/// A list of nullable objects (for array), a list of tuples (string,object?) for object or
/// a double, string, boolean or null (for null).
/// </param>
/// <returns>True on success, false on error.</returns>
public bool TryMatchAnyJSON( out object? value )
```

Matching the invalid input "[null,,]" gives the following error:

```
@1,1 - Expected: Any JSON token or object (TryMatchAnyJSON)
  @1,2 - Expected: JSON array values (TryMatchJSONArrayContent)
    @1,7 - Expected: Any JSON token or object (TryMatchAnyJSON)
                       String 'true' (TryMatch)
                 Or:   String 'false' (TryMatch)
                 Or:   JSON string or null (TryMatchJSONQuotedString)
                 Or:   Floating number (TryMatchDouble)
```

TODO: more complex JSON sample with an error inside.
