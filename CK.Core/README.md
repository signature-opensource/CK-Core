# CK.Core

This single assembly contains basic helpers and useful tools that should ideally not exist:
they (or their equivalent) should be in the .Net core framework.


## Throw and Guard
See [CK.Core/Throw](Throw/).

## Completable & Completion
See [CK.Core/Completable](Completable/).

## "Match and Forward" pattern
See [CK.Core/Matcher](Matcher/).

## CKBinaryReader/Writer, Simple/VersionedSerializable and IUtf8JsonWritable
See [CK.Core/SimpleSerialization](SimpleSerialization/) and [CK.Core/Json](Json/).

## CoreApplicationIdentity
See [CK.Core/CoreApplicationIdentity](CoreApplicationIdentity/).

## CKTrait

CKTrait handle the combination of different tags (strings) in a deterministic an thread safe manner. 
Traits are normalized and ordered strings combinations (*"Sql|DB access|Subscription" == "DB access|Sql|Subscription"* and *"DB access|Sql"* is greater than *"Sql"*):
a total order exists on the set of traits combinations based on lexicographical order for atomic
trait and the number of traits in a composite.
They support union, intersect, except and symmetric except in O(n).

Traits exist in a `CKTraitContext` that defines their separator (typically ',', '+' or '|') and,
thanks to their name, can be defined independently but resolves to the same context (this allows 
references to the same context to be defined and used transparently from totally independent modules/assemblies)

## DatetimeStamp

Very simple readonly struct that is a DateTime and a byte uniquifier.

## FastUniqueIdGenerator

Simple thread safe unique identifier generator with 64 bits (8 bytes) of entropy
that generates 11 characters long strings encoded in base 64 url.
Used as a very fast replacement of Guid (with less entropy but still enough for a lot
of usages).

## NormalizedPath

Immutable encapsulation of a `Path` string ('\\' are mapped to '/') and its `Parts` as an array of strings.
This implements a path closer to Unix than Windows (forward slashes '/' and case sensitivity) but works perfectly well
on Windows.
A NormalizedPath can be relative (supports '..' or '.' parts) or be rooted: 5 kind of
[roots](NormalizedPathRootKind.cs) are supported:
  - None (relative path)
  - '/' (RootedBySeparator), 
  - 'X:' or ':' or '~' (RootedByFirstPart), 
  - '//' (RootedByDoubleSeparator), 
  - or 'xx://' (RootedByURIScheme).

## SimpleServiceContainer

Very basic and simple `IServiceProvider` implementation.

## GrantLevel

[`GrantLevel`](GrantLevel.cs) is a `byte` scale that replaces a set of independent boolean rights
by a single ordered value. Instead of the traditional Resource/User/Right triplet with atomic and
orthogonal rights, an authorization becomes a single comparison against a level.

| Level | Value | The actor... |
|-------|------:|--------------|
| `Blind` | 0 | does not even know that the object exists. |
| `User` | 8 | can see the object names and may use the services it provides, but cannot see the object itself. |
| `Viewer` | 16 | can view the object but cannot interact with it. |
| `Contributor` | 32 | can contribute to the object but cannot modify the object itself. |
| `Editor` | 64 | can edit the standard properties, but maybe not the more sensitive ones such as the names. |
| `SuperEditor` | 80 | can edit the object, its names and any property, but not the security settings. |
| `SafeAdministrator` | 112 | can edit everything and change the security settings, but cannot destroy the object. |
| `Administrator` | 127 | has full control, including destruction. |

Only the two extremes are fundamental: 0 grants nothing, 127 grants everything. The levels in
between are a *standard* semantic that happens to cover a lot of classic scenarios with a single
level per resource - depending on the resource, most of them are useless and can be ignored. A
resource is free to give them another meaning, as long as the order is respected.

The type is a `byte` and the highest named value is 127, which leaves the range 128-255 unnamed. A
consumer of this enum uses that upper half to express blocking values - a deny - as `255 - level`.
Nothing in this package states that convention or enforces it: only the scale is defined here.

## Hash

`SHA1Value`, `SHA256Value` and `SHA512Value` encapsulate in a readonly struct
the hexadecimal string and the binary value of SHA values.

`HashStream` is a simple wrapper around an `IncrementalHash` instance to compute
the hash of a stream. It can be used in read or write mode and as a terminal stream
or as a decorator.

## ISystemClock

Yet another system clock. See https://github.com/dotnet/extensions/issues/151.

## What is not here

The README this file comes from carried a section on the "duck typed" contracts of the automatic
dependency injection, pointing at a `CK.Core/AutomaticDI/` folder. That folder does not exist and
`IAutoService` and its siblings are not declared in this assembly - they moved to another package.
The dead link is removed rather than repaired: it is not this package to document them.
