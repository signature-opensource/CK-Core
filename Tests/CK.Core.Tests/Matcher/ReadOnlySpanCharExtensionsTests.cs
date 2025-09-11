using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;

namespace CK.Core.Tests;

public class ReadOnlySpanCharExtensionsTests
{
    [TestCase( "||", false )]
    [TestCase( """|"|""", false )]
    [TestCase( """|""|""", true )]
    [TestCase( """|"\"|""", false )]
    [TestCase( """|"\\"|""", true )]
    [TestCase( """|"\\\""|""", true )]
    [TestCase( """|"\\\\"|""", true )]
    [TestCase( """|"\\\\\""|""", true )]
    [TestCase( """|"\\\\\"\"|""", false )]
    [TestCase( """|"ab"|""", true )]
    [TestCase( """|"a\"\u0254\"b"|""", true )]
    [TestCase( "|null|", true )]
    public void TrySkipJsonQuotedString_with_allowNull_tests( string s, bool success )
    {
        var head = s.AsSpan();
        head.TryMatch( '|' ).ShouldBeTrue();
        head.TrySkipJsonQuotedString( allowNull: true ).ShouldBe( success );
        if( success )
        {
            head[0].ShouldBe( '|' );
            head.Length.ShouldBe( 1 );
        }
        else
        {
            // Head has not been forwarded.
            (s.Length - head.Length).ShouldBe( 1 );
        }
    }

    [TestCase( "", "" )]
    [TestCase( "a", "a" )]
    [TestCase( """\t\r\n\u0020\v""", "\t\r\n\u0020\v" )]
    public void TryMatchJsonQuotedString( string jsonContent, string expected )
    {
        var s = '"' + jsonContent + '"';
        var head = s.AsSpan();
        head.TryMatchJsonQuotedString( out var parsed ).ShouldBeTrue();
        head.Length.ShouldBe( 0 );
        parsed.ShouldBe( expected );
    }

    [TestCase( "\"\"", "" )]
    [TestCase( "null", null )]
    [TestCase( "\"a\"", "a" )]
    public void TryMatchNullableJsonQuotedString( string json, string? expected )
    {
        var head = json.AsSpan();
        head.TryMatchNullableJsonQuotedString( out var parsed ).ShouldBeTrue();
        head.Length.ShouldBe( 0 );
        parsed.ShouldBe( expected );
    }

    [Test]
    public void hexadecimal_numbers_test()
    {
        // Hexadecimal string can be smaller than expected.
        byte.TryParse( "F", NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var parsedOneDigit ).ShouldBeTrue();
        parsedOneDigit.ShouldBe( 15 );
        byte.TryParse( "FF", NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var parsedTwoDigits ).ShouldBeTrue();
        parsedTwoDigits.ShouldBe( 255 );
        // But not grater (and this is where the match and forward pattern is useful...).
        byte.TryParse( "FFF", NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var _ ).ShouldBeFalse();


    }


    [TestCase( 0 )]
    [TestCase( 42 )]
    [TestCase( 3712 )]
    public void random_string_TryMatchJsonQuotedString_with_JavaScriptEncoderDefault( int seed )
    {
        var random = new Random( seed );
        var unicode = new UTF32Encoding( bigEndian: false, byteOrderMark: false, throwOnInvalidCharacters: true );
        for( int i = 0; i < 500_000; ++i )
        {
            var attempt = $"Seed {seed}, n°{i}";
            // A valid Unicode scalar value is in the range [ U+0000..U+D7FF ], inclusive or [ U+E000..U+10FFFF ], inclusive.
            // We generate 2 random UTF32 chars. The UTF16 string can obviously be longer and the Json encoded string
            // by the JavaScriptEncoder.Default can be even larger, but always valid.
            // (Note: interesting post here https://meta.stackoverflow.com/a/382779/190380)
            Span<int> ints = [random.Next( 0, 0xD7FF + 1 ), random.Next( 0xE000, 0x10FFFF + 1 )];
            Rune.IsValid( ints[0] ).ShouldBeTrue( /*$"{attempt}. False Rune.IsValid for '{ints[0]}'/'0x{ints[0]:X}'."*/ );
            Rune.IsValid( ints[1] ).ShouldBeTrue( /*$"{attempt}. False Rune.IsValid for '{ints[1]}'/'0x{ints[1]:X}'."*/ );

            Span<byte> bytes = MemoryMarshal.AsBytes( ints );
            bytes.Length.ShouldBe( 8 );
            var randomString = unicode.GetString( bytes );
            attempt += $": {randomString}";

            // The JavaScriptEncoder.Default secures any non Latin1 characters.
            // Using it enables a fast conversion to UTF8: all characters fit 7 bits.  
            var jsonString = '"' + JavaScriptEncoder.Default.Encode( randomString ) + '"';
            //
            // High cost: 20x slower tests!
            // jsonCompliantString.ShouldAllBe( c => c <= 0x7F, attempt );
            //
            jsonString.All( c => c <= 0x7F ).ShouldBeTrue( attempt ); 
            var head = jsonString.AsSpan();
            head.TryMatchJsonQuotedString( out var decoded ).ShouldBeTrue( attempt );
            head.Length.ShouldBe( 0, attempt );
            decoded.ShouldBe( randomString, attempt );

            head = jsonString.AsSpan();
            head.TrySkipJsonQuotedString().ShouldBeTrue( attempt );
            head.Length.ShouldBe( 0, attempt );
        }
    }

    [Test]
    public void TryMatchInteger_out_of_ranges_test()
    {
        var negative = "-1".AsSpan();
        negative.TryMatchInteger( out byte _ ).ShouldBeFalse();
        negative.Length.ShouldBe( 2 );
        negative.TryMatchInteger( out ushort _ ).ShouldBeFalse();
        negative.Length.ShouldBe( 2 );
        negative.TryMatchInteger( out uint _ ).ShouldBeFalse();
        negative.Length.ShouldBe( 2 );
        negative.TryMatchInteger( out ulong _ ).ShouldBeFalse();
        negative.Length.ShouldBe( 2 );
        negative.TryMatchInteger( out UInt128 _ ).ShouldBeFalse();
        negative.Length.ShouldBe( 2 );
        negative.TryMatchInteger( out sbyte _ ).ShouldBeTrue();
        negative.Length.ShouldBe( 0 );

        var maxInt = int.MaxValue.ToString().AsSpan();
        maxInt.TryMatchInteger( out sbyte _ ).ShouldBeFalse();
        maxInt.TryMatchInteger( out byte _ ).ShouldBeFalse();
        maxInt.TryMatchInteger( out short _ ).ShouldBeFalse();
        maxInt.TryMatchInteger( out ushort _ ).ShouldBeFalse();
        maxInt.TryMatchInteger( out int _ ).ShouldBeTrue();
        maxInt.Length.ShouldBe( 0 );

        var minInt = int.MinValue.ToString().AsSpan();
        minInt.TryMatchInteger( out sbyte _ ).ShouldBeFalse();
        minInt.TryMatchInteger( out byte _ ).ShouldBeFalse();
        minInt.TryMatchInteger( out short _ ).ShouldBeFalse();
        minInt.TryMatchInteger( out ushort _ ).ShouldBeFalse();
        minInt.TryMatchInteger( out int _ ).ShouldBeTrue();
        maxInt.Length.ShouldBe( 0 );

        var maxInt128 = Int128.MaxValue.ToString().AsSpan();
        maxInt128.TryMatchInteger( out sbyte _ ).ShouldBeFalse();
        maxInt128.TryMatchInteger( out byte _ ).ShouldBeFalse();
        maxInt128.TryMatchInteger( out short _ ).ShouldBeFalse();
        maxInt128.TryMatchInteger( out ushort _ ).ShouldBeFalse();
        maxInt128.TryMatchInteger( out uint _ ).ShouldBeFalse();
        maxInt128.TryMatchInteger( out int _ ).ShouldBeFalse();
        maxInt128.TryMatchInteger( out ulong _ ).ShouldBeFalse();
        maxInt128.TryMatchInteger( out long _ ).ShouldBeFalse();
        maxInt128.TryMatchInteger( out Int128 _ ).ShouldBeTrue();
        maxInt128.Length.ShouldBe( 0 );

        var minInt128 = Int128.MinValue.ToString().AsSpan();
        minInt128.TryMatchInteger( out sbyte _ ).ShouldBeFalse();
        minInt128.TryMatchInteger( out byte _ ).ShouldBeFalse();
        minInt128.TryMatchInteger( out short _ ).ShouldBeFalse();
        minInt128.TryMatchInteger( out ushort _ ).ShouldBeFalse();
        minInt128.TryMatchInteger( out uint _ ).ShouldBeFalse();
        minInt128.TryMatchInteger( out int _ ).ShouldBeFalse();
        minInt128.TryMatchInteger( out ulong _ ).ShouldBeFalse();
        minInt128.TryMatchInteger( out long _ ).ShouldBeFalse();
        minInt128.TryMatchInteger( out Int128 _ ).ShouldBeTrue();
        minInt128.Length.ShouldBe( 0 );
    }



    [TestCase( byte.MaxValue )]
    [TestCase( byte.MinValue )]
    [TestCase( 42 )]
    public void TryMatchInteger_byte_test( byte v )
    {
        CheckBinaryInteger( v );
    }

    [TestCase( sbyte.MaxValue )]
    [TestCase( sbyte.MinValue )]
    [TestCase( 0 )]
    [TestCase( -42 )]
    public void TryMatchInteger_sbyte_test( sbyte v )
    {
        CheckBinaryInteger( v );
    }

    [Test] // NUnit fails to handle uint cast.
    public void TryMatchInteger_uint_test()
    {
        CheckBinaryInteger( uint.MaxValue );
        CheckBinaryInteger( uint.MinValue );
        CheckBinaryInteger( (uint)0 );
        CheckBinaryInteger( (uint)35416416 );
    }

    [Test] // Int128.MaxValue is not a constant.
    public void TryMatchInteger_Int128_test()
    {
        CheckBinaryInteger( Int128.MaxValue );
        CheckBinaryInteger( Int128.MinValue );
        CheckBinaryInteger( Int128.Zero );
        CheckBinaryInteger( (Int128)35416416 );
        CheckBinaryInteger( (Int128)(-676716616416) );
    }

    [Test]
    public void TryMatchInteger_UInt128_test()
    {
        CheckBinaryInteger( UInt128.MaxValue );
        CheckBinaryInteger( UInt128.MinValue );
        CheckBinaryInteger( UInt128.Zero );
        CheckBinaryInteger( (UInt128)35416415661616 );
    }

    [TestCase( int.MaxValue )]
    [TestCase( int.MinValue )]
    [TestCase( 0 )]
    [TestCase( -3712 )]
    public void TryMatchInteger_int_test( int v )
    {
        CheckBinaryInteger( v );
    }

    static void CheckBinaryInteger<T>( T v ) where T : IBinaryInteger<T>
    {
        var head = v.ToString( null, CultureInfo.InvariantCulture ).AsSpan();
        CheckBinaryInteger( head, v );
    }

    static void CheckBinaryInteger<T>( ReadOnlySpan<char> head, T v ) where T : IBinaryInteger<T>
    {
        head.TryMatchInteger<T>( out var backV ).ShouldBeTrue();
        backV.ShouldBe( v );
        head.Length.ShouldBe( 0 );
    }

}
