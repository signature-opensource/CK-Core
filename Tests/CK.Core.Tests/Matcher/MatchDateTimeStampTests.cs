using Shouldly;
using System;
using NUnit.Framework;
using System.Linq;

namespace CK.Core.Tests;


public class MatchDateTimeStampTests
{
    [Test]
    public void matching_FileNameUniqueTimeUtcFormat()
    {
        DateTime t = DateTime.UtcNow;
        var s = ("X" + t.ToString( FileUtil.FileNameUniqueTimeUtcFormat ) + "Y").AsSpan();

        s.TryMatch( 'X' ).ShouldBeTrue();
        DateTime parsed;
        s.TryMatchFileNameUniqueTimeUtcFormat( out parsed ).ShouldBeTrue();
        parsed.ShouldBe( t );
        s.TryMatch( 'Y' ).ShouldBeTrue();

        s = t.ToString( FileUtil.FileNameUniqueTimeUtcFormat ).Insert( 2, "X" ).AsSpan();
        s.TryMatchFileNameUniqueTimeUtcFormat( out _ ).ShouldBeFalse();
        s.TryMatchInteger( out int i ).ShouldBeTrue();
        i.ShouldBe( 20 );
    }

    [Test]
    public void matching_DateTimeStamp()
    {
        DateTimeStamp t = DateTimeStamp.UtcNow;
        CheckDateTimeStamp( t );
        CheckDateTimeStamp( new DateTimeStamp( t.TimeUtc, 67 ) );
    }

    private static void CheckDateTimeStamp( DateTimeStamp t )
    {
        string s = t.ToString();
        var m = new ROSpanCharMatcher( "X" + s + "Y" );
        m.TryMatch( 'X' ).ShouldBeTrue();
        DateTimeStamp parsed;
        m.TryMatchDateTimeStamp( out parsed ).ShouldBeTrue();
        parsed.ShouldBe( t );
        m.TryMatch( 'Y' ).ShouldBeTrue();

        m = new ROSpanCharMatcher( s.Insert( 2, "X" ) );
        m.TryMatchDateTimeStamp( out parsed ).ShouldBeFalse();
        m.TryMatchInteger( out int i ).ShouldBeTrue();
        i.ShouldBe( 20 );

        m = new ROSpanCharMatcher( s.Insert( s.Length - 2, "X" ) );
        m.TryMatchDateTimeStamp( out parsed ).ShouldBeFalse();
        m.Head.Length.ShouldBe( m.AllText.Length );

        m = new ROSpanCharMatcher( s.Insert( s.Length - 1, "X" ) );
        m.TryMatchDateTimeStamp( out parsed ).ShouldBeFalse();
        m.Head.Length.ShouldBe( m.AllText.Length );
    }

    [TestCase( "A", "@0-DateTimeStamp|@0--UTC time" )]
    [TestCase( "2021-12-21 14h37.18.5195853(3", "@0-DateTimeStamp|@29--Character ')'" )]
    [TestCase( "2021-12-21 14h37.18.5195853(X", "@0-DateTimeStamp|@28--A Byte" )]
    [TestCase( "2021-12-21 14h37.18.5195853(", "@0-DateTimeStamp|@28--A Byte" )]
    public void match_methods_must_set_an_error( string s, string errors )
    {
        var m = new ROSpanCharMatcher( s );
        m.TryMatchDateTimeStamp( out _ ).ShouldBeFalse();
        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Select( e => $"@{e.Pos}{new string( '-', e.Depth + 1 )}{e.Expectation}" ).Concatenate( '|' ).ShouldBe( errors );
        m.Head.Length.ShouldBe( m.AllText.Length );
    }

}
