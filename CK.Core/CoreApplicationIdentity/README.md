# CoreApplicationIdentity

## Goal

The goal of this small static class is to capture once for all the notion of a process identity. And this is not a simple
issue.

The idea here is to express this as simply as possible with a triplet of strings:

- DomainName: A process is rooted in a "domain", that is a family of applications that typically work together. A domain 
  is like a "realm" in security frameworks, a "tenant" or an "organization" in a cloud infrastructure.
  A domain can be a path like "AcmeCorp", "AcmeCorp/CRM" or "AcmeCorp/CRM/UserMagement". Note that the first
  part of this path can often be seen as the "OrganizationName".
- EnvironmentName: is optional and aims to identify a deployment context like "#Production" or "#Staging".
  Environment names always starts with a `#` and defaults to "#Dev".
- PartyName: identifies the "application" itself, both the code base and its role in the architecture
  like a "MailSender", "SaaS-11" or "Worker_3712". When used in a path, party names can be prefixed by a `$`
  (they are always prefixed by `$` when they appear in a full name).

This doesn't imply any form of uniqueness, "process uniqueness" is a complex matter. A process can only be truly unique (at
any point in time) by using synchronization primitives like Mutex or specific coordination infrastructure (election algorithms)
in distributed systems. Even a "single web application" may not be truly unique: when its host does an "application recycling",
for a short period of time, two instances of the "same" application co-exist.

This small class will never solve this issue but it aims to capture and expose the necessary and sufficient information to reason
(or help reasoning) about it:

- The "running instance" itself is necessarily unique because of the static `InstanceId` that is a always available unique random string.
- Other "identity" can be captured by the `ContextDescriptor` that can use process arguments, working directory
or any other contextual information that helps identify a process.

One of the first usage of this `{DomainName,EnvironmentName,PartyName}` triplet is to provide a simple path
to group/route logs emitted by processes. Thanks to the `#` and `$` leading characters, any full path derived from
the triplet is self-described. For structuring incoming logs for instance one can choose the following pattern:
`OrganisationName/#EnvironmentName/DomainNameRemaider/$PartyName` (where the DomainName is split on its first part)
when this organization has multiple deployment/environment contexts. When no such global contexts is available, it
may be simpler to adopt a `DomainName/$PartyName/#EnvironmentName` scheme (that is the default).

## Naming constraints and defaults

All names are case sensitive, PascalCase convention should be use.

- DomainName defaults to `"Undefined"` and cannot be empty. It may a a path (contains '/'). Its maximal length is 127 characters.
- EnvironmentName defaults to `"#Dev"` and cannot be empty (`"#Development"` is normalized to `"#Dev"`). Its maximal length is 31 characters.
- PartyName cannot be empty. When not set, it is derived from the process path (`Builder.PartyNameFromProcessPath( Environment.ProcessPath )`)
  and ultimately defaults to `"Unknown"`. Its maximal length is 31 characters.

EnvironmentName and PartyName are "identifiers": the can only contain 'A'-'Z', 'a'-'z', '0'-'9', '-' and '\_'
characters and must not start with a digit, and not start or end with '_' or '-'.

DomainName is an identifier as described above or a path ('/' separated identifiers). No leading or trailing '/'
and no double '//' are allowed.

A default FullName property is available:
- It is `DomainName/$PartyName/#EnvironmentName`.
- The `$PartyName` and `#EnvironmentName` part is optional in a full name: a valid domain name is a valid full name,
- or said differently the full name is able to identify a domain (no specific party nor environment).
- It maximal length is 192 characters.

As long as the '$' prefix is used for the PartyName, any other full name schemes can be used: the static
`CoreApplicationIdentity.TryParseFullName` recovers the triplet (with nullable part and environment names)
from any potential full name.

```csharp
/// <summary>
/// Tries to parse a full name in which the $PartyName part can be anywhere (and optional)
/// and the #EnvironmentName part can be anywhere (and optional).
/// <para>
/// A simple domain name is a valid full name. 
/// </para>
/// </summary>
/// <param name="fullName">The full name to parse.</param>
/// <param name="domainName">The parsed domain name.</param>
/// <param name="partyName">The parsed party name without the leading '$' or null.</param>
/// <param name="environmentName">The parsed environment name or null (<see cref="DefaultEnvironmentName"/> can be used).</param>
/// <returns>True on success, false if the full name is not a valid identity full name (it must at least be a valid domain name).</returns>
public static bool TryParseFullName( ReadOnlySpan<char> fullName,
                                     [NotNullWhen( true )] out string? domainName,
                                     out string? partyName,
                                     out string? environmentName )

```

## Initialization and usage

The usage can hardly be simpler: `CoreApplicationIdentity.Instance` is a singleton that exposes `DomainName`,
`EnvironmentName`, `PartyName`, `ContextDescriptor`, `FullName`, `ContextualId` (a hash of the full name and the context descriptor),
`PartyContextualName` and `PartyInstanceName`. The `InstanceId` is static: it is available as soon as the process starts.

Initializations is a little bit trickier since this must be an immutable information: one shouldn't be able to use it before
it is built and one shouldn't modify it once built.

To configure the identity `Configure` or `TryConfigure` before its very first use (first call to `Instance` property) can be used:

```csharp
  /// <summary>
  /// Configure the application identity if it's not yet initialized or throws an <see cref="InvalidOperationException"/> otherwise.
  /// </summary>
  /// <param name="configurator">The configuration action.</param>
  /// <param name="initialize">Whether <see cref="Initialize()"/> should be called to lock the identity.</param>
  public static void Configure( Action<Builder> configurator, bool initialize = false )
  {
    // ...
  }

  /// <summary>
  /// Tries to configure the application identity if it's not yet initialized.
  /// </summary>
  /// <param name="configurator">The configuration action.</param>
  /// <returns>True if the <paramref name="configurator"/> has been called, false if the <see cref="Instance"/> is already available.</returns>
  public static bool TryConfigure( Action<Builder> configurator )
  {
    // ...
  }
``` 

With `initialize: true`, `Configure` calls `Initialize()` right after the configurator: the identity is built and locked
and the `OnInitialized` callbacks are called.

For code that wants to be safe regarding any deferred initialization of the identity, they can use the `IsInitialized` property
and/or the `OnInitialized` method that handles callbacks.

```csharp
  /// <summary>
  /// Gets whether the <see cref="Instance"/> has been initialized or
  /// can still be configured.
  /// </summary>
  public static bool IsInitialized { get; }

  /// <summary>
  /// Registers a callback that will be called when the <see cref="Instance"/> will be available
  /// or immediately if the instance has already been configured.
  /// </summary>
  /// <param name="action">Any action that requires the application's identity to be available.</param>
  public static void OnInitialized( Action action )
  {
    // ...
  }
```

Once `Instance` or `Initialize` is called the  `CoreApplicationIdentity.Instance` singleton is definitely locked.
