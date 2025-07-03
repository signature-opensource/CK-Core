using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CK.Core;

/// <summary>
/// Provides <see cref="ToCSharpName(Type?, bool, bool, bool)"/> extension method.
/// </summary>
/// <remarks>
/// A ConcurrentDictionary caches the computed strings.
/// </remarks>
public static partial class TypeExtensions
{
    /// <summary>
    /// A read only dictionary with the types that are aliased in C#.
    /// Contains <c>typeof(void)</c> and <c>typeof(object)</c>.
    /// </summary>
    public static readonly IReadOnlyDictionary<Type, string> TypeAliases = new Dictionary<Type, string>
    {
        { typeof( void ), "void" },
        { typeof( bool ), "bool" },
        { typeof( int ), "int" },
        { typeof( long ), "long" },
        { typeof( short ), "short" },
        { typeof( ushort ), "ushort" },
        { typeof( sbyte ), "sbyte" },
        { typeof( uint ), "uint" },
        { typeof( ulong ), "ulong" },
        { typeof( byte ), "byte" },
        { typeof( char ), "char" },
        { typeof( double ), "double" },
        { typeof( float ), "float" },
        { typeof( decimal ), "decimal" },
        { typeof( string ), "string" },
        { typeof( object ), "object" }
    };

    readonly struct KeyType : IEquatable<KeyType>
    {
        public readonly Type Type;
        public readonly int Flags;

        public bool Equals( KeyType other ) => Type == other.Type && Flags == other.Flags;

        public override int GetHashCode() => HashCode.Combine( Type, Flags );

        // ToCSharpName( withNamespace, typeDeclaration, useValueTupleParentheses )
        public KeyType( Type t, bool n, bool d, bool v )
        {
            Type = t;
            Flags = (n ? 1 : 0) | (d ? 8 : 0) | (v ? 256 : 0);
        }

        public override bool Equals( object? obj ) => obj is KeyType k && Equals( k );
    }

    static readonly ConcurrentDictionary<KeyType, string> _names = new ConcurrentDictionary<KeyType, string>();

    /// <summary>
    /// Gets the exact (and readable) C# type name. Handles generic definition (either opened or closed).
    /// <para>This can be called on a null reference: "null" is returned.</para>
    /// <para>
    /// The <paramref name="typeDeclaration"/> parameters applies to open generics:
    /// When true (the default), typeof( Dictionary&lt;,&gt;.KeyCollection )
    /// will result in "System.Collections.Generic.Dictionary&lt;TKey,TValue&gt;.KeyCollection".
    /// When sets to false, it will be "System.Collections.Generic.Dictionary&lt;,&gt;.KeyCollection".
    /// </para>
    /// </summary>
    /// <remarks>
    /// Value tuples are expressed (by default) with the (lovely,brackets) but can use the explicit type: <see cref="ValueTuple{T1, T2}"/>
    /// instead. This is mainly because of pattern matching limitations in (at least) C# 8 (i.e. netcoreapp3.1).
    /// </remarks>
    /// <param name="type">This type.</param>
    /// <param name="withNamespace">False to not include the namespaces.</param>
    /// <param name="typeDeclaration">False to not include generic parameter names in the output (typically for typeof syntax).</param>
    /// <param name="useValueTupleParentheses">False to use the (safer) "System.ValueTuple&lt;&gt;" instead of the (tuple, with, parentheses, syntax).</param>
    /// <returns>The C# type name.</returns>
    public static string ToCSharpName( this Type? type, bool withNamespace = true, bool typeDeclaration = true, bool useValueTupleParentheses = true )
    {
        if( type == null ) return "null";
        return _names.GetOrAdd( new KeyType( type, withNamespace, typeDeclaration, useValueTupleParentheses && !type.ContainsGenericParameters ),
                                k => AppendCSharpName( new StringBuilder(), k.Type, (k.Flags & 1) != 0, (k.Flags & 8) != 0, (k.Flags & 256) != 0 ).ToString() );
    }

    static StringBuilder AppendCSharpName( StringBuilder b, Type t, bool withNamespace, bool typeDeclaration, bool useValueTupleParentheses )
    {
        if( t.IsGenericParameter ) return typeDeclaration ? b.Append( t.Name ) : b;
        if( TypeAliases.TryGetValue( t, out var alias ) )
        {
            b.Append( alias );
            return b;
        }
        if( t.IsArray )
        {
            return b.Append( ToCSharpName( t.GetElementType()!, withNamespace, typeDeclaration, useValueTupleParentheses ) )
                    .Append( '[' ).Append( ',', t.GetArrayRank() - 1 ).Append( ']' );
        }
        if( t.IsByRef )
        {
            return b.Append( ToCSharpName( t.GetElementType()!, withNamespace, typeDeclaration, useValueTupleParentheses ) ).Append( '&' );
        }
        if( t.IsPointer )
        {
            int stars = 0;
            while( t.IsPointer )
            {
                stars++;
                t = t.GetElementType()!;
            }
            return b.Append( ToCSharpName( t, withNamespace, typeDeclaration, useValueTupleParentheses ) )
                    .Append( '*', stars );
        }
        var pathTypes = new Stack<Type>();
        pathTypes.Push( t );
        Type? decl = t.DeclaringType;
        while( decl != null )
        {
            pathTypes.Push( decl );
            decl = decl.DeclaringType;
        }
        var allGenArgs = new Queue<Type>( t.GetGenericArguments() );
        for( int iType = 0; pathTypes.Count > 0; iType++ )
        {
            Type theT = pathTypes.Pop();
            string n;
            if( iType == 0 ) n = withNamespace
                                    ? (theT.FullName ?? $"{theT.Namespace}.{theT.Name}")
                                    : theT.Name;
            else
            {
                n = theT.Name;
                b.Append( '.' );
            }
            int idxTick = n.IndexOf( '`', StringComparison.Ordinal ) + 1;
            if( idxTick > 0 )
            {
                int endNbParam = idxTick;
                while( endNbParam < n.Length && Char.IsDigit( n, endNbParam ) ) endNbParam++;
                int nbParams = int.Parse( n.AsSpan( idxTick, endNbParam - idxTick ), NumberStyles.Integer, NumberFormatInfo.InvariantInfo );
                Debug.Assert( nbParams > 0 );
                var tName = n.Substring( 0, idxTick - 1 );
                bool isValueTuple = tName == (iType == 0 && withNamespace ? "System.ValueTuple" : "ValueTuple");
                Type subType = allGenArgs.Dequeue();
                bool isNullableValue = !isValueTuple && tName == (iType == 0 && withNamespace ? "System.Nullable" : "Nullable") && !subType.IsGenericTypeParameter;
                if( isValueTuple && useValueTupleParentheses )
                {
                    b.Append( '(' );
                }
                else if( !isNullableValue )
                {
                    b.Append( tName );
                    b.Append( '<' );
                }
                --nbParams;
                int iGen = 0;
                for(; ; )
                {
                    if( iGen > 0 ) b.Append( ',' );
                    b.Append( ToCSharpName( subType, withNamespace, typeDeclaration, useValueTupleParentheses ) );
                    if( iGen++ == nbParams ) break;
                    subType = allGenArgs.Dequeue();
                    // Long Value Tuple handling here only if useValueTupleParentheses is true.
                    // This lift the rest content, skipping the rest 8th slot itself.
                    if( iGen == 7 && isValueTuple && useValueTupleParentheses )
                    {
                        Throw.DebugAssert( subType.Name.StartsWith( "ValueTuple", StringComparison.Ordinal ) );
                        Throw.DebugAssert( allGenArgs.Count == 0 );
                        var rest = subType.GetGenericArguments();
                        subType = rest[0];
                        nbParams = rest.Length - 1;
                        for( int i = 1; i < rest.Length; ++i ) allGenArgs.Enqueue( rest[i] );
                        iGen = 0;
                        b.Append( ',' );
                    }
                }
                b.Append( isNullableValue ? '?' : (isValueTuple && useValueTupleParentheses ? ')' : '>') );
            }
            else b.Append( n );
        }
        return b;
    }


    /// <summary>
    /// Gets the assembly qualified name without Culture, Version, PublicKeyToken, PublicKey and Custom assembly properties.
    /// See https://learn.microsoft.com/en-us/dotnet/fundamentals/reflection/specifying-fully-qualified-type-names.
    /// Simply calls <see cref="WeakenAssemblyQualifiedName(string)"/>.
    /// </summary>
    /// <param name="type">This type.</param>
    /// <returns>The type name without assembly properties or null if <see cref="Type.AssemblyQualifiedName"/> is null (generic parameters).</returns>
    public static string? GetWeakAssemblyQualifiedName( this Type type )
    {
        Throw.CheckNotNullArgument( type );
        var aqn = type.AssemblyQualifiedName;
        return aqn == null ? null : WeakenAssemblyQualifiedName( aqn );
    }

    /// <summary>
    /// Removes the Culture, Version, PublicKeyToken, PublicKey and Custom assembly properties from a <see cref="Type.AssemblyQualifiedName"/>.
    /// </summary>
    /// <param name="assemblyQualifiedName">The assembly qualified name.</param>
    /// <returns>The type name without assembly properties.</returns>
    public static string WeakenAssemblyQualifiedName( string assemblyQualifiedName ) => _assemblyProperties().Replace( assemblyQualifiedName, string.Empty );

    [GeneratedRegex( @",\s*(Culture|Version|PublicKey(Token)?|Custom)\s*=\s*[^]]*", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant )]
    private static partial Regex _assemblyProperties();

}
