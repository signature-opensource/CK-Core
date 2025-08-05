using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace CK.Core;

/// <summary>
/// Supports basic operations for "Match and Forward" pattern at the <see cref="ReadOnlySpan{T}"/> level.
/// This doesn't offer expectation support. For simple patterns this may be enough however to be able to
/// have a detailed reason of a match failure, the <see cref="ROSpanCharMatcher"/> should be used.  
/// </summary>
public static class ReadOnlySpanCharExtensions
{
    /// <summary>
    /// Forwards <paramref name="head"/> by <paramref name="length"/> even if actual head's length is shorter and
    /// returns the count of remaining characters (the new head's length).
    /// </summary>
    /// <param name="head">This head.</param>
    /// <param name="length">The length. Must be 0 or positive otherwise an ArgumentOutOfRangeException is thrown.</param>
    /// <returns>The remainder's head length.</returns>
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public static int SafeForward( this ref ReadOnlySpan<char> head, int length )
    {
        // Slice throws ArgumentOutOfRangeException if length is negative.
        head = head.Slice( Math.Min( length, head.Length ) );
        return head.Length;
    }

    /// <summary>
    /// Tries to match a specific string.
    /// </summary>
    /// <param name="head">This head.</param>
    /// <param name="value">The string value to match.</param>
    /// <param name="comparison">How to compare.</param>
    /// <returns>True on success (and the <paramref name="head"/> is forwarded), false otherwise (and the head is not moved).</returns>
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public static bool TryMatch( this ref ReadOnlySpan<char> head, ReadOnlySpan<char> value, StringComparison comparison = StringComparison.Ordinal )
    {
        if( head.StartsWith( value, comparison ) )
        {
            head = head.Slice( value.Length );
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to match a character.
    /// </summary>
    /// <param name="head">This head.</param>
    /// <param name="value">The character to match.</param>
    /// <param name="comparison">How to compare.</param>
    /// <returns>True on success (and the <paramref name="head"/> is forwarded), false otherwise (and the head is not moved).</returns>
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public static bool TryMatch( this ref ReadOnlySpan<char> head, char value, StringComparison comparison )
    {
        if( head.StartsWith( MemoryMarshal.CreateReadOnlySpan( ref value, 1 ), comparison ) )
        {
            head = head.Slice( 1 );
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to match a character.
    /// </summary>
    /// <param name="head">This head.</param>
    /// <param name="value">The character to match.</param>
    /// <returns>True on success (and the <paramref name="head"/> is forwarded), false otherwise (and the head is not moved).</returns>
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public static bool TryMatch( this ref ReadOnlySpan<char> head, char value )
    {
        if( head.Length > 0 && head[0] == value )
        {
            head = head.Slice( 1 );
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to skip a sequence of white spaces.
    /// Using <paramref name="minCount"/> = 0 is the same as calling <see cref="SkipWhiteSpaces(ref ReadOnlySpan{char})"/>.
    /// </summary>
    /// <param name="head">The head.</param>
    /// <param name="minCount">Minimal number of white spaces to skip.</param>
    /// <returns>True on success (and the <paramref name="head"/> is forwarded), false otherwise (and the head is not moved).</returns>
    public static bool TrySkipWhiteSpaces( this ref ReadOnlySpan<char> head, int minCount = 1 )
    {
        int i = 0;
        int len = head.Length;
        while( len != 0 && char.IsWhiteSpace( head[i] ) ) { ++i; --len; }
        if( i >= minCount )
        {
            head = head.Slice( i );
            return true;
        }
        return false;
    }

    /// <summary>
    /// Skips any number of white spaces (including none) and always returns true.
    /// </summary>
    /// <param name="head">The head.</param>
    /// <returns>Always true.</returns>
    public static bool SkipWhiteSpaces( this ref ReadOnlySpan<char> head )
    {
        int i = 0;
        int len = head.Length;
        while( len != 0 && char.IsWhiteSpace( head[i] ) ) { ++i; --len; }
        head = head.Slice( i );
        return true;
    }

    /// <summary>
    /// Tries to skip a sequence of characters for which <paramref name="predicate"/> returns true.
    /// Use <paramref name="minCount"/> = 0 to skip any number of characters (including none) and always returns true.
    /// </summary>
    /// <param name="head">The head.</param>
    /// <param name="predicate">The predicate to match.</param>
    /// <param name="minCount">Minimal number of characters that must be skipped.</param>
    /// <returns>True on success (and the <paramref name="head"/> is forwarded), false otherwise (and the head is not moved).</returns>
    public static bool TrySkip( this ref ReadOnlySpan<char> head, Func<char, bool> predicate, int minCount = 1 )
    {
        int i = 0;
        int len = head.Length;
        while( len != 0 && predicate( head[i] ) ) { ++i; --len; }
        if( i >= minCount )
        {
            head = head.Slice( i );
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to skip a sequence of characters.
    /// Use <paramref name="minCount"/> = 0 to skip any number of characters (including none) and always returns true.
    /// </summary>
    /// <param name="head">The head.</param>
    /// <param name="values">The values to skip.</param>
    /// <param name="minCount">Minimal number of characters that must be skipped.</param>
    /// <returns>True on success (and the <paramref name="head"/> is forwarded), false otherwise (and the head is not moved).</returns>
    public static bool TrySkip( this ref ReadOnlySpan<char> head, SearchValues<char> values, int minCount = 1 )
    {
        int i = 0;
        int len = head.Length;
        while( len != 0 && values.Contains( head[i] ) ) { ++i; --len; }
        if( i >= minCount )
        {
            head = head.Slice( i );
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to skip a sequence of decimal digits (0-9).
    /// Use <paramref name="minCount"/> = 0 to skip any number of characters (including none) and always returns true.
    /// </summary>
    /// <param name="head">The head.</param>
    /// <param name="minCount">Minimal number of decimal digits to skip.</param>
    /// <returns>True on success (and the <paramref name="head"/> is forwarded), false otherwise (and the head is not moved).</returns>
    public static bool TrySkipDigits( this ref ReadOnlySpan<char> head, int minCount = 1 )
    {
        int i = 0;
        int len = head.Length;
        char c;
        while( len != 0 && (c = head[i]) >= '0' && c <= '9' ) { ++i; --len; }
        if( i >= minCount )
        {
            head = head.Slice( i );
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to match a Guid.
    /// </summary>
    /// <remarks>
    /// Any of the 5 forms of Guid can be matched:
    /// <list type="table">
    /// <item><term>N</term><description>00000000000000000000000000000000</description></item>
    /// <item><term>D</term><description>00000000-0000-0000-0000-000000000000</description></item>
    /// <item><term>B</term><description>{00000000-0000-0000-0000-000000000000}</description></item>
    /// <item><term>P</term><description>(00000000-0000-0000-0000-000000000000)</description></item>
    /// <item><term>X</term><description>{0x00000000,0x0000,0x0000,{0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00}}</description></item>
    /// </list>
    /// </remarks>
    /// <param name="head">This head.</param>
    /// <param name="id">The result Guid. <see cref="Guid.Empty"/> on failure.</param>
    /// <returns>True on success (and the <paramref name="head"/> is forwarded), false otherwise (and the head is not moved).</returns>
    public static bool TryMatchGuid( this ref ReadOnlySpan<char> head, out Guid id )
    {
        id = Guid.Empty;
        if( head.Length < 32 ) return false;
        if( head[0] == '{' )
        {
            // Form "B" or "X".
            if( head.Length < 38 ) return false;
            if( head[37] == '}' )
            {
                // The "B" form.
                if( Guid.TryParseExact( head.Slice( 0, 38 ), "B", out id ) )
                {
                    head = head.Slice( 38 );
                    return true;
                }
                return false;
            }
            // The "X" form.
            if( head.Length >= 68 && Guid.TryParseExact( head.Slice( 0, 68 ), "X", out id ) )
            {
                head = head.Slice( 68 );
                return true;
            }
            return false;
        }
        if( head[0] == '(' )
        {
            // Can only be the "P" form.
            if( head.Length >= 38 && Guid.TryParseExact( head.Slice( 0, 38 ), "P", out id ) )
            {
                head = head.Slice( 38 );
                return true;
            }
            return false;
        }
        if( head[0].HexDigitValue() >= 0 )
        {
            // The "N" or "D" form.
            if( head.Length >= 36 && head[8] == '-' )
            {
                // The ""D" form.
                if( Guid.TryParseExact( head.Slice( 0, 36 ), "D", out id ) )
                {
                    head = head.Slice( 36 );
                    return true;
                }
                return false;
            }
            if( Guid.TryParseExact( head.Slice( 0, 32 ), "N", out id ) )
            {
                head = head.Slice( 32 );
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Tries to match a boolean "true" or "false" (case insensitive).
    /// </summary>
    /// <param name="head">This head.</param>
    /// <param name="b">The result boolean. False on failure.</param>
    /// <returns>True on success (and the <paramref name="head"/> is forwarded), false otherwise (and the head is not moved).</returns>
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

    // IBinaryIntegerParseAndFormatInfo<TSelf> is internal... :-(
    // We combine IsSigned and MaxHexDigitCount here.
    static int GetMaxHexDigitCount<T>() where T : IBinaryInteger<T>
    {
        if( typeof( T ) == typeof( int ) ) return ~9;
        if( typeof( T ) == typeof( uint ) ) return 8;
        if( typeof( T ) == typeof( long ) ) return ~17;
        if( typeof( T ) == typeof( ulong ) ) return 16;
        if( typeof( T ) == typeof( byte ) ) return 2;
        if( typeof( T ) == typeof( sbyte ) ) return ~3;
        if( typeof( T ) == typeof( char ) ) return 4;
        if( typeof( T ) == typeof( short ) ) return ~5;
        if( typeof( T ) == typeof( ushort ) ) return 4;
        if( typeof( T ) == typeof( Int128 ) ) return ~33;
        if( typeof( T ) == typeof( UInt128 ) ) return 32;
        return Throw.NotSupportedException<int>();
    }

    // From https://source.dot.net/#System.Private.CoreLib/src/libraries/Common/src/System/Number.NumberBuffer.cs,13
    // - For IFloatingPoint<T>.
    //   The additional byte, per length, is not for the terminating null in our case but for the
    //   optional leading '-'.
    // This is... a lot... And the reason is not obvious.
    // See for instance: https://stackoverflow.com/questions/1701055/what-is-the-maximum-length-in-chars-needed-to-represent-any-double-value
    // This is "safe" as we use the same limits as the .Net parser algorithm... And this is a max (above which we don't call TryParse).
    // If the actual value cannot be parsed, it cannot and everything is fine.
    //
    internal const int DoubleNumberBufferLength = 767 + 1 + 1;  // 767 for the longest input + 1 for rounding: 4.9406564584124654E-324
    internal const int SingleNumberBufferLength = 112 + 1 + 1;  // 112 for the longest input + 1 for rounding: 1.40129846E-45
    internal const int DecimalNumberBufferLength = 29 + 1 + 1;  // 29 for the longest input + 1 for rounding
    internal const int HalfNumberBufferLength = 21 + 1 + 1; // 21 for the longest input + 1 for rounding: 0.000122010707855224609375
    static int GetMaxFloatingCharCount<T>() where T : IFloatingPoint<T>
    {
        if( typeof( T ) == typeof( double ) ) return DoubleNumberBufferLength;
        if( typeof( T ) == typeof( float ) ) return SingleNumberBufferLength;
        if( typeof( T ) == typeof( decimal ) ) return DecimalNumberBufferLength;
        if( typeof( T ) == typeof( Half ) ) return HalfNumberBufferLength;
        return Throw.NotSupportedException<int>();
    }

    // - For integers.
    //   3 for the longest input: 255.
    internal const int UInt8NumberBufferLength = 3;
    //   4 for the longest input: -128.
    internal const int Int8NumberBufferLength = ~4;
    //   5 for the longest input: 32767.
    internal const int UInt16NumberBufferLength = 5;
    //   6 for the longest input: -32768.
    internal const int Int16NumberBufferLength = ~6;
    //   10 for the longest input: 4294967295.
    internal const int UInt32NumberBufferLength = 10;
    //   11 for the longest input: -2147483648.
    internal const int Int32NumberBufferLength = ~11;
    //   20 for the longest input: 18446744073709551615.
    internal const int UInt64NumberBufferLength = 20;
    //   20 for the longest input: -9223372036854775807.
    internal const int Int64NumberBufferLength = ~20;
    //   39 for the longest input: 340282366920938463463374607431768211455.
    internal const int UInt128NumberBufferLength = 39;
    //   40 for the longest input: -170141183460469231731687303715884105728.
    internal const int Int128NumberBufferLength = ~40;
    static int GetMaxIntegerCharCount<T>() where T : IBinaryInteger<T>
    {
        if( typeof( T ) == typeof( int ) ) return Int32NumberBufferLength;
        if( typeof( T ) == typeof( long ) ) return Int64NumberBufferLength;
        if( typeof( T ) == typeof( uint ) ) return UInt32NumberBufferLength;
        if( typeof( T ) == typeof( ulong ) ) return UInt64NumberBufferLength;
        if( typeof( T ) == typeof( short ) ) return Int16NumberBufferLength;
        if( typeof( T ) == typeof( ushort ) ) return UInt16NumberBufferLength;
        if( typeof( T ) == typeof( byte ) ) return UInt8NumberBufferLength;
        if( typeof( T ) == typeof( sbyte ) ) return Int8NumberBufferLength;
        if( typeof( T ) == typeof( Int128 ) ) return Int128NumberBufferLength;
        if( typeof( T ) == typeof( UInt128 ) ) return UInt128NumberBufferLength;
        return Throw.NotSupportedException<int>();
    }

    /// <summary>
    /// Tries to skip a sequence of hexadecimal digits (0-9, a-f, A-F).
    /// Use <paramref name="minCount"/> = 0 to skip any number of characters (including none) and always returns true.
    /// </summary>
    /// <param name="head">The head.</param>
    /// <param name="minCount">Minimal number of decimal digits to skip.</param>
    /// <returns>True on success (and the <paramref name="head"/> is forwarded), false otherwise (and the head is not moved).</returns>
    public static bool TrySkipHexDigits( this ref ReadOnlySpan<char> head, int minCount = 1 )
    {
        int i = 0;
        int len = head.Length;
        while( len != 0 && char.IsAsciiHexDigit( head[i] )) { ++i; --len; }
        if( i >= minCount )
        {
            head = head.Slice( i );
            return true;
        }
        return false;
    }

    /// <inheritdoc cref="TryMatchHexNumber{T}(ref ReadOnlySpan{char}, out T, bool)"/>.
    /// <remarks>
    /// Specific implementation for <see cref="char"/>.
    /// </remarks>
    public static bool TryMatchHexNumber( this ref ReadOnlySpan<char> head, out char value, bool allowLessDigits = false )
    {
        if( head.IsEmpty
            || (!allowLessDigits && head.Length < 4)
            || !char.IsAsciiHexDigit( head[0] ) )
        {
            value = default;
            return false;
        }
        int len;
        if( allowLessDigits )
        {
            len = 1;
            while( head.Length > len && char.IsAsciiHexDigit( head[len] ) ) ++len;
        }
        else len = 4;
        if( ushort.TryParse( head.Slice( 0, len ), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var v ) )
        {
            value = (char)v;
            head.Slice( len );
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Tries to match an hexadecimal values of 1 to 16 '0'-'9', 'A'-'F' or 'a'-'f' (without '0x' prefix) digits and
    /// forward the head on success.
    /// <para>
    /// This applies to <see cref="int"/>, <see cref="long"/>, <see cref="sbyte"/>, <see cref="short"/>,
    /// <see cref="Int128"/> or their unsigned <see cref="uint"/>, <see cref="ulong"/>, <see cref="byte"/>, <see cref="ushort"/>,
    /// <see cref="UInt128"/> and <see cref="char"/>.
    /// </para>
    /// <para>
    /// Char is handled by a dedicated implementation because <see cref="INumberBase{TSelf}.TryParse(ReadOnlySpan{char}, NumberStyles, IFormatProvider?, out TSelf)"/>
    /// for char parses a... char (regardless of the provided NumberStyles).
    /// </para>
    /// </summary>
    /// <param name="head">This head.</param>
    /// <param name="value">Resulting value on success.</param>
    /// <param name="allowLessDigits">
    /// By default, even a <see cref="UInt128"/> matches 'F' (or 'f') with a result of 15.
    /// When true, the exact count of hexadecimal digits that <typeparamref name="T"/> requires must be found
    /// (for a UInt128, it is 32 characters).
    /// </param>
    /// <returns>True on success (and the <paramref name="head"/> is forwarded), false otherwise (and the head is not moved).</returns>
    public static bool TryMatchHexNumber<T>( this ref ReadOnlySpan<char> head, [MaybeNullWhen(false)]out T value, bool allowLessDigits = true )
        where T : IBinaryInteger<T>
    {
        if( head.IsEmpty )
        {
            value = default;
            return false;
        }
        // The first digit (or the 2 first digits for signed) are handled out of the loop.  
        int len = 0;
        if( head[0] == '-' )
        {
            len = 1;
            if( head.Length == 1 || !char.IsAsciiHexDigit( head[1] ) )
            {
                value = default;
                return false;
            }
            len = 2;
        }
        else
        {
            if( !char.IsAsciiHexDigit( head[0] ) )
            {
                value = default;
                return false;
            }
            len = 1;
        }
        int maxCount = GetMaxHexDigitCount<T>();
        if( maxCount > 0 )
        {
            // T is an unsigned.
            if( len == 2 ) // We found a -X.
            {
                value = default;
                return false;
            }
        }
        else
        {
            // T is a signed type.
            maxCount = ~maxCount;
        }
        for( ; len < maxCount; ++len )
        {
            if( len >= head.Length || !char.IsAsciiHexDigit( head[len] ) )
            {
                if( !allowLessDigits )
                {
                    value = default;
                    return false;
                }
                break;
            }
        }
        if( T.TryParse( head.Slice( 0, len ), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value ) )
        {
            head = head.Slice( len );
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Tries to skip a floating number value.
    /// This skips a pattern like the regular expression "^-?[0-9]+(\.[0-9]+)?((e|E)(\+|-)?[0-9]+)?".
    /// </summary>
    /// <param name="head">This head.</param>
    /// <returns>True on success (and the <paramref name="head"/> is forwarded), false otherwise (and the head is not moved).</returns>
    public static bool TrySkipFloatingNumber( this ref ReadOnlySpan<char> head )
    {
        if( head.Length == 0 ) return false;
        var h = head;
        if( h[0] == '-' ) h = h.Slice( 1 );
        if( !h.TrySkipDigits( 1 ) ) return false;
        if( h.Length > 0 )
        {
            if( h[0] == '.' )
            {
                h = h.Slice( 1 );
                if( !h.TrySkipDigits( 1 ) ) return false;
            }
            if( h.Length != 0 && (h[0] == 'e' || h[0] == 'E') )
            {
                h = h.Slice( h.Length > 1 && (h[1] == '-' || h[1] == '+') ? 2 : 1 );
                if( !h.TrySkipDigits( 1 ) ) return false;
            }
        }
        head = head.Slice( head.Length - h.Length );
        return true;
    }

    [Obsolete( "Use TryMatchFloatingNumber instead." )]
    public static bool TryMatchDouble( this ref ReadOnlySpan<char> head, out double value )
        => TryMatchFloatingNumber<double>( ref head, out value );

    /// <summary>
    /// Tries to match a <see cref="double"/>, <see cref="float"/>, <see cref="Half"/> or <see cref="decimal"/>.
    /// </summary>
    /// <param name="head">This head.</param>
    /// <param name="value">The result value.</param>
    /// <returns>True on success (and the <paramref name="head"/> is forwarded), false otherwise (and the head is not moved).</returns>
    public static bool TryMatchFloatingNumber<T>( this ref ReadOnlySpan<char> head, [MaybeNullWhen(false)]out T value )
        where T : IFloatingPoint<T>
    {
        var h = head;
        if( TrySkipFloatingNumber( ref h ) )
        {
            var len = head.Length - h.Length;
            if( len <= GetMaxFloatingCharCount<T>()
                && T.TryParse( head.Slice( 0, len ), NumberStyles.Float, CultureInfo.InvariantCulture, out value ) )
            {
                head = h;
                return true;
            }
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Tries to skip an integer value. This skips a pattern like the regular expression "^-?[0-9]+".
    /// </summary>
    /// <param name="head">This head.</param>
    /// <param name="allowMinus">False to accept only unsigned pattern (no leading '-').</param>
    /// <returns>True on success (and the <paramref name="head"/> is forwarded), false otherwise (and the head is not moved).</returns>
    public static bool TrySkipInteger( this ref ReadOnlySpan<char> head, bool allowMinus = true )
    {
        if( allowMinus ) head.TryMatch( '-' );
        return head.TrySkipDigits( 1 );
    }

    /// <summary>
    /// Tries to match the decimal representation of <see cref="int"/>, <see cref="long"/>, <see cref="sbyte"/>, <see cref="short"/>,
    /// <see cref="Int128"/> or their unsigned <see cref="uint"/>, <see cref="ulong"/>, <see cref="byte"/>, <see cref="ushort"/>,
    /// <see cref="UInt128"/>.
    /// </summary>
    /// <param name="head">This head.</param>
    /// <param name="value">The result value.</param>
    /// <returns>True on success (and the <paramref name="head"/> is forwarded), false otherwise (and the head is not moved).</returns>
    public static bool TryMatchInteger<T>( this ref ReadOnlySpan<char> head, [MaybeNullWhen( false )] out T value )
        where T : IBinaryInteger<T>
    {
        var h = head;
        if( TrySkipInteger( ref h ) )
        {
            var len = head.Length - h.Length;
            var maxLen = GetMaxIntegerCharCount<T>();
            if( maxLen > 0 )
            {
                // Unsigned!
                if( head[0] == '-' )
                {
                    value = default;
                    return false;
                }
            }
            else
            {
                maxLen = ~maxLen;
            }
            if( len <= maxLen
                && T.TryParse( head.Slice( 0, len ), NumberStyles.Integer, CultureInfo.InvariantCulture, out value ) )
            {
                head = h;
                return true;
            }
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Tries to skip a quoted string. This handles escaped \" and \\ but no other
    /// escaped characters: the string may be invalid regarding JSON string grammar (and
    /// for <see cref="TryMatchJsonQuotedString(ref ReadOnlySpan{char}, ref StringBuilder?)"/>).
    /// <para>
    /// See the string definition https://www.json.org/json-en.html.
    /// </para>
    /// </summary>
    /// <param name="head">This head.</param>
    /// <param name="allowNull">True to allow 'null' token.</param>
    /// <returns>True on success, false otherwise.</returns>
    public static bool TrySkipJsonQuotedString( this ref ReadOnlySpan<char> head, bool allowNull = false )
    {
        if( head.Length == 0 ) return false;
        if( head[0] != '"' )
        {
            return allowNull && TryMatch( ref head, "null" );
        }
        var h = head.Slice( 1 );
        for(; ; )
        {
            int idx = h.IndexOf( '"' );
            if( idx < 0 ) return false;
            int rIdx = idx - 1;
            while( rIdx >= 0 && h[rIdx] == '\\' ) rIdx--;
            int escapeCountPlusQuote = idx - rIdx;
            h = h.Slice( idx + 1 );
            if( (escapeCountPlusQuote & 1) == 1 )
            {
                head = h;
                return true;
            }
            // This quote is escaped. Skip it.
        }
    }

    /// <summary>
    /// Tries to match 'null' or a JSON quoted string.
    /// See <see cref="TryMatchJsonQuotedString(ref ReadOnlySpan{char}, ref StringBuilder?)"/>.
    /// <para>
    /// On 'null', this returns true and the <paramref name="destination"/> is untouched.
    /// </para>
    /// </summary>
    /// <param name="head">This head.</param>
    /// <param name="destination">
    /// The string builder into which the successfully evaluated content will be appended.
    /// Will be allocated only if needed (may be allocated on failure but will be empty).
    /// </param>
    /// <returns>True on success, false otherwise.</returns>
    public static bool TryMatchNullableJsonQuotedString( this ref ReadOnlySpan<char> head,
                                                        ref StringBuilder? destination )
    {
        if( head.Length == 0 ) return false;
        if( head[0] != '"' )
        {
            return TryMatch( ref head, "null" );
        }
        return TryMatchJsonQuotedString( ref head, ref destination );

    }

    /// <summary>
    /// Tries to match a JSON quoted string. All \uXXXX are evaluated, invalid escaped characters (like \' or \x) will fail,
    /// only \r, \n, \b, \t, \f, \uXXXX, \\, \/ and \" are valid and are evaluated (Note: ECMA-262 allows encoding U+000B as "\v",
    /// but ECMA-404 does not, so we handle it).
    /// See the string definition https://www.json.org/json-en.html.
    /// <para>
    /// On error, the <paramref name="destination"/> is unchanged.
    /// </para>
    /// <para>
    /// To allow the 'null' token, use <see cref="TryMatchNullableJsonQuotedString(ref ReadOnlySpan{char}, ref StringBuilder?)"/>.
    /// </para>
    /// </summary>
    /// <param name="head">This head.</param>
    /// <param name="destination">
    /// The string builder into which the successfully evaluated content will be appended.
    /// Will be allocated only if needed (may be allocated on failure but will be empty).
    /// </param>
    /// <returns>True on success, false otherwise.</returns>
    static bool TryMatchJsonQuotedString( ref ReadOnlySpan<char> head, ref StringBuilder? destination )
    {
        if( head.Length == 0 || head[0] != '"' )
        {
            return false;
        }
        // This restores the destination on error.
        static bool Error( int destinationStartIndex, StringBuilder? destination )
        {
            if( destinationStartIndex >= 0 )
            {
                Throw.DebugAssert( destination != null );
                destination.Length = destinationStartIndex;
            }
            return false;
        }

        int destinationStartIndex = -1;
        int i = 1;
        int len = head.Length - 1;
        while( len >= 0 )
        {
            if( len == 0 ) return Error( destinationStartIndex, destination );
            char c = head[i++];
            --len;
            if( c == '"' ) break;
            if( c == '\\' )
            {
                if( len == 0 ) return Error( destinationStartIndex, destination );
                if( destinationStartIndex == -1 )
                {
                    Throw.DebugAssert( i >= 2 );
                    destination ??= new StringBuilder( i + 254 );
                    destinationStartIndex = destination.Length;
                    destination.Append( head.Slice( 1, i - 2 ) );
                }
                switch( c = head[i++] )
                {
                    case 'r': c = '\r'; break;
                    case 'n': c = '\n'; break;
                    case 'b': c = '\b'; break;
                    case 't': c = '\t'; break;
                    case 'f': c = '\f'; break;
                    case 'v': c = '\v'; break; // Allowed by ECMA-262.
                    case 'u':
                    {
                        var h = head.Slice( i );
                        if( !h.TryMatchHexNumber( out char u, allowLessDigits: false ) )
                        {
                            return Error( destinationStartIndex, destination );
                        }
                        len -= 4;
                        i += 4;
                        c = (char)u;
                        break;
                    }
                    case '\\': // These are the only other valid escaped characters in JSON.
                    case '"':
                    case '/': break;
                    default:
                    {
                        return Error( destinationStartIndex, destination );
                    }
                }
            }
            if( destinationStartIndex >= 0 )
            {
                Throw.DebugAssert( destination != null );
                destination.Append( c );
            }
        }
        if( destinationStartIndex == -1 )
        {
            int sLen = i - 2;
            if( sLen > 0 )
            {
                destination ??= new StringBuilder( sLen );
                destination.Append( head.Slice( 1, sLen ) );
            }
        }
        head = head.Slice( i );
        return true;
    }

    /// <summary>
    /// Tries to match and evaluate a JSON quoted string.
    /// See <see cref="TryMatchJsonQuotedString(ref ReadOnlySpan{char}, ref StringBuilder?)"/>.
    /// <para>
    /// The head must not be on 'null'. Use <see cref="TryMatchNullableJsonQuotedString(ref ReadOnlySpan{char}, out string?)"/>
    /// to handle 'null' token.
    /// </para>
    /// </summary>
    /// <param name="head">This head.</param>
    /// <param name="result">The evaluated string on success, null otherwise.</param>
    /// <returns>True on success, false otherwise.</returns>
    public static bool TryMatchJsonQuotedString( this ref ReadOnlySpan<char> head, [NotNullWhen( true )] out string? result )
    {
        StringBuilder? b = null;
        if( TryMatchJsonQuotedString( ref head, ref b ) )
        {
            result = b?.ToString() ?? string.Empty;
            return true;
        }
        result = null;
        return false;
    }

    /// <summary>
    /// Tries to match a 'null' or a JSON quoted string and evaluate it.
    /// See <see cref="TryMatchJsonQuotedString(ref ReadOnlySpan{char}, ref StringBuilder?)"/>.
    /// </summary>
    /// <param name="head">This head.</param>
    /// <param name="result">The result string. Null on 'null' token.</param>
    /// <returns>True on success, false otherwise.</returns>
    public static bool TryMatchNullableJsonQuotedString( this ref ReadOnlySpan<char> head, out string? result )
    {
        StringBuilder? b = null;
        if( TryMatchJsonQuotedString( ref head, ref b ) )
        {
            result = b?.ToString() ?? string.Empty;
            return true;
        }
        result = null;
        return head.TryMatch( "null" );
    }

    /// <summary>
    /// Tries to skip a JSON terminal value: a "string", null, a number (double value), true or false.
    /// </summary>
    /// <param name="head">This head.</param>
    /// <returns>True on success, false otherwise.</returns>
    public static bool TrySkipJsonTerminalValue( this ref ReadOnlySpan<char> head )
    {
        return head.TrySkipJsonQuotedString( true )
                || head.TrySkipFloatingNumber()
                || head.TryMatch( "true" )
                || head.TryMatch( "false" );
    }

    /// <summary>
    /// Tries to skip a //.... or /* ... */ comment.
    /// Proper termination of comment (by a new line or the closing */) is not required: 
    /// a ending /*... is considered valid.
    /// </summary>
    /// <param name="head">This head.</param>
    /// <returns>True on success, false otherwise.</returns>
    public static bool TrySkipJSComment( this ref ReadOnlySpan<char> head )
    {
        if( head.Length < 2 || head[0] != '/' ) return false;
        if( head[1] == '/' )
        {
            int idx = head.IndexOf( '\n' ) + 1;
            if( idx == 0 ) idx = head.Length;
            head = head.Slice( idx );
            return true;
        }
        else if( head[1] == '*' )
        {
            int idx = head.IndexOf( "*/" ) + 2;
            if( idx == 1 ) idx = head.Length;
            head = head.Slice( idx );
            return true;
        }
        return false;
    }

    /// <summary>
    /// Skips any white spaces or JS comments (//... or /* ... */) and always returns true.
    /// Proper termination of comment (by a new line or the closing */) is not required: 
    /// a ending /*... is considered valid.
    /// </summary>
    /// <param name="head">This head.</param>
    /// <returns>Always true to ease composition.</returns>
    public static bool SkipWhiteSpacesAndJSComments( this ref ReadOnlySpan<char> head )
    {
        SkipWhiteSpaces( ref head );
        while( TrySkipJSComment( ref head ) ) SkipWhiteSpaces( ref head );
        return true;
    }

}
