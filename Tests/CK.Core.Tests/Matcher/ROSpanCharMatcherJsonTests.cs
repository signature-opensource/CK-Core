using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using System.Diagnostics;
using System;

namespace CK.Core.Tests;

[TestFixture]
public class ROSpanCharMatcherJsonTests
{
    [Test]
    public void match_Json_objects()
    {
        {
            var j = @"{""A"":1,""B"":2}";
            var m = new ROSpanCharMatcher( j );
            m.TryMatchAnyJSON( out object? o ).ShouldBeTrue();
            var list = o as List<(string Name, object Value)>;
            Debug.Assert( list != null );
            list.Select( k => k.Name + '|' + k.Value ).Concatenate().ShouldBe( "A|1, B|2" );
        }
        {
            var j = @"{ ""A"" : 1.0, ""B"" : 2 }";
            var m = new ROSpanCharMatcher( j );
            m.TryMatchAnyJSON( out object? o ).ShouldBeTrue();
            var list = o as List<(string Name, object Value)>;
            Debug.Assert( list != null );
            list.Select( k => k.Name + '|' + k.Value ).Concatenate().ShouldBe( "A|1, B|2" );
        }
        {
            var j = @"{ ""A"" : [ ""a"" , 3 , null , 6], ""B"" : [ 2, 3, ""XX"" ] }";
            var m = new ROSpanCharMatcher( j );
            m.TryMatchAnyJSON( out object? o ).ShouldBeTrue();
            var list = o as List<(string Name, object Value)>;
            Debug.Assert( list != null );
            list.Select( k => k.Name
                              + '|'
                              + ((List<object?>)k.Value).Select( v => v?.ToString() ).Concatenate( "+" ) )
                .Concatenate().ShouldBe( "A|a+3++6, B|2+3+XX" );
        }
    }

    [Test]
    public void match_Json_empty_array_or_objects()
    {
        {
            var j = @"{}";
            var m = new ROSpanCharMatcher( j );
            m.TryMatchAnyJSON( out object? o ).ShouldBeTrue();
            var list = o as List<(string Name, object Value)>;
            Debug.Assert( list != null );
            list.ShouldBeEmpty();
        }
        {
            var j = @"[]";
            var m = new ROSpanCharMatcher( j );
            m.TryMatchAnyJSON( out object? o ).ShouldBeTrue();
            var list = o as List<object?>;
            Debug.Assert( list != null );
            list.ShouldBeEmpty();
        }
    }

    [TestCase( "1.2" )]
    [TestCase( "/* ... */1.2// ..." )]
    [TestCase( "1.2/* ..." )]
    [TestCase( "/* ... */1.2// ..." )]
    [TestCase( @"/*
*/  // ...

/* 3 */ 1.2     
//...
/*" )]
    public void match_Json_skips_JS_comments( string jsonWithComment )
    {
        var m = new ROSpanCharMatcher( jsonWithComment );
        m.TryMatchAnyJSON( out object? o ).ShouldBeTrue();
        o.ShouldBe( 1.2 );
    }


    [TestCase( "{", "@0-Any JSON token or object|@1--JSON object properties" )]
    [TestCase( "[", "@0-Any JSON token or object|@1--JSON array values" )]
    [TestCase( "[null,,]", "@0-Any JSON token or object|@1--JSON array values|@6---Any JSON token or object|@6----String 'true'|@6----String 'false'|@6----JSON string or null|@6----Floating number" )]
    public void TryMatchAnyJson_has_detailed_errors( string s, string errors )
    {
        var m = new ROSpanCharMatcher( s );
        m.TryMatchAnyJSON( out _ ).ShouldBeFalse();
        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Select( e => $"@{e.Pos}{new string( '-', e.Depth + 1 )}{e.Expectation}" ).Concatenate( '|' ).ShouldBe( errors );

        m.SetSuccess();
        m.SingleExpectationMode = true;
        m.TryMatchAnyJSON( out _ ).ShouldBeFalse();
        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Single().Expectation.ShouldBe( errors.Split( '|' )[0].Remove( 0, 3 ) );
    }
}
