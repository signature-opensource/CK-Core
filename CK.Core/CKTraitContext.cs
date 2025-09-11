using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

#nullable enable

namespace CK.Core;

/// <summary>
/// Thread-safe registration root for <see cref="CKTrait"/> objects.
/// Each context has a <see cref="Name"/> that uniquely, and definitely, identifies it when <see cref="IsShared"/> is true.
/// Shared contexts with the same name can be <see cref="Create"/>ed multiple times: as long as they define the same <see cref="Separator"/>,
/// they all are actually the exact same context. (If Separator differ during a redefinition, an <see cref="InvalidOperationException"/>
/// is thrown.)
/// </summary>
public sealed class CKTraitContext : IComparable<CKTraitContext>
{
    static int _nextIndependentIndex = 0;

    readonly Regex _canonize2;
    readonly ConcurrentDictionary<string, CKTrait> _tags;
    // We don't want to expose the concurrent dictionary content at any time with
    // a CKTrait that will not be the final, unique, one. As msdn states: "AddOrUpdate is not atomic with regards to
    // all other operations on the ConcurrentDictionary<TKey,TValue> class.": this could lead to duplicates to appear
    // in the wild. We use a stronger lock (with double check) when adding new CKtrait.
    readonly object _creationLock;
    readonly string _separatorString;
    readonly int _independentIndex;

    CKTraitContext( string name, char separator, bool shared, ICKBinaryReader? r )
    {
        Throw.CheckNotNullOrWhiteSpaceArgument( name );
        Name = name.Normalize();
        Separator = separator;
        if( !shared ) Monitor.Enter( _basicLock );
        var found = _regexes.FirstOrDefault( reg => reg.Key[0] == separator );
        if( found.Key == null )
        {
            _separatorString = new String( separator, 1 );
            string pattern = "(\\s*" + Regex.Escape( _separatorString ) + "\\s*)+";
            _canonize2 = new Regex( pattern, RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant );
            _regexes.Add( new KeyValuePair<string, Regex>( _separatorString, _canonize2 ) );
        }
        else
        {
            _separatorString = found.Key;
            _canonize2 = found.Value;
        }
        if( !shared ) Monitor.Exit( _basicLock );
        EmptyTrait = new CKTrait( this );
        if( r != null )
        {
            IEnumerable<KeyValuePair<string, CKTrait>> Read()
            {
                yield return new KeyValuePair<string, CKTrait>( String.Empty, EmptyTrait );
                int count = r.ReadInt32();
                for( int i = 0; i < count; ++i )
                {
                    var s = r.ReadString();
                    yield return new KeyValuePair<string, CKTrait>( s, new CKTrait( this, s ) );
                }
            }
            _tags = new ConcurrentDictionary<string, CKTrait>( Read(), StringComparer.Ordinal );
        }
        else
        {
            _tags = new ConcurrentDictionary<string, CKTrait>( StringComparer.Ordinal );
            _tags[String.Empty] = EmptyTrait;
        }
        EnumWithEmpty = new CKTrait[] { EmptyTrait };
        _creationLock = new object();
        _independentIndex = shared ? 0 : Interlocked.Increment( ref _nextIndependentIndex );
    }

    /// <summary>
    /// This is basic. And we don't really need more: CKTraitContext are typically created once, statically.
    /// The only "really dynamic" solicitation of this list will be during deserialization.
    /// </summary>
    static readonly List<CKTraitContext> _allContexts;
    static readonly List<KeyValuePair<string, Regex>> _regexes;
    static readonly object _basicLock;

    static CKTraitContext()
    {
        _allContexts = new List<CKTraitContext>();
        _regexes = new List<KeyValuePair<string, Regex>>();
        _basicLock = new object();
    }

    /// <summary>
    /// Initializes a new context for tags with the given separator.
    /// When <paramref name="shared"/> is true, if a context with the same name but a different separator
    /// has already been created, this raises a <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="name">Name for the context that identifies it. Must not be null nor whitespace.</param>
    /// <param name="separator">Separator (if it must differ from '|').</param>
    /// <param name="shared">False to create an independent context: other contexts with the same name coexist with this one.</param>
    public static CKTraitContext Create( string name, char separator = '|', bool shared = true )
    {
        return shared ? Bind( name, separator, null ) : new CKTraitContext( name, separator, false, null );
    }

    static CKTraitContext Bind( string name, char separator, ICKBinaryReader? tagReader )
    {
        CKTraitContext? c, exists;
        lock( _basicLock )
        {
            c = exists = _allContexts.FirstOrDefault( x => x.Name == name );
            if( exists == null ) _allContexts.Add( c = new CKTraitContext( name, separator, true, tagReader ) );
            Debug.Assert( c != null );
        }
        if( exists != null )
        {
            Debug.Assert( c != null );
            if( exists.Separator != separator )
            {
                Throw.InvalidOperationException( $"CKTraitContext named '{name}' is already defined with the separator '{exists.Separator}', it cannot be redefined with the separator '{separator}'." );
            }
            if( tagReader != null )
            {
                int count = tagReader.ReadInt32();
                while( --count >= 0 )
                {
                    c.FindOrCreateAtomicTrait( tagReader.ReadString(), true );
                }
            }
        }
        return c;
    }

    /// <summary>
    /// Reads a <see cref="CKTraitContext"/> that has been previously written by <see cref="Write"/>.
    /// </summary>
    /// <param name="r">The binary reader to use.</param>
    public static CKTraitContext Read( ICKBinaryReader r )
    {
        Throw.CheckNotNullArgument( r );
        byte vS = r.ReadByte();
        bool shared = (vS & 128) != 0;
        bool withTags = (vS & 64) != 0;

        var name = shared ? r.ReadSharedString()! : r.ReadString();
        var sep = r.ReadChar();
        var tagReader = withTags ? r : null;
        return shared ? Bind( name, sep, tagReader ) : new CKTraitContext( name, sep, false, tagReader );
    }

    /// <summary>
    /// Writes the <see cref="Name"/> and <see cref="Separator"/> so that <see cref="Read(ICKBinaryReader)"/> can
    /// rebuild or rebind to the context.
    /// </summary>
    /// <param name="w">The binary writer to use.</param>
    /// <param name="writeAllTags">True to write all existing tags.</param>
    public void Write( ICKBinaryWriter w, bool writeAllTags = false )
    {
        byte version = 0;
        if( IsShared ) version |= 128;
        if( writeAllTags ) version |= 64;
        w.Write( version );
        if( IsShared ) w.WriteSharedString( Name );
        else w.Write( Name );
        w.Write( Separator );
        if( writeAllTags )
        {
            // ConcurrentDictionary.Values is a snapshot in a ReadOnlyCollection<CKTrait> that wraps a List<CKTrait>.
            // Using Values means concretizing the list of all the traits where we only need atomic ones and internally locking
            // the dictionary.
            // Using the GetEnumerator has no lock.
            var atomics = new List<string>();
            foreach( var t in _tags )
            {
                if( t.Value.IsAtomic && !t.Value.IsEmpty ) atomics.Add( t.Key );
            }
            w.Write( atomics.Count );
            foreach( var s in atomics )
            {
                w.Write( s );
            }
        }
    }

    /// <summary>
    /// Gets the separator to use to separate combined tags. It is | by default.
    /// </summary>
    public char Separator { get; }

    /// <summary>
    /// Gets the name of this context.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets whether this context is shared.
    /// </summary>
    public bool IsShared => _independentIndex == 0;

    /// <summary>
    /// Gets the empty tag for this context. It corresponds to the empty string.
    /// </summary>
    public CKTrait EmptyTrait { get; }

    /// <summary>
    /// Obtains a <see cref="CKTrait"/> (either combined or atomic).
    /// </summary>
    /// <param name="tags">Atomic tag or tags separated by <see cref="Separator"/>.</param>
    /// <returns>A tag.</returns>
    public CKTrait FindOrCreate( string? tags ) => FindOrCreate( tags, true ) ?? EmptyTrait;

    /// <summary>
    /// Finds a <see cref="CKTrait"/> (either combined or atomic) only if all 
    /// of its atomic tags already exists: if any of the atomic tags are not already 
    /// registered, null is returned.
    /// </summary>
    /// <param name="tags">Atomic tag or tags separated by <see cref="Separator"/>.</param>
    /// <returns>A tag or null if the tag does not exists.</returns>
    public CKTrait? FindIfAllExist( string? tags ) => FindOrCreate( tags, false );

    /// <summary>
    /// Finds a <see cref="CKTrait"/> with only already existing atomic tags (null when not found).
    /// </summary>
    /// <param name="tags">Atomic tag or tags separated by <see cref="Separator"/>.</param>
    /// <param name="collector">Optional collector for unknown tag. As soon as the collector returns false, the process stops.</param>
    /// <returns>A tag that contains only already existing tag or null if none already exists.</returns>
    public CKTrait? FindOnlyExisting( string? tags, Func<string, bool>? collector = null )
    {
        if( tags == null || tags.Length == 0 ) return null;
        tags = tags.Normalize( NormalizationForm.FormC );
        if( !_tags.TryGetValue( tags, out CKTrait? m ) )
        {
            string[] splitTags = SplitMultiTag( tags, out int tagCount );
            if( tagCount <= 0 ) return null;
            if( tagCount == 1 )
            {
                m = FindOrCreateAtomicTrait( splitTags[0], false );
            }
            else
            {
                tags = String.Join( _separatorString, splitTags, 0, tagCount );
                if( !_tags.TryGetValue( tags, out m ) )
                {
                    var atomics = new List<CKTrait>();
                    for( int i = 0; i < tagCount; ++i )
                    {
                        CKTrait? tag = FindOrCreateAtomicTrait( splitTags[i], false );
                        if( tag == null )
                        {
                            if( collector != null && !collector( splitTags[i] ) ) break;
                        }
                        else atomics.Add( tag );
                    }
                    if( atomics.Count != 0 )
                    {
                        tags = String.Join( _separatorString, atomics );
                        if( !_tags.TryGetValue( tags, out m ) )
                        {
                            lock( _creationLock )
                            {
                                if( !_tags.TryGetValue( tags, out m ) )
                                {
                                    m = new CKTrait( this, tags, atomics.ToArray() );
                                    _tags[tags] = m;
                                }
                            }
                        }
                    }
                }
            }
        }
        return m;
    }

    /// <summary>
    /// Compares this context to another one. The keys are <see cref="Separator"/>, then <see cref="Name"/>.
    /// </summary>
    /// <param name="other">Context to compare.</param>
    /// <returns>0 for the exact same context, greater/lower than 0 otherwise.</returns>
    public int CompareTo( CKTraitContext? other )
    {
        if( other == null ) return 1;
        if( ReferenceEquals( this, other ) ) return 0;
        int cmp = Separator - other.Separator;
        if( cmp == 0 )
        {
            cmp = StringComparer.Ordinal.Compare( Name, other.Name );
            if( cmp == 0 )
            {
                Debug.Assert( _independentIndex != 0 );
                cmp = _independentIndex - other._independentIndex;
            }
        }
        return cmp;
    }

    /// <summary>
    /// Overridden to return the <see cref="Name"/> and <see cref="Separator"/>.
    /// </summary>
    /// <returns>A readable string.</returns>
    public override string ToString() => $"CKTraitContext {Name} '{Separator}'";

    /// <summary>
    /// Gets the fallback for empty and atomic tags.
    /// </summary>
    internal IEnumerable<CKTrait> EnumWithEmpty { get; }

    CKTrait? FindOrCreate( string? tags, bool create )
    {
        if( tags == null || tags.Length == 0 ) return EmptyTrait;
        tags = tags.Normalize();
        if( tags.IndexOfAny( new[] { '\n', '\r' } ) >= 0 ) Throw.ArgumentException( Impl.CoreResources.TagsMustNotBeMultiLineString );
        if( !_tags.TryGetValue( tags, out CKTrait? m ) )
        {
            string[] splitTags = SplitMultiTag( tags, out int tagCount );
            if( tagCount <= 0 ) return EmptyTrait;
            if( tagCount == 1 )
            {
                m = FindOrCreateAtomicTrait( splitTags[0], create );
            }
            else
            {
                tags = String.Join( _separatorString, splitTags, 0, tagCount );
                if( !_tags.TryGetValue( tags, out m ) )
                {
                    CKTrait[] atomics = new CKTrait[tagCount];
                    for( int i = 0; i < tagCount; ++i )
                    {
                        CKTrait? tag = FindOrCreateAtomicTrait( splitTags[i], create );
                        if( tag == null ) return null;
                        atomics[i] = tag;
                    }
                    lock( _creationLock )
                    {
                        if( !_tags.TryGetValue( tags, out m ) )
                        {
                            m = new CKTrait( this, tags, atomics );
                            _tags[tags] = m;
                        }
                    }
                }
                Debug.Assert( !m.IsAtomic && m.AtomicTraits.Count == tagCount, "Combined tag." );
            }
        }
        return m;
    }


    /// <summary>
    /// Obtains a tag from a list of atomic (already sorted) tags.
    /// Used by the Add, Toggle, Remove, Intersect methods.
    /// </summary>
    internal CKTrait FindOrCreateFromAtomicSortedList( List<CKTrait> atomicTags )
    {
        if( atomicTags.Count == 0 ) return EmptyTrait;
        Debug.Assert( atomicTags[0].Context == this, "This is one of our tags." );
        Debug.Assert( atomicTags[0].AtomicTraits.Count == 1, "This is an atomic tag and not the empty one." );
        if( atomicTags.Count == 1 ) return atomicTags[0];
        var b = new StringBuilder( atomicTags[0].ToString() );
        for( int i = 1; i < atomicTags.Count; ++i )
        {
            Debug.Assert( atomicTags[i].Context == this, "This is one of our tags." );
            Debug.Assert( atomicTags[i].AtomicTraits.Count == 1, "This is an atomic tag and not the empty one." );
            Debug.Assert( StringComparer.Ordinal.Compare( atomicTags[i - 1].ToString(), atomicTags[i].ToString() ) < 0,
                "Tags are already sorted and NO DUPLICATES exist." );
            b.Append( Separator ).Append( atomicTags[i].ToString() );
        }
        string tags = b.ToString();
        if( !_tags.TryGetValue( tags, out CKTrait? m ) )
        {
            lock( _creationLock )
            {
                if( !_tags.TryGetValue( tags, out m ) )
                {
                    m = new CKTrait( this, tags, atomicTags.ToArray() );
                    _tags[tags] = m;
                }
            }
        }
        return m;
    }

    CKTrait? FindOrCreateAtomicTrait( string tag, bool create )
    {
        if( !_tags.TryGetValue( tag, out CKTrait? m ) && create )
        {
            lock( _creationLock )
            {
                if( !_tags.TryGetValue( tag, out m ) )
                {
                    m = new CKTrait( this, tag );
                    _tags[tag] = m;
                }
            }
            Debug.Assert( m != null && m.IsAtomic, "Special construction for atomic tags." );
        }
        return m;
    }

    /// <summary>
    /// Obtains a tag from a list of atomic (already sorted) tags.
    /// Used by fall back generation.
    /// </summary>
    internal CKTrait FindOrCreate( CKTrait[] atomicTags, int count )
    {
        Debug.Assert( count > 1, "Atomic tags are handled directly." );

        Debug.Assert( !Array.Exists( atomicTags, mA => mA.Context != this || mA.AtomicTraits.Count != 1 ), "Tags are from this Context and they are atomic and not empty." );

        var b = new StringBuilder( atomicTags[0].ToString() );
        for( int i = 1; i < count; ++i )
        {
            Debug.Assert( StringComparer.Ordinal.Compare( atomicTags[i - 1].ToString(), atomicTags[i].ToString() ) < 0, "Tags are already sorted and NO DUPLICATE exists." );
            b.Append( Separator ).Append( atomicTags[i].ToString() );
        }
        string tags = b.ToString();
        if( !_tags.TryGetValue( tags, out CKTrait? m ) )
        {
            // We must clone the array since fall backs generation reuses it.
            if( atomicTags.Length != count )
            {
                CKTrait[] subArray = new CKTrait[count];
                Array.Copy( atomicTags, subArray, count );
                atomicTags = subArray;
            }
            else atomicTags = (CKTrait[])atomicTags.Clone();
            lock( _creationLock )
            {
                if( !_tags.TryGetValue( tags, out m ) )
                {
                    m = new CKTrait( this, tags, atomicTags );
                    _tags[tags] = m;
                }
            }
        }
        return m;
    }

    string[] SplitMultiTag( string s, out int count )
    {
        string[] tags = _canonize2.Split( s.Trim() );
        count = tags.Length;
        Debug.Assert( count != 0, "Split always create a cell." );
        int i = tags[0].Length == 0 ? 1 : 0;
        // Special handling for first and last slots if they are empty.
        if( tags[count - 1].Length == 0 )
        {
            count = count - 1 - i;
        }
        else
        {
            count -= i;
        }
        if( count != tags.Length )
        {
            if( count <= 0 ) return Array.Empty<string>();
            string[] m = new string[count];
            Array.Copy( tags, i, m, 0, count );
            tags = m;
        }
        // Sort if necessary (more than one atomic tag).
        if( count > 1 )
        {
            Array.Sort( tags, StringComparer.Ordinal );
            // And removes duplicates. Since this occur very rarely
            // and that count is small we use a O(n) process that shifts
            // the tags array.
            i = count - 1;
            string last = tags[i];
            while( --i >= 0 )
            {
                Debug.Assert( last.Length > 0, "There is no empty strings." );
                string cur = tags[i];
                if( StringComparer.Ordinal.Equals( cur, last ) )
                {
                    int delta = (--count) - i - 1;
                    if( delta > 0 )
                    {
                        Array.Copy( tags, i + 2, tags, i + 1, delta );
                    }
                }
                last = cur;
            }
        }
        return tags;
    }

}
