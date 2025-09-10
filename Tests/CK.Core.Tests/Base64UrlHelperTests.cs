using Shouldly;
using NUnit.Framework;
using System;
using System.Security.Cryptography;

namespace CK.Core.Tests;

[TestFixture]
public class Base64UrlHelperTests
{
    [TestCase( 0 )]
    [TestCase( 1 )]
    [TestCase( 2 )]
    [TestCase( 3 )]
    [TestCase( 4 )]
    [TestCase( 257 )]
    [TestCase( 3712 )]
    [TestCase( 5454 )]
    public void convert_tests( int size )
    {
        var bytes = RandomNumberGenerator.GetBytes( size );
        var s = Base64UrlHelper.ToBase64UrlString( bytes );
        var back = Base64UrlHelper.FromBase64UrlString( s );
        back.ToArray().ShouldBeEquivalentTo( bytes );
    }

    [TestCase( "°" )]
    [TestCase( "+" )]
    [TestCase( "=" )]
    [TestCase( "A" )]
    [TestCase( "/" )]
    [TestCase( "YQ==" )]
    public void invalid_FromBase64UrlString_must_throw( string s )
    {
        Should.Throw<ArgumentException>( () => Base64UrlHelper.FromBase64UrlString( s ) );
    }

    [TestCase( "QQ", true )]
    [TestCase( "-", true )]
    [TestCase( "_", true )]
    [TestCase( "3712", true )]
    [TestCase( "§", false )]
    [TestCase( "12*45", false )]
    [TestCase( "12*", false )]
    public void IsBase64UrlString_check( string s, bool expect )
    {
        Base64UrlHelper.Base64UrlCharacters.Length.ShouldBe( 64 );
        Base64UrlHelper.IsBase64UrlCharacters( s ).ShouldBe( expect );
    }

}
