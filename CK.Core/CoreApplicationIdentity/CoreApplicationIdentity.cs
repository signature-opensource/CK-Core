using CommunityToolkit.HighPerformance;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace CK.Core;

/// <summary>
/// Exposes the identity of the current application (technically the Application Domain) as an immutable
/// singleton <see cref="Instance"/>.
/// <see cref="Configure(Action{Builder},bool)"/> or <see cref="TryConfigure(Action{Builder})"/> methods can be called
/// until the instance is used. <see cref="OnInitialized(Action)"/> enables deferring actions to wait for the application
/// identity to be ready.
/// <para>
/// The "party" is identified by the <see cref="DomainName"/>, <see cref="EnvironmentName"/> and <see cref="PartyName"/>.
/// This "party" is the logical process: logs issued by a process can always be grouped by this "party identifier".
/// </para>
/// But "process uniqueness" is a complex matter. A process can only be truly unique (at any point in time) by using
/// synchronization primitives like <see cref="System.Threading.Mutex"/> or specific coordination infrastructure
/// (election algorithms) in distributed systems.
/// <para>
/// At least, the "running instance" itself is necessarily unique because of the <see cref="InstanceId"/>.
/// </para>
/// <para>
/// Other "identity" can be captured by the <see cref="ContextDescriptor"/>: this can use process arguments, working directory
/// or any other contextual information that helps identify a process. Whether this uniquely identifies the process
/// is not (and cannot) be handled by this model.
/// </para>
/// </summary>
public sealed partial class CoreApplicationIdentity
{
    /// <summary>
    /// The "Undefined" domain name denotes an external party of the System.
    /// </summary>
    public const string DefaultDomainName = "Undefined";

    /// <summary>
    /// This is "#Dev". Environment names always start with '#'.
    /// </summary>
    public const string DefaultEnvironmentName = "#Dev";

    /// <summary>
    /// The default party name is "Unknown".
    /// </summary>
    public const string DefaultPartyName = "Unknown";

    /// <summary>
    /// The maximal domain name length.
    /// </summary>
    public const int DomainNameMaxLength = 127;

    /// <summary>
    /// The maximal environment name length including the required leading '#'.
    /// </summary>
    public const int EnvironmentNameMaxLength = 31;

    /// <summary>
    /// The maximal party name length.
    /// This doesn't include the '$' prefix that appears only in full names.
    /// </summary>
    public const int PartyNameMaxLength = 31;

    /// <summary>
    /// The maximal ContextDescriptor length.
    /// </summary>
    public const int ContextDescriptorMaxLength = 8 * 1024;

    /// <summary>
    /// The maximal <see cref="CoreApplicationIdentity.FullName"/> length.
    /// </summary>
    public const int FullNameMaxLength = DomainNameMaxLength + EnvironmentNameMaxLength + PartyNameMaxLength + 3;

    /// <summary>
    /// Gets the name of the domain to which this application belongs.
    /// It cannot empty and defaults to "Undefined" (<see cref="DefaultDomainName"/>). This reserved name
    /// should be treated as "external to the System".
    /// Its maximal length is <see cref="DomainNameMaxLength"/>.
    /// <para>
    /// See <see cref="IsValidDomainName(ReadOnlySpan{char})"/> for its syntax.
    /// </para>
    /// </summary>
    public string DomainName { get; }

    /// <summary>
    /// Gets the name of the environment. Defaults to "#Dev" (<see cref="DefaultEnvironmentName"/>).
    /// Its maximal length is <see cref="EnvironmentNameMaxLength"/>.
    /// <para>
    /// "#Development" (case insensitive) is normalized to "#Dev".
    /// </para>
    /// <para>
    /// See <see cref="IsValidEnvironmentName(ReadOnlySpan{char})"/> for its syntax.
    /// </para>
    /// </summary>
    public string EnvironmentName { get; }

    /// <summary>
    /// Gets this party name. Cannot be empty.
    /// Its maximal length is <see cref="PartyNameMaxLength"/>, it doesn't start with '$': the '$' must
    /// appear only in a <see cref="FullName"/>.
    /// <para>
    /// See <see cref="IsValidPartyName(ReadOnlySpan{char})"/> for its syntax.
    /// </para>
    /// <para>
    /// Defaults to a string derived from the <see cref="Environment.ProcessPath"/>.
    /// If the ProcessPath is null, "Unknown" string is used (<see cref="DefaultPartyName"/>).
    /// </para>
    /// </summary>
    public string PartyName { get; }

    /// <summary>
    /// Gets a string that identifies the context into which this
    /// application is running. Defaults to the empty string.
    /// <para>
    /// There is no constraint on this string except that it cannot exceed <see cref="ContextDescriptorMaxLength"/>
    /// (but shorter is better).
    /// Note that the characters 0 to 8 (NUl, SOH, STX, ETX, EOT, ENQ, ACK, BEL, BSP) are
    /// mapped to their respective angle bracket enclosed string representation
    /// (0x0 is mapped to &lt;NUL&gt;, 0x1 to &lt;SOH&gt;, etc.).
    /// </para>
    /// </summary>
    public string ContextDescriptor { get; }

    /// <summary>
    /// Gets a Base64Url encoded opaque string that is the SHA1
    /// of the <see cref="DomainName"/>/<see cref="EnvironmentName"/>/<see cref="PartyName"/>/<see cref="ContextDescriptor"/>.
    /// This identifies this application and its running context (but not this running instance).
    /// </summary>
    public string ContextualId { get; }

    /// <summary>
    /// Gets this <see cref="PartyName"/>.C<see cref="ContextualId"/>.
    /// Since PartyName cannot contain '.', the "-C" acts as an easy separator that can be used.
    /// </summary>
    public string PartyContextualName { get; }

    /// <summary>
    /// Gets this <see cref="PartyName"/>.I<see cref="InstanceId"/>.
    /// Since PartyName cannot contain '.', the "-I" acts as an easy separator that can be used.
    /// </summary>
    public string PartyInstanceName { get; }

    /// <summary>
    /// Gets this identity default full name: <see cref="DomainName"/>/$<see cref="PartyName"/>/<see cref="EnvironmentName"/>
    /// (note that EnvironmentName always starts with `#`).
    /// The maximal length is <see cref="FullNameMaxLength"/>.
    /// <para>
    /// Full names scheme can differ: thanks to the '#' and the '$' it is always possible to
    /// recover the {Domain,Party,Environment} triplet.
    /// </para>
    /// </summary>
    public NormalizedPath FullName { get; }

    /// <summary>
    /// Gets a Base64Url encoded opaque random string that identifies this running instance.
    /// This is currently a 21 characters (15 bytes of entropy).
    /// <para>
    /// This is a static property: it's available as soon as the process starts.
    /// </para>
    /// </summary>
    public static string InstanceId { get; } = Util.GetRandomBase64UrlString( 21 );

    CoreApplicationIdentity( Builder b )
    {
        DomainName = b.DomainName;
        EnvironmentName = b.EnvironmentName;
        PartyName = b.PartyName ?? DefaultPartyName;
        ContextDescriptor = b.ContextDescriptor ?? "";
        FullName = $"{DomainName}/${PartyName}/{EnvironmentName}";
        ContextualId = Base64UrlHelper.ToBase64UrlString( SHA1.HashData( Encoding.UTF8.GetBytes( $"{FullName.Path}/{ContextDescriptor}" ) ) );
        PartyContextualName = PartyName + ".C" + ContextualId;
        PartyInstanceName = PartyName + ".I" + InstanceId;
    }

    static Builder? _builder;
    static CoreApplicationIdentity? _instance;

#if DEBUG
    // Method that exists in DEBUG only. Used by tests: the
    // CancellationTokenSource can be reset.
    static CancellationTokenSource _token;

    static void Reset()
    {
        _instance = null;
        _builder = new Builder();
        _token = new CancellationTokenSource();
        IsInitialized = false;
    }
#else
    // Real (release) CancellationTokenSource cannot be reset.
    // Tests are disabled.
    static readonly CancellationTokenSource _token;
#endif

    static CoreApplicationIdentity()
    {
        _builder = new Builder();
        _token = new CancellationTokenSource();
    }

    /// <summary>
    /// Configure the application identity if it's not yet initialized or throws an <see cref="InvalidOperationException"/> otherwise.
    /// </summary>
    /// <param name="configurator">The configuration action.</param>
    /// <param name="initialize">Whether <see cref="Initialize()"/> should be called to lock the identity.</param>
    public static void Configure( Action<Builder> configurator, bool initialize = false )
    {
        lock( _token )
        {
            if( _builder == null ) Throw.InvalidOperationException( "CoreApplicationIdentity is already initialized." );
            else configurator( _builder );
            if( initialize ) Initialize();
        }
    }

    /// <summary>
    /// Tries to configure the application identity if it's not yet initialized.
    /// Any invalid configuration value (like a too long or invalid party name) will throw an <see cref="ArgumentException"/>.
    /// </summary>
    /// <param name="configurator">The configuration action.</param>
    /// <returns>True if the <paramref name="configurator"/> has been called, false if the <see cref="Instance"/> is already available.</returns>
    public static bool TryConfigure( Action<Builder> configurator )
    {
        if( _builder != null )
        {
            lock( _token )
            {
                if( _builder != null )
                {
                    configurator( _builder );
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Gets whether the <see cref="Instance"/> has been initialized or
    /// can still be configured.
    /// <para>This is set to true after the callbacks of <see cref="OnInitialized(Action)"/> have been called.</para>
    /// </summary>
    public static bool IsInitialized { get; private set; }

    /// <summary>
    /// Registers a callback that will be called when the <see cref="Instance"/> will be available
    /// or immediately if the instance has already been configured.
    /// <para>
    /// Current implementation calls the stored delegates in LIFO order. If this becomes an issue, this will
    /// be changed.
    /// </para>
    /// </summary>
    /// <param name="action">Any action that requires the application's identity to be available.</param>
    public static void OnInitialized( Action action )
    {
        _token.Token.UnsafeRegister( _ => action(), null );
    }

    /// <summary>
    /// Gets the available identity.
    /// The first call to this property triggers the initialization of the identity
    /// and the calls to registered <see cref="OnInitialized(Action)"/> callbacks.
    /// </summary>
    public static CoreApplicationIdentity Instance => Initialize();

    /// <summary>
    /// Ensures that the available identity is initialized.
    /// This locks the <see cref="Instance"/> and calls the registered <see cref="OnInitialized(Action)"/>
    /// callbacks.
    /// </summary>
    public static CoreApplicationIdentity Initialize()
    {
        if( _instance == null )
        {
            bool callInit = false;
            // Simple double check locking.
            lock( _token )
            {
                if( _instance == null )
                {
                    Debug.Assert( _builder != null );
                    _instance = _builder.Build();
                    _builder = null;
                    callInit = true;
                }
            }
            // Calls the callbacks outside the lock.
            if( callInit )
            {
                _token.Cancel( throwOnFirstException: true );
                IsInitialized = true;
            }
        }
        return _instance;
    }

    /// <summary>
    /// Gets whether the <see cref="Instance"/> is available and we are in "#Dev" environment:
    /// Use this when something must be done in "#Dev" environment and only in "#Dev".
    /// </summary>
    public static bool IsDevelopmentAndInitialized => IsInitialized && Instance.EnvironmentName == DefaultEnvironmentName;

    /// <summary>
    /// Gets whether the <see cref="Instance"/> is not yet initialized or we are in "#Dev" environment.
    /// Use this when something must be done in "#Dev" environment or when the identity is not yet settled.
    /// </summary>
    public static bool IsDevelopmentOrUninitialized => !IsInitialized || Instance.EnvironmentName == DefaultEnvironmentName;

    /// <summary>
    /// Tries to parse a full name in which the $PartyName part can be anywhere (and optional)
    /// and the #EnvironmentName part can be anywhere (and optional).
    /// <para>
    /// A simple domain name is a valid full name. 
    /// </para>
    /// </summary>
    /// <param name="fullName">The full name to parse.</param>
    /// <param name="domainName">The parsed domain name.</param>
    /// <param name="partyName">The parsed party name with the leading '$' or null.</param>
    /// <param name="environmentName">The parsed environment name or null (<see cref="DefaultEnvironmentName"/> can be used).</param>
    /// <returns>True on success, false if the full name is not a valid identity full name (it must at least be a valid domain name).</returns>
    public static bool TryParseFullName( ReadOnlySpan<char> fullName,
                                         [NotNullWhen( true )] out string? domainName,
                                         out string? partyName,
                                         out string? environmentName )
    {
        domainName = null;
        partyName = null;
        environmentName = null;
        if( fullName.Length == 0 || fullName.Length > FullNameMaxLength ) return false;
        Span<char> d = stackalloc char[DomainNameMaxLength];
        int dHead = 0;
        foreach( var part in fullName.Tokenize( '/' ) )
        {
            if( part.Length == 0 ) return false;
            if( part[0] == '#' )
            {
                if( environmentName != null || !IsValidEnvironmentName( part ) ) return false;
                environmentName = part.ToString();
            }
            else if( part[0] == '$' )
            {
                if( partyName != null || !IsValidPartyName( part ) ) return false;
                partyName = part.ToString();
            }
            else
            {
                if( dHead == 0 )
                {
                    if( part.Length > DomainNameMaxLength ) return false;
                    part.CopyTo( d );
                }
                else
                {
                    if( ++dHead + part.Length > DomainNameMaxLength ) return false;
                    d[dHead - 1] = '/';
                    part.CopyTo( d.Slice( dHead ) );
                }
                dHead += part.Length;
            }
        }
        d = d.Slice( 0, dHead );
        if( !IsValidDomainName( d ) ) return false;
        domainName = d.ToString();
        return true;
    }

    /// <summary>
    /// Checks whether the value is a valid <see cref="CoreApplicationIdentity.DomainName"/>.
    /// <para>
    /// It must be a case sensitive identifier or path of identifiers not longer than <see cref="DomainNameMaxLength"/>:
    /// <list type="bullet">
    /// <item>
    /// Identifier should use PascalCase convention if possible and must only
    /// contain 'A'-'Z', 'a'-'z', '0'-'9', '-' and '_' characters and must not
    /// start with a digit, and not start or end with '_' or '-'.
    /// </item>
    /// <item>
    /// A path must be a '/' separated identifiers as described above. No leading or trailing '/' and no double '//' are allowed.
    /// </item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="value">The candidate domain name.</param>
    /// <returns>True for a valid domain name.</returns>
    public static bool IsValidDomainName( ReadOnlySpan<char> value )
    {
        if( value.Length == 0 || value.Length > DomainNameMaxLength ) return false;
        int idx = value.IndexOf( '/' );
        while( idx >= 0 )
        {
            if( idx == 0 ) return false;
            if( idx == value.Length - 1 ) return false;
            if( !IsValidIdentifier( value.Slice( 0, idx ) ) ) return false;
            value = value.Slice( idx + 1 );
            idx = value.IndexOf( '/' );
        }
        return IsValidIdentifier( value );

        static bool IsValidIdentifier( ReadOnlySpan<char> value )
        {
            if( value.Length == 0 ) return false;
            char first = value[0];
            if( Char.IsDigit( first ) || first == '-' || first == '_' ) return false;
            char last = value[value.Length - 1];
            if( last == '-' || last == '_' ) return false;
            foreach( var c in value )
            {
                if( !IsValidNameChar( c ) ) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Checks whether the value is a valid <see cref="CoreApplicationIdentity.EnvironmentName"/>.
    /// <para>
    /// It must start with a '#', should use PascalCase convention if possible and must only
    /// contain 'A'-'Z', 'a'-'z', '0'-'9', '-' and '_'.
    /// </para>
    /// </summary>
    /// <param name="value">The candidate.</param>
    /// <returns>True for a valid environment name.</returns>
    public static bool IsValidEnvironmentName( ReadOnlySpan<char> value )
    {
        if( value.Length < 2 || value.Length > EnvironmentNameMaxLength || value[0] != '#' ) return false;
        foreach( var c in value.Slice( 1 ) )
        {
            if( !IsValidNameChar( c ) ) return false;
        }
        return true;
    }

    /// <summary>
    /// Checks whether the value is a valid <see cref="CoreApplicationIdentity.PartyName"/>.
    /// <para>
    /// It should use PascalCase convention if possible and must only
    /// contain 'A'-'Z', 'a'-'z', '0'-'9', '-' and '_' characters and must not
    /// start with a digit, and not start or end with '_' or '-'.
    /// </para>
    /// <para>
    /// It can optionally be prefixed by '$' (its representation in the full name).
    /// The leading $ is silently ignored by this method.
    /// </para>
    /// <para>
    /// It must not be longer than <see cref="PartyNameMaxLength"/>.
    /// </para>
    /// </summary>
    /// <param name="value">The candidate.</param>
    /// <returns>True for a valid identifier.</returns>
    public static bool IsValidPartyName( ReadOnlySpan<char> value )
    {
        if( value.Length == 0 ) return false;
        if( value[0] == '$' )
        {
            value = value.Slice( 1 );
            if( value.Length == 0 ) return false;
        }
        if( value.Length > PartyNameMaxLength ) return false;
        char first = value[0];
        if( Char.IsDigit( first ) || first == '-' || first == '_' ) return false;
        char last = value[value.Length - 1];
        if( last == '-' || last == '_' ) return false;
        foreach( var c in value )
        {
            if( !IsValidNameChar( c ) ) return false;
        }
        return true;
    }

    static bool IsValidNameChar( char c )
    {
        return (c is >= 'a' and <= 'z') || (c is >= 'A' and <= 'Z') || (c is >= '0' and <= '9') || c == '-' || c == '_';
    }
}
