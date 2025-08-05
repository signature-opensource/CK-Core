using System;
using System.Linq;
using Shouldly;
using NUnit.Framework;

namespace CK.Core.Tests;

[TestFixture]
public class ROSpanCharMatcherTests
{
    [Test]
    public void simple_char_matching()
    {
        string s = "ABCD";
        var m = new ROSpanCharMatcher( s );
        m.TryMatch( 'a' ).ShouldBeFalse();
        m.HasError.ShouldBeTrue();
        m.TryMatch( 'A' ).ShouldBeTrue();
        m.HasError.ShouldBeFalse();
        m.TryMatch( 'A' ).ShouldBeFalse();
        m.HasError.ShouldBeTrue();
        m.TryMatch( 'B' ).ShouldBeTrue();
        m.HasError.ShouldBeFalse();
        m.TryMatch( 'C' ).ShouldBeTrue();
        m.HasError.ShouldBeFalse();

        m.TryMatch( 'D' ).ShouldBeTrue();
        m.HasError.ShouldBeFalse();
        m.GetRawErrors().ShouldBeEmpty();

        m.TryMatch( 'D' ).ShouldBeFalse();
        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Single().Expectation.ShouldBe( "Character 'D'" );
    }

    [TestCase( "abcdef", 0xABCDEFUL )]
    [TestCase( "12abcdef", 0x12ABCDEFUL )]
    [TestCase( "12abcdef12abcdef", 0x12abcdef12abcdefUL )]
    [TestCase( "00000000FFFFFFFF", 0xFFFFFFFFUL )]
    [TestCase( "FFFFFFFFFFFFFFFF", 0xFFFFFFFFFFFFFFFFUL )]
    public void matching_hex_number( string s, ulong v )
    {
        var m = new ROSpanCharMatcher( s );
        m.TryMatchHexNumber( out ulong value ).ShouldBeTrue();
        value.ShouldBe( v );
        m.Head.Length.ShouldBe( 0 );
    }

    [TestCase( "0|", 0x0UL, '|' )]
    [TestCase( "AG", 0xAUL, 'G' )]
    [TestCase( "cX", 0xCUL, 'X' )]
    public void matching_hex_number_one_digit( string s, ulong v, char end )
    {
        var m = new ROSpanCharMatcher( s );
        m.TryMatchHexNumber( out ulong value ).ShouldBeTrue();
        value.ShouldBe( v );
        m.HasError.ShouldBeFalse();
        m.Head.Length.ShouldBePositive();
        m.Head[0].ShouldBe( end );
    }

    [TestCase( "not a hex." )]
    public void matching_hex_number_failures( string s )
    {
        var m = new ROSpanCharMatcher( s );
        m.TryMatchHexNumber( out ulong value ).ShouldBeFalse();
        m.Head.Length.ShouldBe( s.Length );
        m.GetRawErrors().Single().Expectation.ShouldBe( "Hexadecimal number" );
        m.GetRawErrors().Single().CallerName.ShouldBe( "TryMatchHexNumber" );
    }

    [Test]
    public void matching_texts_and_whitespaces()
    {
        string s = " AB  \t\r C";
        var m = new ROSpanCharMatcher( s );

        m.TryMatch( "A" ).ShouldBeFalse();
        m.TrySkipWhiteSpaces().ShouldBeTrue();
        m.TryMatch( "A" ).ShouldBeTrue();
        m.TryMatch( "B" ).ShouldBeTrue();

        m.TrySkipWhiteSpaces( 6 ).ShouldBeFalse();
        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Single().Expectation.ShouldBe( "At least 6 white space(s)" );

        m.TrySkipWhiteSpaces( 5 ).ShouldBeTrue();
        m.HasError.ShouldBeFalse();
        m.GetRawErrors().ShouldBeEmpty();

        m.TrySkipWhiteSpaces().ShouldBeFalse();
        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Single().Expectation.ShouldBe( "At least one white space" );

        m.TryMatch( "c", StringComparison.OrdinalIgnoreCase ).ShouldBeTrue();
        m.Head.IsEmpty.ShouldBeTrue();

        m.TryMatch( "A" ).ShouldBeFalse();
        m.TrySkipWhiteSpaces().ShouldBeFalse();
    }

    [Test]
    public void matching_integers()
    {
        var m = new ROSpanCharMatcher( "X3712Y" );
        m.TryMatch( 'X' ).ShouldBeTrue();
        m.TryMatchInteger( out int i ).ShouldBeTrue();
        i.ShouldBe( 3712 );
        m.TryMatch( 'Y' ).ShouldBeTrue();
    }

    [Test]
    public void matching_digits()
    {
        var m = "X  012345678901234567890123456789  Y".AsSpan();
        m.TryMatch( 'X' ).ShouldBeTrue();
        m.SkipWhiteSpaces().ShouldBeTrue();
        m.TrySkipDigits( 0 ).ShouldBeTrue();
        m.SkipWhiteSpaces().ShouldBeTrue();
        m.TryMatch( 'Y' ).ShouldBeTrue();
    }

    delegate T ROSpanCharMatcherFunc<T>( ROSpanCharMatcher m );

    [Test]
    public void match_methods_must_set_an_error()
    {
        var m = new ROSpanCharMatcher( "~" );

        CheckMatchError( m, m => m.TryMatch( 'a' ), "Character 'a'" );
        CheckMatchError( m, m => m.TryMatchHexNumber( out _ ), "Hexadecimal number" );
        CheckMatchError( m, m => m.TryMatchHexNumber( out _ ), "Hexadecimal number" );
        CheckMatchError( m, m => m.TryMatchHexNumber( out _), "Hexadecimal number" );
        CheckMatchError( m, m => m.TryMatchInteger( out int _ ), "A Int32" );
        CheckMatchError( m, m => m.TryMatchInteger( out ulong _ ), "A UInt64" );
        CheckMatchError( m, m => m.TryMatchInteger( out sbyte _ ), "A SByte" );
        CheckMatchError( m, m => m.TryMatchGuid( out _ ), "Guid" );
        CheckMatchError( m, m => m.TryMatchFloatingNumber( out double _ ), "Floating number 'Double'" );
        CheckMatchError( m, m => m.TrySkipWhiteSpaces(), "At least one white space" );
    }

    static void CheckMatchError( ROSpanCharMatcher m, ROSpanCharMatcherFunc<bool> fail, string message )
    {
        int len = m.Head.Length;
        fail( m ).ShouldBeFalse();
        m.HasError.ShouldBeTrue();
        m.Head.Length.ShouldBe( len );
        m.GetRawErrors().Count().ShouldBe( 1 );
        m.GetRawErrors().Single().Expectation.ShouldBe( message );
        m.SetSuccess();
    }

    [TestCase( "0", 0 )]
    [TestCase( "2.3", 2.3 )]
    [TestCase( "255", 255 )]
    [TestCase( "9876978", 9876978 )]
    [TestCase( "-9876978", -9876978 )]
    [TestCase( "0.0", 0 )]
    [TestCase( "0.00", 0 )]
    [TestCase( "0.34", 0.34 )]
    [TestCase( "-4e+5", -4e5 )]
    [TestCase( "4E5", 4E5 )]
    [TestCase( "4.0E5", 4E5 )]
    [TestCase( "29380.34e98", 29380.34e98 )]
    [TestCase( "29380.34E98", 29380.34E98 )]
    [TestCase( "-80.34e-98", -80.34e-98 )]
    [TestCase( "-80.34E+98", -80.34E98 )]
    public void matching_double_values( string s, double d )
    {
        {
            var m = new ROSpanCharMatcher( "P" + s + "S" );
            m.TryMatch( 'P' ).ShouldBeTrue();
            m.TrySkipDouble().ShouldBeTrue();
            m.TryMatch( 'S' ).ShouldBeTrue();
            m.Head.IsEmpty.ShouldBeTrue();

            m.Head = m.AllText.Slice( 1 );
            m.TryMatchFloatingNumber( out double parsed ).ShouldBeTrue();
            parsed.ShouldBe( d, 1f );
            m.TryMatch( 'S' ).ShouldBeTrue();
            m.Head.IsEmpty.ShouldBeTrue();
        }
        {
            var m = new ROSpanCharMatcher( s );
            m.TrySkipDouble().ShouldBeTrue();
            m.Head.IsEmpty.ShouldBeTrue();

            m.Head = m.AllText;
            m.TryMatchFloatingNumber( out double parsed ).ShouldBeTrue();
            parsed.ShouldBe( d, 1f );
            m.Head.IsEmpty.ShouldBeTrue();
        }
    }

    [TestCase( "N" )]
    [TestCase( "D" )]
    [TestCase( "B" )]
    [TestCase( "P" )]
    [TestCase( "X" )]
    public void matching_the_5_forms_of_guid( string form )
    {
        var id = Guid.NewGuid();
        string sId = id.ToString( form );
        {
            string s = sId;
            var m = new ROSpanCharMatcher( s );
            Guid readId;
            m.TryMatchGuid( out readId ).ShouldBeTrue();
            readId.ShouldBe( id );
        }
        {
            string s = "S" + sId;
            var m = new ROSpanCharMatcher( s );
            Guid readId;
            m.TryMatch( 'S' ).ShouldBeTrue();
            m.TryMatchGuid( out readId ).ShouldBeTrue();
            readId.ShouldBe( id );
        }
        {
            string s = "S" + sId + "T";
            var m = new ROSpanCharMatcher( s );
            Guid readId;
            m.TryMatch( 'S' ).ShouldBeTrue();
            m.TryMatchGuid( out readId ).ShouldBeTrue();
            readId.ShouldBe( id );
            m.TryMatch( 'T' ).ShouldBeTrue();
        }
        sId = sId.Remove( sId.Length - 1 );
        {
            string s = sId;
            var m = new ROSpanCharMatcher( s );
            Guid readId;
            m.TryMatchGuid( out readId ).ShouldBeFalse();
            m.Head.Length.ShouldBe( m.AllText.Length );
        }
        sId = id.ToString().Insert( 3, "K" ).Remove( 4 );
        {
            string s = sId;
            var m = new ROSpanCharMatcher( s );
            Guid readId;
            m.TryMatchGuid( out readId ).ShouldBeFalse();
            m.Head.Length.ShouldBe( m.AllText.Length );
        }
    }

    [Test]
    public void OpenExpectations_are_pooled_objects()
    {
        var m = new ROSpanCharMatcher( "" );
        var g1 = m.OpenExpectations();
        g1.Dispose();
        var g1Back = m.OpenExpectations();
        g1Back.ShouldBeSameAs( g1 );
        g1Back.Dispose();

        g1 = m.OpenExpectations();
        var g2 = m.OpenExpectations();
        var g3 = m.OpenExpectations();
        var g4 = m.OpenExpectations();
        var g5 = m.OpenExpectations();

        g5.Dispose();
        g4.Dispose();
        g3.Dispose();
        g2.Dispose();
        g1.Dispose();

        g1Back = m.OpenExpectations();
        var g2Back = m.OpenExpectations();
        var g3Back = m.OpenExpectations();
        var g4Back = m.OpenExpectations();
        var g5Back = m.OpenExpectations();

        g1Back.ShouldBeSameAs( g1 );
        g2Back.ShouldBeSameAs( g2 );
        g3Back.ShouldBeSameAs( g3 );
        g4Back.ShouldBeSameAs( g4 );
        g5Back.ShouldBeSameAs( g5 );
    }

    [Test]
    public void Mismatch_OpenExpectations_Dispose_throw_InvalidOpertionException()
    {
        var m = new ROSpanCharMatcher( "" );
        var g1 = m.OpenExpectations();
        var g2 = m.OpenExpectations();
        Util.Invokable( () => g1.Dispose() ).ShouldThrow<InvalidOperationException>();
        g2.Dispose();
        g1.Dispose();
    }

    [Test]
    public void Expectation_and_error_management_with_OpenExpectations()
    {
        const string defaultGroupName = nameof( Expectation_and_error_management_with_OpenExpectations );

        var m = new ROSpanCharMatcher( "" );
        m.HasError.ShouldBeFalse();
        using( m.OpenExpectations() )
        {
            m.HasError.ShouldBeFalse( "Newly opened group has no error." );
            m.SetSuccess();
        }
        m.HasError.ShouldBeFalse( "ClearExpectations has been called in group." );
        using( m.OpenExpectations() )
        {
            m.HasError.ShouldBeFalse();
        }
        m.HasError.ShouldBeTrue( "ClearExpectations has NOT been called in group: the error is the caller name since there's no explicit expect string." );
        m.GetRawErrors().Single().Expectation.ShouldBe( defaultGroupName );

        m.SetSuccess();
        m.HasError.ShouldBeFalse();
        m.GetRawErrors().ShouldBeEmpty();

        using( m.OpenExpectations() )
        {
            m.HasError.ShouldBeFalse();
            m.AddExpectation( "1" );
            m.SetSuccess();

            m.HasError.ShouldBeFalse();
            m.GetRawErrors().ShouldBeEmpty();
        }
        m.HasError.ShouldBeFalse();
        m.GetRawErrors().ShouldBeEmpty();

        using( m.OpenExpectations() )
        {
            m.HasError.ShouldBeFalse();
            m.AddExpectation( "1" );
            m.HasError.ShouldBeTrue();
            using( m.OpenExpectations( "sub" ) )
            {
                m.HasError.ShouldBeFalse( "No error in this group." );
            }
            m.HasError.ShouldBeTrue();
            m.GetRawErrors().Select( e => e.Expectation ).ToArray().ShouldBe( new[] { "1", "sub" } );
        }
        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Select( e => e.Expectation ).ToArray().ShouldBe( new[] { defaultGroupName, "1", "sub" } );

        using( m.OpenExpectations( "G2" ) )
        {
            m.HasError.ShouldBeFalse();
            m.AddExpectation( "2" );
            m.HasError.ShouldBeTrue();
            using( m.OpenExpectations( "sub2" ) )
            {
                m.HasError.ShouldBeFalse();
                m.AddExpectation( "E" );
            }
            m.HasError.ShouldBeTrue();
            m.GetRawErrors().Select( e => e.Expectation ).ToArray().ShouldBe( new[] { "2", "sub2", "E" } );
        }
        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Select( e => e.Expectation ).ToArray().ShouldBe( new[] { defaultGroupName, "1", "sub", "G2", "2", "sub2", "E" } );
    }

    [Test]
    public void when_SingleExpectationMode_is_true_only_the_last_expectation_is_kept()
    {
        var m = new ROSpanCharMatcher( "" );
        m.SingleExpectationMode.ShouldBeFalse( "The default is false." );
        m.SingleExpectationMode = true;

        m.AddExpectation( "A" );
        m.AddExpectation( "B" );
        m.AddExpectation( "C" );

        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Single().Expectation.ShouldBe( "C" );

        m.SingleExpectationMode = false;

        m.AddExpectation( "A" );
        m.AddExpectation( "B" );

        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Select(e => e.Expectation).ToArray().ShouldBe( new[] { "C", "A", "B" } );

        m.SingleExpectationMode = true;

        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Single().Expectation.ShouldBe( "B" );

    }

    [Test]
    public void above_SingleExpectationMode_initially_propagates_to_OpenExpectations_groups()
    {
        var m = new ROSpanCharMatcher( "" );
        m.SingleExpectationMode.ShouldBeFalse();
        m.SingleExpectationMode = true;

        using( m.OpenExpectations( "Thing" ) )
        {
            m.SingleExpectationMode.ShouldBeTrue();
            m.AddExpectation( "A" );
            m.AddExpectation( "B" );
            m.AddExpectation( "C" );
            m.HasError.ShouldBeTrue();
            m.GetRawErrors().Single().Expectation.ShouldBe( "C" );
        }
        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Single().Expectation.ShouldBe( "Thing" );

        m.AddExpectation( "A" );
        m.AddExpectation( "B" );
        m.AddExpectation( "C" );

        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Single().Expectation.ShouldBe( "C" );

        m.SetSuccess();
        m.GetRawErrors().ShouldBeEmpty();

        using( m.OpenExpectations( "Thing1" ) )
        {
            m.HasError.ShouldBeFalse();
            m.SingleExpectationMode.ShouldBeTrue();
            m.AddExpectation( "A1" );
            m.AddExpectation( "B1" );
            m.AddExpectation( "C1" );
            m.HasError.ShouldBeTrue();
            m.GetRawErrors().Single().Expectation.ShouldBe( "C1" );
            using( m.OpenExpectations( "Thing2" ) )
            {
                m.HasError.ShouldBeFalse();
                m.SingleExpectationMode.ShouldBeTrue();
                m.AddExpectation( "A2" );
                m.AddExpectation( "B2" );
                m.AddExpectation( "C2" );
                m.HasError.ShouldBeTrue();
                m.GetRawErrors().Single().Expectation.ShouldBe( "C2" );
            }
            m.HasError.ShouldBeTrue();
            m.GetRawErrors().Single().Expectation.ShouldBe( "Thing2" );
        }
        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Single().Expectation.ShouldBe( "Thing1" );
    }

    [Test]
    public void SingleExpectationMode_can_be_freely_changed_in_any_OpenExpectations_group()
    {
        var m = new ROSpanCharMatcher( "" );
        m.SingleExpectationMode.ShouldBeFalse();
        m.SingleExpectationMode = true;

        using( m.OpenExpectations( "Thing" ) )
        {
            m.SingleExpectationMode.ShouldBeTrue();
            m.SingleExpectationMode = false;
            m.AddExpectation( "A" );
            m.AddExpectation( "B" );
            m.AddExpectation( "C" );
            m.HasError.ShouldBeTrue();
            m.GetRawErrors().Select( e => e.Expectation ).ToArray().ShouldBe( new[] { "A", "B", "C" } );
        }
        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Single().Expectation.ShouldBe( "Thing" );

        m.SingleExpectationMode = false;
        using( m.OpenExpectations( "Thing2" ) )
        {
            m.SingleExpectationMode.ShouldBeFalse();
            m.AddExpectation( "A" );
            m.AddExpectation( "B" );
            m.AddExpectation( "C" );
            m.HasError.ShouldBeTrue();
            m.GetRawErrors().Select( e => e.Expectation ).ToArray().ShouldBe( new[] { "A", "B", "C" } );

            m.SingleExpectationMode = true;
            m.GetRawErrors().Single().Expectation.ShouldBe( "C" );
        }

        m.HasError.ShouldBeTrue();
        m.GetRawErrors().Select(e => e.Expectation).ToArray().ShouldBe( new[] { "Thing", "Thing2", "C" } );
    }

}
