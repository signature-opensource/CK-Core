using System;
using System.Text.RegularExpressions;

namespace CK.Core;

/// <summary>
/// Offers a global <see cref="Resolver"/> function that replaces <see cref="Type.GetType(string, bool)"/>. 
/// Default implementation is set to <see cref="WeakResolver(string, bool)"/>.
/// </summary>
public static class SimpleTypeFinder
{
    static Func<string, bool, Type?> _resolver = WeakResolver;
    static Func<string, bool, Type?> _coreResolver = Type.GetType;

    /// <summary>
    /// Gets or sets a global resolver. This resolver MUST always throw a <see cref="TypeLoadException"/> 
    /// when the boolean parameter is true: this is what <see cref="StandardResolver(string, bool)"/> do.
    /// Defaults to <see cref="WeakResolver(string, bool)"/>.
    /// </summary>
    public static Func<string, bool, Type?> Resolver
    {
        get { return _resolver; }
        set
        {
            if( value == null ) value = WeakResolver;
            _resolver = value;
        }
    }

    /// <summary>
    /// The <see cref="Type.GetType(string, bool)"/> function that <see cref="StandardResolver(string, bool)"/> 
    /// and <see cref="WeakResolver(string, bool)"/> use. Another function may be injected in advanced scenario if needed.
    /// </summary>
    public static Func<string, bool, Type?> RawGetType
    {
        get { return _coreResolver; }
        set
        {
            if( value == null ) value = Type.GetType;
            _coreResolver = value;
        }
    }

    /// <summary>
    /// An implementation of <see cref="Resolver"/> that can be used to load types regardless of 
    /// the version, culture, architecture and public key token (strongly-named assemblies) of the type names.
    /// Use <see cref="StandardResolver(string, bool)"/> on <see cref="TypeExtensions.GetWeakAssemblyQualifiedName(Type)"/>.
    /// </summary>
    /// <returns>The type or null if not found and <paramref name="throwOnError"/> is false.</returns>
    /// <exception cref="TypeLoadException">
    /// When <paramref name="throwOnError"/> is true, always throws a type load exception.
    /// The original error (not a <see cref="TypeLoadException"/>) is available in the <see cref="Exception.InnerException"/>.
    /// </exception>
    public static Type? WeakResolver( string assemblyQualifiedName, bool throwOnError )
    {
        return StandardResolver( TypeExtensions.WeakenAssemblyQualifiedName( assemblyQualifiedName ), throwOnError );
    }

    /// <summary>
    /// Direct implementation that checks that the assembly qualified name set as parameter is valid, 
    /// then calls <see cref="Type.GetType(string,bool)"/> and converts any exception that may be raised
    /// to <see cref="TypeLoadException"/>.
    /// </summary>
    /// <param name="assemblyQualifiedName">The assembly qualified name of a type.</param>
    /// <param name="throwOnError">
    /// True to ALWAYS throw a <see cref="TypeLoadException"/> if the type is not found.
    /// It may also throw <see cref="ArgumentNullException"/> and <see cref="ArgumentException"/> when the assembly qualified name is not valid
    /// False prevents any exception from being thrown and simply returns null.
    /// </param>
    /// <returns>The type or null if not found and <paramref name="throwOnError"/> is false.</returns>
    /// <exception cref="TypeLoadException">
    /// When <paramref name="throwOnError"/> is true, always throws a type load exception.
    /// The original error (not a <see cref="TypeLoadException"/>) is available in the <see cref="Exception.InnerException"/>.
    /// </exception>
    public static Type? StandardResolver( string assemblyQualifiedName, bool throwOnError )
    {
        if( throwOnError ) CheckAssemblyQualifiedNameValid( assemblyQualifiedName );
        try
        {
            return RawGetType( assemblyQualifiedName, throwOnError );
        }
        catch( Exception ex )
        {
            if( !throwOnError ) return null;
            if( ex is TypeLoadException ) throw;
            throw new TypeLoadException( $"An exception occurred while resolving type: {assemblyQualifiedName}.", ex );
        }
    }

    static void CheckAssemblyQualifiedNameValid( string assemblyQualifiedName )
    {
        Throw.CheckNotNullOrEmptyArgument( assemblyQualifiedName );
        Throw.CheckArgument( assemblyQualifiedName.Contains( ',' ) );
    }

    /// <summary>
    /// Helper method to split the assembly qualified name into its assembly name and full type name.
    /// "CK.Core.SimpleTypeFinder, CK.Core, version=1.0.0, culture='fr-FR'" gives "CK.Core.SimpleTypeFinder" and "CK.Core, version=1.0.0, culture='fr-FR'".
    /// </summary>
    /// <param name="assemblyQualifiedName">The assembly qualified name to split.</param>
    /// <param name="fullTypeName">Full type name on output or an empty string.</param>
    /// <param name="assemblyFullName">Weaken type name on output or an empty string.</param>
    /// <returns>True if the weakening has been successfully done. False otherwise.</returns>
    static public bool SplitAssemblyQualifiedName( string assemblyQualifiedName, out string fullTypeName, out string assemblyFullName )
    {
        int i = assemblyQualifiedName.IndexOf( ',' );
        if( i > 0 && i < assemblyQualifiedName.Length - 1 )
        {
            assemblyFullName = assemblyQualifiedName.Substring( Char.IsWhiteSpace( assemblyQualifiedName, i + 1 ) ? i + 2 : i + 1 ).Trim();
            fullTypeName = assemblyQualifiedName.Substring( 0, i ).Trim();
            return assemblyFullName.Length > 0 && fullTypeName.Length > 0;
        }
        assemblyFullName = fullTypeName = String.Empty;
        return false;
    }

    /// <summary>
    /// Helper method to split an assembly full name in two parts: 
    /// "CK.Core, version=1.0.0, culture='fr-FR'" gives "CK.Core" and "version=1.0.0, culture='fr-FR'".
    /// </summary>
    /// <param name="assemblyFullName">The assembly full name.</param>
    /// <param name="assemblyName">Set to assembly name only.</param>
    /// <param name="versionCultureAndPublicKeyToken">Set to extra information.</param>
    /// <returns>True if the split worked. False otherwise.</returns>
    static public bool SplitAssemblyFullName( string assemblyFullName, out string assemblyName, out string versionCultureAndPublicKeyToken )
    {
        versionCultureAndPublicKeyToken = assemblyName = String.Empty;
        int i = assemblyFullName.IndexOf( ',' );
        if( i < 0 ) assemblyName = assemblyFullName;
        else if( i > 0 )
        {
            if( i < assemblyFullName.Length - 1 )
            {
                versionCultureAndPublicKeyToken = assemblyFullName.Substring( Char.IsWhiteSpace( assemblyFullName, i + 1 ) ? i + 2 : i + 1 ).Trim();
            }
            assemblyName = assemblyFullName.Substring( 0, i ).Trim();
        }
        return assemblyName.Length > 0;
    }

}
