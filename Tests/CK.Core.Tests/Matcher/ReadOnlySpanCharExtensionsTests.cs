using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using static CK.Core.Tests.TypeExtensionTests;

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
    [TestCase( """\t\r\n\u0000\v""", "\t\r\n\u0000\v" )]
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

}
