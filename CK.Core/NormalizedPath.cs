using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Core;

/// <summary>
/// Immutable encapsulation of a path that normalizes <see cref="AltDirectorySeparatorChar"/> ('\')
/// to <see cref="DirectorySeparatorChar"/> ('/') and provides useful path manipulation methods.
/// This is the opposite of the Windows OS, but Windows handles the '/' transparently at more and more levels, 
/// and it's better to have a unified way to work with paths, regardless of the OS.
/// <para>
/// All comparisons uses <see cref="StringComparer.Ordinal"/>: this is fully compatible with case sensitive
/// file systems (typically the case of Unix-based OS). Windows' volumes are normally case insensitive but
/// using file names that differ only by case is not a good practice and this helper assumes this.
/// This struct is implicitly convertible to and from string.
/// </para>
/// </summary>
[DebuggerDisplay( "{Path}" )]
public readonly partial struct NormalizedPath : IEquatable<NormalizedPath>, IComparable<NormalizedPath>
{
    static readonly char[] _separators = new[] { AltDirectorySeparatorChar, DirectorySeparatorChar };

    readonly string[]? _parts;
    readonly string? _path;
    // Currently, _option is a NormalizedPathRootKind.
    // If other meta information must be handled, it should be
    // stored inside this option field. 
    readonly NormalizedPathRootKind _option;

    /// <summary>
    /// This is the always the '/' character. On Windows it is the "opposite" of
    /// the <see cref="System.IO.Path.DirectorySeparatorChar"/> but Windows now accepts
    /// the '/' and this is the sense of history, so we assume this choice by defining a
    /// definitive public const for our directory separator character, regardless of the
    /// platform.
    /// </summary>
    public const char DirectorySeparatorChar = '/';

    /// <summary>
    /// This is the '\' character, regardless of the
    /// platform.
    /// </summary>
    public const char AltDirectorySeparatorChar = '\\';

    /// <summary>
    /// Gets the <see cref="DirectorySeparatorChar"/> as a string.
    /// </summary>
    public const string DirectorySeparatorString = "/";

    /// <summary>
    /// Gets a double <see cref="DirectorySeparatorChar"/> string.
    /// </summary>
    public const string DoubleDirectorySeparatorString = "//";

    /// <summary>
    /// Gets the <see cref="AltDirectorySeparatorChar"/> as a string.
    /// </summary>
    public const string AltDirectorySeparatorString = "\\";

    /// <summary>
    /// Explicitly builds a new <see cref="NormalizedPath"/> struct from a string (that can be null or empty).
    /// </summary>
    /// <param name="path">The path as a string (can be null or empty).</param>
    public NormalizedPath( string? path )
    {
        _parts = path?.Split( _separators, StringSplitOptions.RemoveEmptyEntries );
        if( _parts == null || _parts.Length == 0 )
        {
            _parts = null;
            _path = String.Empty;
            _option = NormalizedPathRootKind.None;
            if( path != null && path.Length > 0 )
            {
                if( path[0] == DirectorySeparatorChar || path[0] == AltDirectorySeparatorChar )
                {
                    if( path.Length > 1
                        && (path[1] == DirectorySeparatorChar || path[1] == AltDirectorySeparatorChar) )
                    {
                        _path = DoubleDirectorySeparatorString;
                        _option = NormalizedPathRootKind.RootedByDoubleSeparator;
                    }
                    else
                    {
                        _path = DirectorySeparatorString;
                        _option = NormalizedPathRootKind.RootedBySeparator;
                    }
                }
            }
        }
        else
        {
            Debug.Assert( path != null );
            var c = path[0];
            if( c == DirectorySeparatorChar || c == AltDirectorySeparatorChar )
            {
                _path = DirectorySeparatorChar + _parts.Concatenate( DirectorySeparatorString );
                if( path.Length > 1 )
                {
                    c = path[1];
                    if( c == DirectorySeparatorChar || c == AltDirectorySeparatorChar )
                    {
                        _path = DirectorySeparatorChar + _path;
                        _option = NormalizedPathRootKind.RootedByDoubleSeparator;
                    }
                    else _option = NormalizedPathRootKind.RootedBySeparator;
                }
                else _option = NormalizedPathRootKind.None;
            }
            else
            {
                var first = _parts[0];
                Debug.Assert( first.Length > 0 );
                if( c == '~' && first.Length == 1 )
                {
                    _option = NormalizedPathRootKind.RootedByFirstPart;
                    _path = _parts.Concatenate( DirectorySeparatorString );
                }
                else
                {
                    if( first[^1] == ':' )
                    {
                        // To avoid errors and to not be too lax:
                        //  - Length = 1 or 2: "://" or "C://" are invalid (but ":/" or "C:/" are fine).
                        //  - Length > 2: "ni:/" is invalid (but "ni://" is fine).
                        if( first.Length <= 2 )
                        {
                            _option = NormalizedPathRootKind.RootedByFirstPart;
                        }
                        else
                        {
                            _option = NormalizedPathRootKind.RootedByURIScheme;
                            _parts[0] = first + DirectorySeparatorChar;
                        }
                        if( path.Length > first.Length )
                        {
                            bool hasDoubleSlash = false;
                            int doubleSlashPosLast = first.Length + 1;
                            if( path.Length > doubleSlashPosLast )
                            {
                                Debug.Assert( path[doubleSlashPosLast - 1] == DirectorySeparatorChar || path[doubleSlashPosLast - 1] == AltDirectorySeparatorChar );
                                var cS = path[doubleSlashPosLast];
                                hasDoubleSlash = cS == DirectorySeparatorChar || cS == AltDirectorySeparatorChar;
                            }
                            if( hasDoubleSlash && _option == NormalizedPathRootKind.RootedByFirstPart )
                            {
                                Throw.ArgumentException( nameof( path ), $"Invalid root path: '{first}' may be followed by one {DirectorySeparatorChar} but not two." );
                            }
                            if( !hasDoubleSlash && _option == NormalizedPathRootKind.RootedByURIScheme )
                            {
                                Throw.ArgumentException( nameof( path ), $"Invalid root path: '{first}', as a URI scheme, may be followed by two {DirectorySeparatorChar} but not by only one." );
                            }
                        }
                    }
                    else
                    {
                        _option = NormalizedPathRootKind.None;
                    }
                    _path = _parts.Concatenate( DirectorySeparatorString );
                    if( _option == NormalizedPathRootKind.RootedByURIScheme && _parts.Length == 1 ) _path += DirectorySeparatorChar;
                }
            }
        }
        Debug.Assert( _parts != null || _option != NormalizedPathRootKind.RootedByFirstPart, "parts == null ==> option != RootedByFirstPart" );
    }

    static string BuildNonEmptyPath( string[] parts, NormalizedPathRootKind o )
    {
        var path = parts.Concatenate( DirectorySeparatorString );
        return o switch
        {
            NormalizedPathRootKind.RootedBySeparator => DirectorySeparatorChar + path,
            NormalizedPathRootKind.RootedByDoubleSeparator => DoubleDirectorySeparatorString + path,
            _ => path,
        };
    }

    NormalizedPath( string[]? parts, string path, NormalizedPathRootKind o )
    {
        Throw.DebugAssert( path != null );
        Throw.DebugAssert( "parts != null ==> there are parts.", parts == null || parts.Length > 0 );
        Throw.DebugAssert( "parts == null ==> not RootedByFirstPart and not URI", parts != null || o is not NormalizedPathRootKind.RootedByFirstPart and not NormalizedPathRootKind.RootedByURIScheme );
        _parts = parts;
        _path = path;
        _option = o;
    }

    NormalizedPath( NormalizedPathRootKind o )
    {
        if( o == NormalizedPathRootKind.RootedByFirstPart || o == NormalizedPathRootKind.RootedByURIScheme )
        {
            o = NormalizedPathRootKind.None;
        }
        _parts = null;
        _path = o == NormalizedPathRootKind.RootedBySeparator
                ? DirectorySeparatorString
                : (o == NormalizedPathRootKind.RootedByDoubleSeparator
                   ? DoubleDirectorySeparatorString
                   : String.Empty);
        _option = o;
    }

    /// <summary>
    /// Implicitly converts a path to a normalized string path.
    /// </summary>
    /// <param name="path">The normalized path.</param>
    public static implicit operator string( NormalizedPath path ) => path._path ?? String.Empty;

    /// <summary>
    /// Implicitly converts a string to a <see cref="NormalizedPath"/>.
    /// </summary>
    /// <param name="path">The path as a string.</param>
    public static implicit operator NormalizedPath( string? path ) => new NormalizedPath( path );

    /// <summary>
    /// Gets whether this path is rooted.
    /// </summary>
    public bool IsRooted => _option != NormalizedPathRootKind.None;

    /// <summary>
    /// Gets this path's <see cref="NormalizedPathRootKind"/>.
    /// </summary>
    public NormalizedPathRootKind RootKind => _option;

    /// <summary>
    /// Gets the parent list from this up to the <see cref="FirstPart"/>.
    /// </summary>
    public IEnumerable<NormalizedPath> Parents
    {
        get
        {
            var p = this;
            while( !p.IsEmptyPath )
            {
                yield return p;
                p = p.RemoveLastPart();
            }
        }
    }

    /// <summary>
    /// Sets the <see cref="RootKind"/> by returning this or a new <see cref="NormalizedPath"/>.
    /// There are 2 forbidden cases: the target kind is <see cref="NormalizedPathRootKind.RootedByURIScheme"/>
    /// (this always results in an <see cref="ArgumentException"/>) or the target kind is
    /// <see cref="NormalizedPathRootKind.RootedByFirstPart"/> but <see cref="HasParts"/> is false (this throws an <see cref="ArgumentException"/>).
    /// </summary>
    /// <param name="kind">The <see cref="NormalizedPathRootKind"/> to set.</param>
    /// <returns>This or a new path.</returns>
    public NormalizedPath With( NormalizedPathRootKind kind )
    {
        if( kind == _option ) return this;
        Throw.CheckArgument( "Cannot change any existing path to be RootedByURIScheme.", kind != NormalizedPathRootKind.RootedByURIScheme );
        if( _parts == null )
        {
            switch( kind )
            {
                case NormalizedPathRootKind.None:
                {
                    Debug.Assert( _option == NormalizedPathRootKind.RootedBySeparator || _option == NormalizedPathRootKind.RootedByDoubleSeparator );
                    return new NormalizedPath();
                }
                case NormalizedPathRootKind.RootedByFirstPart:
                {
                    Throw.ArgumentException( "Invalid RootedByFirstPart on path without any parts." );
                    break;
                }
                case NormalizedPathRootKind.RootedBySeparator:
                {
                    return new NormalizedPath( kind );
                }
                case NormalizedPathRootKind.RootedByDoubleSeparator:
                {
                    return new NormalizedPath( kind );
                }
                default: Throw.NotSupportedException(); break;
            }
        }
        Debug.Assert( _path != null );
        if( _option == NormalizedPathRootKind.None || _option == NormalizedPathRootKind.RootedByFirstPart )
        {
            return kind switch
            {
                NormalizedPathRootKind.None or NormalizedPathRootKind.RootedByFirstPart => new NormalizedPath( _parts, _path, kind ),
                NormalizedPathRootKind.RootedBySeparator => new NormalizedPath( _parts, DirectorySeparatorChar + _path, kind ),
                NormalizedPathRootKind.RootedByDoubleSeparator => new NormalizedPath( _parts, DoubleDirectorySeparatorString + _path, kind ),
                _ => Throw.NotSupportedException<NormalizedPath>(),
            };
        }
        if( _option == NormalizedPathRootKind.RootedBySeparator || _option == NormalizedPathRootKind.RootedByDoubleSeparator )
        {
            return kind switch
            {
                NormalizedPathRootKind.None or NormalizedPathRootKind.RootedByFirstPart => new NormalizedPath( _parts, _path.Substring( _option == NormalizedPathRootKind.RootedBySeparator ? 1 : 2 ), kind ),
                NormalizedPathRootKind.RootedBySeparator => new NormalizedPath( _parts, _path.Substring( 1 ), kind ),
                NormalizedPathRootKind.RootedByDoubleSeparator => new NormalizedPath( _parts, DirectorySeparatorChar + _path, kind ),
                _ => Throw.NotSupportedException<NormalizedPath>(),
            };
        }
        Debug.Assert( _option == NormalizedPathRootKind.RootedByURIScheme );
        return RemoveFirstPart().With( kind );
    }

    /// <summary>
    /// Enumerates paths from this one up to the <see cref="FirstPart"/> with <paramref name="subPaths"/>
    /// and <paramref name="lastParts"/> cross combined and appended in order.
    /// Each result ends with one of the <paramref name="lastParts"/>: if <paramref name="lastParts"/> is empty,
    /// this enumeration is empty.
    /// </summary>
    /// <param name="subPaths">The sub paths that will be combined in order. Can be null or empty.</param>
    /// <param name="lastParts">
    /// The last parts that will be appended in order.
    /// Can not be null and should not be empty otherwise there will be no result at all.
    /// </param>
    /// <returns>
    /// All <see cref="Parents"/> with each <paramref name="subPaths"/> combined and
    /// each <paramref name="lastParts"/> appended.
    /// </returns>
    public IEnumerable<NormalizedPath> PathsToFirstPart( IEnumerable<NormalizedPath>? subPaths, IEnumerable<string> lastParts )
    {
        Throw.CheckArgument( lastParts != null );
        var p = this;
        if( subPaths != null && subPaths.Any() )
        {
            while( p.HasParts )
            {
                foreach( var sub in subPaths )
                {
                    var pSub = p.Combine( sub );
                    foreach( var last in lastParts )
                    {
                        yield return String.IsNullOrEmpty( last ) ? pSub : pSub.AppendPart( last );
                    }
                }
                p = p.RemoveLastPart();
            }
        }
        else
        {
            while( p.HasParts )
            {
                foreach( var last in lastParts )
                {
                    yield return String.IsNullOrEmpty( last ) ? p : p.AppendPart( last );
                }
                p = p.RemoveLastPart();
            }
        }
    }

    /// <summary>
    /// Returns a path where '.' and '..' parts are resolved under a root part.
    /// When <paramref name="throwOnAboveRoot"/> is true (the default), any '..' that would
    /// lead to a path above the root throws an <see cref="InvalidOperationException"/>.
    /// When false, the root acts as an absorbing element.
    /// </summary>
    /// <param name="rootPartsCount">
    /// By default, the resolution can reach the empty root.
    /// By specifying a positive number, any prefix length can be locked.
    /// Dotted parts in this locked prefix will be ignored and left as-is in the result.
    /// </param>
    /// <param name="throwOnAboveRoot">
    /// By default any attempt to resolve above the root will throw an <see cref="InvalidOperationException"/>.
    /// By specifying false, the root acts as an absorbing element.
    /// </param>
    /// <returns>The resolved normalized path.</returns>
    public NormalizedPath ResolveDots( int rootPartsCount = 0, bool throwOnAboveRoot = true )
    {
        int len = _parts != null ? _parts.Length : 0;
        if( rootPartsCount > len ) Throw.ArgumentOutOfRangeException( nameof( rootPartsCount ) );
        if( rootPartsCount == 0 && (_option == NormalizedPathRootKind.RootedByFirstPart || _option == NormalizedPathRootKind.RootedByURIScheme) )
        {
            rootPartsCount = 1;
        }
        if( rootPartsCount == len ) return this;
        Debug.Assert( !IsEmptyPath && _parts != null );
        string[]? newParts = null;
        int current = 0;
        NormalizedPathRootKind o = _option;
        for( int i = rootPartsCount; i < len; ++i )
        {
            string curPart = _parts[i];
            bool isDot = curPart == ".";
            bool isDotDot = !isDot && curPart == "..";
            if( isDot || isDotDot )
            {
                if( newParts == null )
                {
                    newParts = new string[_parts.Length];
                    current = i;
                    if( isDotDot ) --current;
                    if( current < rootPartsCount )
                    {
                        if( throwOnAboveRoot ) ThrowAboveRootException( _parts, rootPartsCount, i );
                        current = rootPartsCount;
                    }
                    Array.Copy( _parts, 0, newParts, 0, current );
                }
                else if( isDotDot )
                {
                    if( current == rootPartsCount )
                    {
                        if( throwOnAboveRoot ) ThrowAboveRootException( _parts, rootPartsCount, i );
                    }
                    else --current;
                }
            }
            else if( newParts != null )
            {
                newParts[current++] = curPart;
            }
        }
        if( newParts == null ) return this;
        if( current == 0 ) return new NormalizedPath( _option );
        Array.Resize( ref newParts, current );
        return new NormalizedPath( newParts, BuildNonEmptyPath( newParts, o ), o );
    }

    static void ThrowAboveRootException( string[] parts, int rootPartsCount, int iCulprit )
    {
        var msg = $"Path '{String.Join( DirectorySeparatorString, parts.Skip( iCulprit ) )}' must not resolve above root '{String.Join( DirectorySeparatorString, parts.Take( rootPartsCount ) )}'.";
        Throw.InvalidOperationException( msg );
    }

    /// <summary>
    /// Appends the given path to this one and returns a new <see cref="NormalizedPath"/>.
    /// Note that relative parts (. and ..) are not resolved by this method. If <paramref name="suffix"/> is rooted,
    /// the suffix is returned.
    /// </summary>
    /// <param name="suffix">The path to append.</param>
    /// <returns>The resulting path.</returns>
    public NormalizedPath Combine( NormalizedPath suffix )
    {
        if( suffix.IsRooted ) return suffix;
        if( suffix._parts == null ) return this;
        if( _parts == null )
        {
            Debug.Assert( _option != NormalizedPathRootKind.RootedByFirstPart );
            if( _option == NormalizedPathRootKind.None ) return suffix;
            var p = _option == NormalizedPathRootKind.RootedBySeparator
                    ? DirectorySeparatorChar + suffix.Path
                    : DoubleDirectorySeparatorString + suffix.Path;
            return new NormalizedPath( suffix._parts, p, _option );
        }
        var parts = new string[_parts.Length + suffix._parts.Length];
        Array.Copy( _parts, parts, _parts.Length );
        Array.Copy( suffix._parts, 0, parts, _parts.Length, suffix._parts.Length );
        return new NormalizedPath( parts, _path + DirectorySeparatorChar + suffix._path, _option );
    }

    /// <summary>
    /// Gets the last part of this path or the empty string if <see cref="IsEmptyPath"/> is true.
    /// </summary>
    public string LastPart => _parts?[^1] ?? String.Empty;

    /// <summary>
    /// Gets the first part of this path or the empty string if <see cref="IsEmptyPath"/> is true.
    /// </summary>
    public string FirstPart => _parts?[0] ?? String.Empty;

    /// <summary>
    /// Appends a part that must not contain <see cref="DirectorySeparatorChar"/>
    /// or <see cref="AltDirectorySeparatorChar"/> and returns a new <see cref="NormalizedPath"/>.
    /// When there is no <see cref="Parts"/> (this appends the first part), the part may contain separators so
    /// that <see cref="RootKind"/> is computed.
    /// </summary>
    /// <param name="part">The part to append. When null or empty, it is ignored.</param>
    /// <returns>A new <see cref="NormalizedPath"/>.</returns>
    public NormalizedPath AppendPart( string? part )
    {
        if( string.IsNullOrEmpty( part ) ) return this;
        if( _parts == null )
        {
            Throw.DebugAssert( _option != NormalizedPathRootKind.RootedByFirstPart );
            if( _option == NormalizedPathRootKind.None ) return new NormalizedPath( part );
            var p = _option == NormalizedPathRootKind.RootedBySeparator
                                ? DirectorySeparatorChar + part
                                : DoubleDirectorySeparatorString + part;
            return new NormalizedPath( p );
        }
        if( part.IndexOfAny( _separators ) >= 0 ) Throw.ArgumentException( nameof( part ), $"Illegal separators in '{part}'." );
        var parts = new string[_parts.Length + 1];
        Array.Copy( _parts, parts, _parts.Length );
        parts[_parts.Length] = part;
        return new NormalizedPath( parts, _path + DirectorySeparatorChar + part, _option );
    }

    /// <summary>
    /// Returns a new <see cref="NormalizedPath"/> with <see cref="LastPart"/> removed (or more).
    /// The <paramref name="count"/> must be between 0 and the number of <see cref="Parts"/>.
    /// </summary>
    /// <param name="count">Number of parts to remove. Must be between 0 and the number of parts.</param>
    /// <returns>A new path.</returns>
    public NormalizedPath RemoveLastPart( int count = 1 )
    {
        if( count <= 0 )
        {
            if( count == 0 ) return this;
            Throw.ArgumentOutOfRangeException( nameof( count ) );
        }
        if( _parts == null ) Throw.ArgumentOutOfRangeException( nameof( count ) );
        if( count >= _parts.Length )
        {
            if( count == _parts.Length ) return new NormalizedPath( _option );
            Throw.ArgumentOutOfRangeException( nameof( count ) );
        }
        Debug.Assert( _parts != null && _path != null );
        var parts = new string[_parts.Length - count];
        Array.Copy( _parts, parts, parts.Length );
        int len = _parts[^1].Length + count;
        while( count > 1 ) len += _parts[^(count--)].Length;
        return new NormalizedPath( parts, _path.Substring( 0, _path.Length - len ), _option );
    }

    /// <summary>
    /// Returns a new <see cref="NormalizedPath"/> with <see cref="FirstPart"/> removed (or more)
    /// and <see cref="RootKind"/> sets to <see cref="NormalizedPathRootKind.None"/>.
    /// Can be safely called when <see cref="IsEmptyPath"/> is true.
    /// </summary>
    /// <param name="count">Number of parts to remove. Must be positive.</param>
    /// <returns>A new path.</returns>
    public NormalizedPath RemoveFirstPart( int count = 1 )
    {
        if( count <= 0 )
        {
            if( count == 0 ) return this;
            Throw.ArgumentOutOfRangeException( nameof( count ) );
        }
        if( _parts == null )
        {
            if( count == 0 ) return this;
            Throw.ArgumentOutOfRangeException( nameof( count ) );
        }
        if( count >= _parts.Length )
        {
            if( count == _parts.Length ) return new NormalizedPath( _option );
            Throw.ArgumentOutOfRangeException( nameof( count ) );
        }
        var parts = new string[_parts.Length - count];
        Array.Copy( _parts, count, parts, 0, parts.Length );
        int len = _parts[0].Length + count;
        while( count > 1 ) len += _parts[--count].Length;
        Debug.Assert( _path != null );
        string p = _option switch
        {
            NormalizedPathRootKind.None or NormalizedPathRootKind.RootedByURIScheme or NormalizedPathRootKind.RootedByFirstPart
                => _path.Substring( len ),
            NormalizedPathRootKind.RootedBySeparator
                => _path.Substring( len + 1 ),
            NormalizedPathRootKind.RootedByDoubleSeparator
                => _path.Substring( len + 2 ),
            _ => Throw.NotSupportedException<string>(),
        };
        return new NormalizedPath( parts, p, NormalizedPathRootKind.None );
    }

    /// <summary>
    /// Removes one of the <see cref="Parts"/> and returns a new <see cref="NormalizedPath"/>.
    /// The <paramref name="index"/> must be valid otherwise a <see cref="ArgumentOutOfRangeException"/> will be thrown.
    /// </summary>
    /// <param name="index">Index of the part to remove.</param>
    /// <returns>A new path.</returns>
    public NormalizedPath RemovePart( int index ) => index == 0 ? RemoveFirstPart( 1 ) : RemoveParts( index, 1 );

    /// <summary>
    /// Removes some of the <see cref="Parts"/> and returns a new <see cref="NormalizedPath"/>.
    /// The <paramref name="startIndex"/> and <paramref name="count"/> must be valid
    /// otherwise a <see cref="ArgumentOutOfRangeException"/> will be thrown.
    /// </summary>
    /// <param name="startIndex">Starting index to remove.</param>
    /// <param name="count">Number of parts to remove (can be 0).</param>
    /// <returns>A new path.</returns>
    public NormalizedPath RemoveParts( int startIndex, int count )
    {
        int to = startIndex + count;
        Throw.CheckOutOfRangeArgument( _parts != null && startIndex >= 0 && to <= _parts.Length );
        if( count == 0 ) return this;
        if( startIndex == 0 ) return RemoveFirstPart( count );
        int nb = _parts.Length - count;
        if( nb == 0 ) return new NormalizedPath();
        Debug.Assert( _path != null );
        var parts = new string[nb];
        Array.Copy( _parts, parts, startIndex );
        int sIdx = startIndex, sLen = count;
        int tailCount = _parts.Length - to;
        if( tailCount != 0 ) Array.Copy( _parts, to, parts, startIndex, tailCount );
        else --sIdx;
        int i = 0;
        for( ; i < startIndex; ++i ) sIdx += _parts[i].Length;
        for( ; i < to; ++i ) sLen += _parts[i].Length;
        if( _option == NormalizedPathRootKind.RootedBySeparator ) ++sIdx;
        else if( _option == NormalizedPathRootKind.RootedByDoubleSeparator ) sIdx += 2;
        return new NormalizedPath( parts, _path.Remove( sIdx, sLen ), _option );
    }

    /// <summary>
    /// Tests whether this <see cref="NormalizedPath"/> starts with another one.
    /// </summary>
    /// <param name="other">The path that may be a prefix of this path.</param>
    /// <param name="strict">
    /// False to allow the other path to be the same as this one and to consider an empty other path as valid prefix.
    /// By default this path must be longer than the other one.</param>
    /// <returns>True if this path starts with the other one and the other one must not be empty.</returns>
    public bool StartsWith( NormalizedPath other, bool strict = true ) => (other.IsEmptyPath && !strict)
                                                    || (!other.IsEmptyPath
                                                        && !IsEmptyPath
                                                        && other._parts!.Length <= _parts!.Length
                                                        && (!strict || other._parts.Length < _parts.Length)
                                                        && StringComparer.Ordinal.Equals( other.LastPart, _parts[other._parts.Length - 1] )
                                                        && _path!.StartsWith( other._path!, StringComparison.Ordinal ));

    /// <summary>
    /// Reproduces <see cref="string.StartsWith(string)"/> on the <see cref="Path"/>.
    /// </summary>
    /// <param name="other">The path that may be a prefix of this <see cref="Path"/> string.</param>
    /// <param name="strict">
    /// True to force this path to be longer than the other one and the other one must not be empty.
    /// By default, the behavior of this method (with strict false) matches the <see cref="String.StartsWith(string)"/> behavior.
    /// </param>
    /// <returns>True if this path starts with the other one.</returns>
    public bool StartsWith( string other, bool strict = false ) => IsEmptyPath
                                                                    ? (!strict && (other == null || other.Length == 0))
                                                                    : (other == null || other.Length == 0)
                                                                        ? !strict
                                                                        : (_path!.Length > other.Length || (!strict && _path.Length == other.Length))
                                                                          && _path.StartsWith( other, StringComparison.Ordinal );

    /// <summary>
    /// Tests whether this <see cref="NormalizedPath"/> ends with another one.
    /// </summary>
    /// <param name="other">The path that may be a prefix of this path.</param>
    /// <param name="strict">
    /// False to allow the other path to be the same as this one.
    /// By default this path must be longer than the other one and the other one must not be empty.</param>
    /// <returns>True if this path ends with the other one.</returns>
    public bool EndsWith( NormalizedPath other, bool strict = true ) => (other.IsEmptyPath && !strict)
                                                    || (!other.IsEmptyPath
                                                        && !IsEmptyPath
                                                        && other._parts!.Length <= _parts!.Length
                                                        && (!strict || other._parts.Length < _parts.Length)
                                                        && StringComparer.Ordinal.Equals( other.FirstPart, _parts[^other._parts.Length] )
                                                        && _path!.EndsWith( other._path!, StringComparison.Ordinal ));

    /// <summary>
    /// Reproduces <see cref="string.EndsWith(string)"/> on the <see cref="Path"/>.
    /// </summary>
    /// <param name="other">The path that may end this path.</param>
    /// <param name="strict">
    /// True to force this path to be longer than the other one and the other one must not be empty.
    /// By default, the behavior of this method (with strict false) matches the <see cref="String.EndsWith(string)"/> behavior.
    /// </param>
    /// <returns>True if this path ends with the other one.</returns>
    public bool EndsWith( string other, bool strict = false ) => IsEmptyPath
                                                                    ? (!strict && (other == null || other.Length == 0))
                                                                    : (other == null || other.Length == 0)
                                                                        ? !strict
                                                                        : (_path!.Length > other.Length || (!strict && _path.Length == other.Length))
                                                                          && _path.EndsWith( other, StringComparison.Ordinal );

    /// <summary>
    /// Removes the prefix from this path. The prefix must starts with or be exactly the same as this one
    /// otherwise an <see cref="ArgumentException"/> is thrown.
    /// </summary>
    /// <param name="prefix">The prefix to remove.</param>
    /// <returns>A new path.</returns>
    public NormalizedPath RemovePrefix( NormalizedPath prefix )
    {
        Throw.CheckArgument( StartsWith( prefix, false ) );
        if( prefix._parts == null ) return this;
        Debug.Assert( _parts != null && _path != null && prefix._parts != null && prefix._path != null );
        int nb = _parts.Length - prefix._parts.Length;
        if( nb == 0 ) return new NormalizedPath();
        var parts = new string[nb];
        Array.Copy( _parts, prefix._parts.Length, parts, 0, nb );
        return new NormalizedPath( parts, _path.Substring( prefix._path.Length + 1 ), NormalizedPathRootKind.None );
    }

    /// <summary>
    /// Tries to compute the relative path from this one to the <paramref name="target"/>.
    /// This and <paramref name="target"/> must:
    /// <list type="bullet">
    ///     <item>Be absolute.</item>
    ///     <item>Have the same <see cref="RootKind"/>.</item>
    ///     <item>When <see cref="NormalizedPathRootKind.RootedByFirstPart"/>, their first part must the same.</item>
    ///     <item>When <see cref="NormalizedPathRootKind.RootedByURIScheme"/>, their 2 first parts must the same.</item>
    /// </list>
    /// <para>
    /// To compute a relative path between relative paths, they must first be rooted (typically by <see cref="NormalizedPathRootKind.RootedBySeparator"/>).
    /// </para>
    /// </summary>
    /// <param name="target">The target path.</param>
    /// <param name="relative">The relative path. Must be used only if true is returned.</param>
    /// <returns>True if the relative path can be computed.</returns>
    public bool TryGetRelativePathTo( NormalizedPath target, out NormalizedPath relative )
    {
        relative = default;
        var kind = RootKind;
        if( kind == NormalizedPathRootKind.None || kind != target.RootKind )
        {
            return false;
        }
        if( Equals( target ) ) return true;
        if( _parts == null )
        {
            // The kind cannot be RootedByURIScheme or NormalizedPathRootKind.RootedByFirstPart otherwise
            // at least one part would be there.
            Throw.DebugAssert( kind is not NormalizedPathRootKind.RootedByURIScheme and not NormalizedPathRootKind.RootedByFirstPart );
            // Source is a root, the relative is simply the relative target.
            Throw.DebugAssert( "Same root kind: they would have been equal.", target.HasParts );
            relative = target.With( NormalizedPathRootKind.None );
            return true;
        }
        if( target._parts == null )
        {
            // The kind cannot be RootedByURIScheme or NormalizedPathRootKind.RootedByFirstPart otherwise
            // at least one part would be there.
            Throw.DebugAssert( kind is not NormalizedPathRootKind.RootedByURIScheme and not NormalizedPathRootKind.RootedByFirstPart );
            // Target is the root: it is a ../../.. relative path.
            Throw.DebugAssert( "Same root kind: they would have been equal.", _parts != null );
            relative = DoCreateBackPath( _parts.Length );
            return true;
        }
        int maxCommon = Math.Min( _parts.Length, target._parts.Length );
        int common = 0;
        do
        {
            if( _parts[common] != target._parts[common] ) break;
            ++common;
        }
        while( common < maxCommon );

        int backCount = _parts.Length - common;
        relative = backCount == 0 ? default : DoCreateBackPath( backCount );

        if( target.RootKind is NormalizedPathRootKind.RootedByFirstPart )
        {
            // When the first part is the root, it must be the same.
            if( common == 0 ) return false;
            target = target.RemoveFirstPart();
            --common;
        }
        else if( target.RootKind is NormalizedPathRootKind.RootedByURIScheme )
        {
            // For URI the 2 first parts must be the same.
            if( common < 2 ) return false;
            target = target.RemoveFirstPart( 2 );
            common -= 2;
        }
        else
        {
            // For any other kind of root, we forget the root kind.
            target = target.With( NormalizedPathRootKind.None );
        }
        relative = relative.Combine( target.RemoveFirstPart( common ) );
        return true;

    }

    static NormalizedPath DoCreateBackPath( int backCount )
    {
        Throw.DebugAssert( backCount > 0 );
        var p = new string[backCount];
        for( int i = 0; i < backCount; i++ ) p[i] = "..";
        return new NormalizedPath( p, string.Join( DirectorySeparatorChar, p ), NormalizedPathRootKind.None );
    }

    /// <summary>
    /// Gets whether this is the empty path. A new <see cref="NormalizedPath"/>() (default constructor),
    /// <c>default(NormalizedPath)</c> or the empty string are empty.
    /// But "/" (<see cref="NormalizedPathRootKind.RootedBySeparator"/>) or
    /// "//" (<see cref="NormalizedPathRootKind.RootedByDoubleSeparator"/>) are not empty even if they
    /// have no <see cref="Parts"/>.
    /// </summary>
    public bool IsEmptyPath => _parts == null && _option == NormalizedPathRootKind.None;

    /// <summary>
    /// Gets whether this <see cref="NormalizedPath"/> has at least one <see cref="Parts"/>.
    /// </summary>
    public bool HasParts => _parts != null;

    /// <summary>
    /// Gets the parts that compose this <see cref="NormalizedPath"/>.
    /// </summary>
    public IReadOnlyList<string> Parts => _parts ?? Array.Empty<string>();

    /// <summary>
    /// Gets this path as a normalized string.
    /// </summary>
    public string Path => _path ?? String.Empty;

    /// <summary>
    /// Compares this path to another one.
    /// The <see cref="Parts"/> length is considered first and if they are equal, the
    /// two <see cref="Path"/> are compared using <see cref="StringComparer.Ordinal"/>.
    /// </summary>
    /// <param name="other">The path to compare to.</param>
    /// <returns>A positive integer if this is greater than other, a negative integer if this is lower than the other one and 0 if they are equal.</returns>
    public int CompareTo( NormalizedPath other )
    {
        if( _parts == null ) return other._parts == null ? _option.CompareTo( other._option ) : -1;
        if( other._parts == null ) return 1;
        int cmp = _parts.Length - other._parts.Length;
        return cmp != 0 ? cmp : StringComparer.Ordinal.Compare( _path, other._path );
    }

    /// <summary>
    /// Equality operator calls <see cref="Equals(NormalizedPath)"/>.
    /// </summary>
    /// <param name="p1">First path.</param>
    /// <param name="p2">Second path.</param>
    /// <returns>True if the two paths are equal.</returns>
    public static bool operator ==( NormalizedPath p1, NormalizedPath p2 ) => p1.Equals( p2 );

    /// <summary>
    /// Inequality operator.
    /// </summary>
    /// <param name="p1">First path.</param>
    /// <param name="p2">Second path.</param>
    /// <returns>True if the two paths are not equal.</returns>
    public static bool operator !=( NormalizedPath p1, NormalizedPath p2 ) => !p1.Equals( p2 );

    /// <summary>
    /// Comparison operator calls <see cref="CompareTo(NormalizedPath)"/>.
    /// </summary>
    /// <param name="p1">First path.</param>
    /// <param name="p2">Second path.</param>
    /// <returns>True p1 is greater than p2.</returns>
    public static bool operator >( NormalizedPath p1, NormalizedPath p2 ) => p1.CompareTo( p2 ) > 0;

    /// <summary>
    /// Comparison operator calls <see cref="CompareTo(NormalizedPath)"/>.
    /// </summary>
    /// <param name="p1">First path.</param>
    /// <param name="p2">Second path.</param>
    /// <returns>True if p1 is smaller than p2.</returns>
    public static bool operator <( NormalizedPath p1, NormalizedPath p2 ) => p1.CompareTo( p2 ) < 0;

    /// <summary>
    /// Comparison operator calls <see cref="CompareTo(NormalizedPath)"/>.
    /// </summary>
    /// <param name="p1">First path.</param>
    /// <param name="p2">Second path.</param>
    /// <returns>True if p1 is greater than or equal to p2.</returns>
    public static bool operator >=( NormalizedPath p1, NormalizedPath p2 ) => p1.CompareTo( p2 ) >= 0;

    /// <summary>
    /// Comparison operator calls <see cref="CompareTo(NormalizedPath)"/>.
    /// </summary>
    /// <param name="p1">First path.</param>
    /// <param name="p2">Second path.</param>
    /// <returns>True if p1 is less than or equal to p2.</returns>
    public static bool operator <=( NormalizedPath p1, NormalizedPath p2 ) => p1.CompareTo( p2 ) <= 0;

    /// <summary>
    /// Gets whether the <paramref name="obj"/> is a <see cref="NormalizedPath"/> that is equal to
    /// this one.
    /// Comparison is done by <see cref="StringComparer.Ordinal"/>.
    /// </summary>
    /// <param name="obj">The object to challenge.</param>
    /// <returns>True if they are equal, false otherwise.</returns>
    public override bool Equals( object? obj ) => obj is NormalizedPath p && Equals( p );

    /// <summary>
    /// Gets the hash code.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode( ToString() );

    /// <summary>
    /// Gets whether the other path is equal to this one.
    /// Comparison is done by <see cref="StringComparer.Ordinal"/>.
    /// </summary>
    /// <param name="other">The other path to challenge.</param>
    /// <returns>True if they are equal, false otherwise.</returns>
    public bool Equals( NormalizedPath other )
    {
        if( _parts == null ) return other._parts == null && _option == other._option;
        if( other._parts == null || _parts.Length != other._parts.Length ) return false;
        return StringComparer.Ordinal.Equals( _path, other._path );
    }

    /// <summary>
    /// Returns the string <see cref="Path"/>.
    /// </summary>
    /// <returns>The path as a string.</returns>
    public override string ToString() => _path ?? String.Empty;

    /// <summary>
    /// Returns a path with a specific character as the path separator instead of <see cref="DirectorySeparatorChar"/>.
    /// </summary>
    /// <param name="separator">The separator to use.</param>
    /// <returns>The path with the separator.</returns>
    public string ToString( char separator )
    {
        if( _path == null ) return String.Empty;
        Debug.Assert( _parts != null );
        if( separator == DirectorySeparatorChar || _parts.Length == 1 )
        {
            return _path;
        }
        return _path.Replace( DirectorySeparatorChar, separator );
    }
}
