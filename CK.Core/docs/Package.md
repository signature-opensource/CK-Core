Basic helpers and tools that should ideally be in the .NET framework itself.

Main types: `NormalizedPath` (normalizes separators, exposes Parts, handles five kinds of root),
`CKTrait` (thread safe sets of immutable string tags with all set operations in O(n)),
`DateTimeStamp`, `GrantLevel`, `SimpleServiceContainer`, `HashStream` with the `SHA1Value`/`SHA256Value`/`SHA512Value`
value structs, and `ISystemClock`.

Also `Completable` and `Completion` - futures offering covariance of the result and extension points
to map exceptions or cancellation to regular results - and `CKBinaryReader`/`CKBinaryWriter`, which
extend the BCL readers and writers with nullable support, more standard types and optional value
sharing.
