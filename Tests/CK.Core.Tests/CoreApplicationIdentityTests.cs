using Shouldly;
using NUnit.Framework;
using System;
using System.Diagnostics;

namespace CK.Core.Tests;


[TestFixture]
public class CoreApplicationIdentityTests
{
    public static bool ResetStaticIdentity()
    {
        System.Reflection.MethodInfo? m = typeof( CoreApplicationIdentity ).GetMethod( "Reset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static );
        if( m == null ) return false;
        m.Invoke( null, null );
        return true;
    }

    [Test]
    public void InstanceId_is_21_chars_length()
    {
        CoreApplicationIdentity.InstanceId.Length.ShouldBe( 21 );
    }

    [Test]
    public void IsValidPartyName_check()
    {
        CoreApplicationIdentity.IsValidPartyName( "" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidPartyName( "$" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidPartyName( " " ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidPartyName( "_" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidPartyName( "_A" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidPartyName( "A_" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidPartyName( "-A" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidPartyName( "A-" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidPartyName( new string( 'P', CoreApplicationIdentity.PartyNameMaxLength + 1 ) ).ShouldBeFalse();

        CoreApplicationIdentity.IsValidPartyName( "A" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidPartyName( "A_B" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidPartyName( "A__B" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidPartyName( "A__B_C" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidPartyName( "A-B" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidPartyName( "A--B" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidPartyName( "A--B-C" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidPartyName( "$A" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidPartyName( "$A_B" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidPartyName( "$A__B" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidPartyName( "$A__B_C" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidPartyName( "$A-B" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidPartyName( "$A--B" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidPartyName( "$A--B-C" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidPartyName( "$" + new string( 'P', CoreApplicationIdentity.PartyNameMaxLength ) ).ShouldBeTrue();
    }

    [Test]
    public void IsValidDomainName_check()
    {
        CoreApplicationIdentity.IsValidDomainName( "" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidDomainName( "/" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidDomainName( "/A" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidDomainName( "A/" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidDomainName( "A//B" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidDomainName( "A/B/C/" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidDomainName( "A/B//C" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidDomainName( new string( 'D', CoreApplicationIdentity.DomainNameMaxLength + 1 ) ).ShouldBeFalse();

        CoreApplicationIdentity.IsValidDomainName( "A" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidDomainName( "A/B" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidDomainName( "A/B/C" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidDomainName( new string( 'D', CoreApplicationIdentity.DomainNameMaxLength ) ).ShouldBeTrue();
    }

    [Test]
    public void IsValidEnvironmentName_check()
    {
        CoreApplicationIdentity.IsValidEnvironmentName( "" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidEnvironmentName( "#" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidEnvironmentName( "A" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidEnvironmentName( "/" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidEnvironmentName( "#~" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidEnvironmentName( "#/" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidEnvironmentName( "##" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidEnvironmentName( "#A#" ).ShouldBeFalse();
        CoreApplicationIdentity.IsValidEnvironmentName( "#" + new string( 'E', CoreApplicationIdentity.EnvironmentNameMaxLength ) ).ShouldBeFalse();

        CoreApplicationIdentity.IsValidEnvironmentName( "#A" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidEnvironmentName( "#AB" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidEnvironmentName( "#_Prod-Lovely_" ).ShouldBeTrue();
        CoreApplicationIdentity.IsValidEnvironmentName( "#" + new string( 'E', CoreApplicationIdentity.EnvironmentNameMaxLength - 1 ) ).ShouldBeTrue();
    }

    [Test]
    public void PartyName_from_ProcessPath_ultimately_resolves_to_Unknown()
    {
        var valid = CoreApplicationIdentity.Builder.PartyNameFromProcessPath( Environment.ProcessPath );
        CoreApplicationIdentity.IsValidPartyName( valid ).ShouldBeTrue();

        CoreApplicationIdentity.Builder.PartyNameFromProcessPath( "" ).ShouldBe( "Unknown" );
    }

    [Test]
    public void default_CoreApplicationIdentity_is_valid()
    {
        Debug.Assert( CoreApplicationIdentity.DefaultDomainName == "Undefined" );
        Debug.Assert( CoreApplicationIdentity.DefaultEnvironmentName == "#Dev" );
        Debug.Assert( CoreApplicationIdentity.DefaultPartyName == "Unknown" );
        var d = CoreApplicationIdentity.Instance;
        d.DomainName.ShouldBe( "Undefined" );
        d.EnvironmentName.ShouldBe( "#Dev" );
        d.PartyName.ShouldNotBeNullOrWhiteSpace();
        d.PartyName.ShouldNotContain( "\\" );
        d.PartyName.ShouldNotContain( "//" );
        d.PartyName.ShouldNotContain( ":" );
        d.PartyName.ShouldNotStartWith( "_" );
        d.PartyName.ShouldNotStartWith( "-" );
        d.ContextDescriptor.ShouldBe( "" );
        d.ContextualId.ShouldNotBeNullOrWhiteSpace();
        d.PartyContextualName.ShouldBe( d.PartyName + ".C" + d.ContextualId );
        d.PartyInstanceName.ShouldBe( d.PartyName + ".I" + CoreApplicationIdentity.InstanceId );
    }

    [Test]
    public void OnInitialized_is_always_called_even_after_initialization()
    {
        Assume.That( ResetStaticIdentity(), "This test can only run in DEBUG." );

        // This test shows that CancellationTokenSource reverts the call order.
        // Here we don't really care but this could be annoying in general
        // since this doesn't follow the standard event pattern...
        //
        // This behavior is by design and intended (comment from the CancellationTokenSource.ExecuteCallbackHandlers source code):
        //
        // "We call the delegates in LIFO order on each partition so that callbacks fire 'deepest first'.
        //  This is intended to help with nesting scenarios so that child enlisters cancel before their parents."
        //
        int callOrder = 0;
        int call1 = 0;
        CoreApplicationIdentity.OnInitialized( () => call1 = ++callOrder );
        int call2 = 0;
        CoreApplicationIdentity.OnInitialized( () => call2 = ++callOrder );
        int call3 = 0;
        CoreApplicationIdentity.OnInitialized( () => call3 = ++callOrder );
        int call4 = 0;
        CoreApplicationIdentity.OnInitialized( () => call4 = ++callOrder );

        CoreApplicationIdentity.Initialize();

        int call5 = 0;
        CoreApplicationIdentity.OnInitialized( () => call5 = ++callOrder );

        call1.ShouldBe( 4 );
        call2.ShouldBe( 3 );
        call3.ShouldBe( 2 );
        call4.ShouldBe( 1 );
        call5.ShouldBe( 5 );
    }

    [Test]
    public void once_Initialized_Configure_fails()
    {
        Assume.That( ResetStaticIdentity(), "This test can only run in DEBUG." );

        CoreApplicationIdentity.IsInitialized.ShouldBeFalse();
        CoreApplicationIdentity.TryConfigure( b => b.DomainName = "D" ).ShouldBeTrue();
        CoreApplicationIdentity.Configure( b => b.PartyName = "Pop", true );

        CoreApplicationIdentity.IsInitialized.ShouldBeTrue();

        CoreApplicationIdentity.TryConfigure( b => b.DomainName = "nop" ).ShouldBeFalse();
        Should.Throw<InvalidOperationException>( () => CoreApplicationIdentity.Configure( b => b.PartyName = "Pop" ) );
    }

    [Test]
    public void max_lengths_are_checked_by_builder()
    {
        Assume.That( ResetStaticIdentity(), "This test can only run in DEBUG." );

        CoreApplicationIdentity.IsInitialized.ShouldBeFalse();
        Should.Throw<ArgumentNullException>( () => CoreApplicationIdentity.Configure( b => b.DomainName = null! ) );
        Should.Throw<ArgumentException>( () => CoreApplicationIdentity.Configure( b => b.DomainName = "" ) );
        Should.Throw<ArgumentException>( () => CoreApplicationIdentity.Configure( b => b.DomainName = "." ) );
        Should.Throw<ArgumentException>( () => CoreApplicationIdentity.Configure( b => b.DomainName = "/A" ) );
        Should.Throw<ArgumentException>( () => CoreApplicationIdentity.Configure( b => b.DomainName = new string( 'D', CoreApplicationIdentity.DomainNameMaxLength + 1 ) ) );

        Should.Throw<ArgumentNullException>( () => CoreApplicationIdentity.Configure( b => b.EnvironmentName = null! ) );
        Should.Throw<ArgumentException>( () => CoreApplicationIdentity.Configure( b => b.EnvironmentName = "_" ) );
        Should.Throw<ArgumentException>( () => CoreApplicationIdentity.Configure( b => b.EnvironmentName = "A_" ) );
        Should.Throw<ArgumentException>( () => CoreApplicationIdentity.Configure( b => b.EnvironmentName = "" ) );
        Should.Throw<ArgumentException>( () => CoreApplicationIdentity.Configure( b => b.EnvironmentName = "." ) );
        Should.Throw<ArgumentException>( () => CoreApplicationIdentity.Configure( b => b.EnvironmentName = "A/B" ) );
        Should.Throw<ArgumentException>( () => CoreApplicationIdentity.Configure( b => b.EnvironmentName = new string( 'E', CoreApplicationIdentity.EnvironmentNameMaxLength + 1 ) ) );

        Should.Throw<ArgumentException>( () => CoreApplicationIdentity.Configure( b => b.PartyName = "" ) );
        Should.Throw<ArgumentException>( () => CoreApplicationIdentity.Configure( b => b.PartyName = "." ) );
        Should.Throw<ArgumentException>( () => CoreApplicationIdentity.Configure( b => b.PartyName = "A/B" ) );
        Should.Throw<ArgumentException>( () => CoreApplicationIdentity.Configure( b => b.PartyName = new string( 'P', CoreApplicationIdentity.PartyNameMaxLength + 1 ) ) );

        Should.Throw<ArgumentException>( () => CoreApplicationIdentity.Configure( b => b.ContextDescriptor = new string( 'C', CoreApplicationIdentity.ContextDescriptorMaxLength + 1 ) ) );

        CoreApplicationIdentity.IsInitialized.ShouldBeFalse();
    }

    [Test]
    public void PartyNameFromProcessPath_tests()
    {
        CoreApplicationIdentity.Builder.PartyNameFromProcessPath( null ).ShouldBe( CoreApplicationIdentity.DefaultPartyName );
        CoreApplicationIdentity.Builder.PartyNameFromProcessPath( "" ).ShouldBe( CoreApplicationIdentity.DefaultPartyName );
        CoreApplicationIdentity.Builder.PartyNameFromProcessPath( "---" ).ShouldBe( CoreApplicationIdentity.DefaultPartyName );
        CoreApplicationIdentity.Builder.PartyNameFromProcessPath( "_" ).ShouldBe( CoreApplicationIdentity.DefaultPartyName );
        CoreApplicationIdentity.Builder.PartyNameFromProcessPath( "0123456789" ).ShouldBe( CoreApplicationIdentity.DefaultPartyName );
        CoreApplicationIdentity.Builder.PartyNameFromProcessPath( "_0_" ).ShouldBe( CoreApplicationIdentity.DefaultPartyName );
        CoreApplicationIdentity.Builder.PartyNameFromProcessPath( "Party012345678901234567890123456789" ).ShouldBe( "y012345678901234567890123456789" );
    }

    // With party.
    [TestCase( "A/$p", "A", "$p", null )]
    [TestCase( "$p/A", "A", "$p", null )]
    [TestCase( "$p/A/B", "A/B", "$p", null )]
    [TestCase( "A/B/$p", "A/B", "$p", null )]
    [TestCase( "A/$p/B", "A/B", "$p", null )]
    [TestCase( "A/B/C/$p", "A/B/C", "$p", null )]
    [TestCase( "A/B/$p/C", "A/B/C", "$p", null )]
    [TestCase( "A/$p/B/C", "A/B/C", "$p", null )]
    [TestCase( "$p/A/B/C", "A/B/C", "$p", null )]
    // With party and environment.
    [TestCase( "A/$p/#e", "A", "$p", "#e" )]
    [TestCase( "$p/A/#e", "A", "$p", "#e" )]
    [TestCase( "$p/A/B/#e", "A/B", "$p", "#e" )]
    [TestCase( "A/B/$p/#e", "A/B", "$p", "#e" )]
    [TestCase( "A/$p/B/#e", "A/B", "$p", "#e" )]
    [TestCase( "A/B/C/$p/#e", "A/B/C", "$p", "#e" )]
    [TestCase( "A/B/$p/C/#e", "A/B/C", "$p", "#e" )]
    [TestCase( "A/$p/B/C/#e", "A/B/C", "$p", "#e" )]
    [TestCase( "$p/A/B/C/#e", "A/B/C", "$p", "#e" )]
    [TestCase( "A/#e/$p", "A", "$p", "#e" )]
    [TestCase( "$p/#e/A", "A", "$p", "#e" )]
    [TestCase( "$p/A/#e/B", "A/B", "$p", "#e" )]
    [TestCase( "A/B/#e/$p", "A/B", "$p", "#e" )]
    [TestCase( "A/$p/#e/B", "A/B", "$p", "#e" )]
    [TestCase( "#e/A/B/C/$p", "A/B/C", "$p", "#e" )]
    [TestCase( "A/#e/B/$p/C", "A/B/C", "$p", "#e" )]
    [TestCase( "A/$p/B/#e/C", "A/B/C", "$p", "#e" )]
    [TestCase( "#e/$p/A/B/C", "A/B/C", "$p", "#e" )]
    // Domain only.
    [TestCase( "A", "A", null, null )]
    [TestCase( "A/B", "A/B", null, null )]
    // With environment.
    [TestCase( "A/B/#e", "A/B", null, "#e" )]
    [TestCase( "#e/A/B", "A/B", null, "#e" )]
    [TestCase( "A/#e/B", "A/B", null, "#e" )]
    public void TryParseFullName_successful_tests( string? f, string domainName, string? partyName, string? environmentName )
    {
        CoreApplicationIdentity.TryParseFullName( f, out var d, out var p, out var e ).ShouldBeTrue();
        d.ShouldBe( domainName );
        p.ShouldBe( partyName );
        e.ShouldBe( environmentName );
    }

    [TestCase( null )]
    [TestCase( "" )]
    [TestCase( "$p" )]
    [TestCase( "#e" )]
    [TestCase( "#e/$p" )]
    [TestCase( "A$p/B/#e" )]
    [TestCase( "A/$p/B#e" )]
    public void TryParseFullName_failed_tests( string f )
    {
        CoreApplicationIdentity.TryParseFullName( f, out _, out _, out _ ).ShouldBeFalse();
    }

    [Test]
    public void TryParseFullName_failed_too_long_tests()
    {
        var domainMax = new string( 'd', CoreApplicationIdentity.DomainNameMaxLength );
        var partyMax = new string( 'p', CoreApplicationIdentity.PartyNameMaxLength );
        var environmentMax = '#' + new string( 'e', CoreApplicationIdentity.EnvironmentNameMaxLength - 1 );
        CoreApplicationIdentity.TryParseFullName( $"{domainMax}/${partyMax}/{environmentMax}", out var d, out var p, out var e ).ShouldBeTrue();
        d.ShouldBe( domainMax );
        p.ShouldBe( '$' + partyMax );
        e.ShouldBe( environmentMax );

        CoreApplicationIdentity.TryParseFullName( $"X{domainMax}/${partyMax}/{environmentMax}", out _, out _, out _ ).ShouldBeFalse();
        CoreApplicationIdentity.TryParseFullName( $"{domainMax}D/$p/#e", out _, out _, out _ ).ShouldBeFalse();
        CoreApplicationIdentity.TryParseFullName( $"D/${partyMax}X/#e", out _, out _, out _ ).ShouldBeFalse();
        CoreApplicationIdentity.TryParseFullName( $"D/$p/{environmentMax}X", out _, out _, out _ ).ShouldBeFalse();

    }
}
