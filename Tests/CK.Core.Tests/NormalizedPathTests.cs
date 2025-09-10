using Shouldly;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Core.Tests;

[TestFixture]
public class NormalizedPathTests
{
    [TestCase( "", NormalizedPathRootKind.None, "" )]
    [TestCase( "a", NormalizedPathRootKind.None, "a" )]
    [TestCase( "./", NormalizedPathRootKind.None, "." )]
    [TestCase( ".", NormalizedPathRootKind.None, "." )]
    [TestCase( "./a", NormalizedPathRootKind.None, "./a" )]
    [TestCase( "/a", NormalizedPathRootKind.RootedBySeparator, "/a" )]
    [TestCase( "/a/b", NormalizedPathRootKind.RootedBySeparator, "/a/b" )]
    [TestCase( "/", NormalizedPathRootKind.RootedBySeparator, "/" )]
    [TestCase( "//a", NormalizedPathRootKind.RootedByDoubleSeparator, "//a" )]
    [TestCase( "//a/b", NormalizedPathRootKind.RootedByDoubleSeparator, "//a/b" )]
    [TestCase( "//", NormalizedPathRootKind.RootedByDoubleSeparator, "//" )]
    [TestCase( "c:/", NormalizedPathRootKind.RootedByFirstPart, "c:" )]
    [TestCase( "X:", NormalizedPathRootKind.RootedByFirstPart, "X:" )]
    [TestCase( "~", NormalizedPathRootKind.RootedByFirstPart, "~" )]
    [TestCase( "~/", NormalizedPathRootKind.RootedByFirstPart, "~" )]
    [TestCase( "~/a", NormalizedPathRootKind.RootedByFirstPart, "~/a" )]
    [TestCase( ":", NormalizedPathRootKind.RootedByFirstPart, ":" )]
    [TestCase( ":/", NormalizedPathRootKind.RootedByFirstPart, ":" )]
    [TestCase( ":/A", NormalizedPathRootKind.RootedByFirstPart, ":/A" )]
    [TestCase( "plop:", NormalizedPathRootKind.RootedByURIScheme, "plop://" )]
    [TestCase( "http://", NormalizedPathRootKind.RootedByURIScheme, "http://" )]
    [TestCase( "https://co.co/me", NormalizedPathRootKind.RootedByURIScheme, "https://co.co/me" )]
    [TestCase( "xrq:\\/nimp", NormalizedPathRootKind.RootedByURIScheme, "xrq://nimp" )]
    [TestCase( "xrq:\\\\nimp", NormalizedPathRootKind.RootedByURIScheme, "xrq://nimp" )]
    [TestCase( "xrq:/\\nimp", NormalizedPathRootKind.RootedByURIScheme, "xrq://nimp" )]
    [TestCase( "xrq:\\/", NormalizedPathRootKind.RootedByURIScheme, "xrq://" )]
    [TestCase( "xrq:\\\\", NormalizedPathRootKind.RootedByURIScheme, "xrq://" )]
    [TestCase( "xrq:/\\", NormalizedPathRootKind.RootedByURIScheme, "xrq://" )]
    public void all_kind_of_root( string p, NormalizedPathRootKind o, string path )
    {
        var n = new NormalizedPath( p );
        n.RootKind.ShouldBe( o );
        n.Path.ShouldBe( path );
    }

    [TestCase( "c:", NormalizedPathRootKind.None, "c:" )]
    [TestCase( "/a", NormalizedPathRootKind.None, "a" )]
    [TestCase( "//a", NormalizedPathRootKind.None, "a" )]
    [TestCase( "~a", NormalizedPathRootKind.None, "~a" )]

    [TestCase( "", NormalizedPathRootKind.RootedByFirstPart, "ArgumentException" )]
    [TestCase( "/", NormalizedPathRootKind.RootedByFirstPart, "ArgumentException" )]
    [TestCase( "//", NormalizedPathRootKind.RootedByFirstPart, "ArgumentException" )]

    [TestCase( "", NormalizedPathRootKind.RootedBySeparator, "/" )]
    [TestCase( "", NormalizedPathRootKind.RootedByDoubleSeparator, "//" )]
    [TestCase( "c:", NormalizedPathRootKind.RootedByFirstPart, "c:" )]
    [TestCase( "c:", NormalizedPathRootKind.RootedBySeparator, "/c:" )]
    [TestCase( "c:", NormalizedPathRootKind.RootedByDoubleSeparator, "//c:" )]

    [TestCase( "a", NormalizedPathRootKind.RootedByFirstPart, "a" )]
    [TestCase( "/a", NormalizedPathRootKind.RootedByFirstPart, "a" )]
    [TestCase( "//a", NormalizedPathRootKind.RootedByFirstPart, "a" )]
    [TestCase( "a", NormalizedPathRootKind.RootedBySeparator, "/a" )]
    [TestCase( "a", NormalizedPathRootKind.RootedByDoubleSeparator, "//a" )]
    [TestCase( "//a", NormalizedPathRootKind.RootedBySeparator, "/a" )]
    [TestCase( "/a", NormalizedPathRootKind.RootedByDoubleSeparator, "//a" )]
    [TestCase( "/~a", NormalizedPathRootKind.RootedByDoubleSeparator, "//~a" )]
    [TestCase( "~a", NormalizedPathRootKind.RootedByDoubleSeparator, "//~a" )]

    [TestCase( "http://", NormalizedPathRootKind.RootedByFirstPart, "ArgumentException" )]
    [TestCase( "http://co.co", NormalizedPathRootKind.RootedByFirstPart, "co.co" )]
    [TestCase( "http://", NormalizedPathRootKind.None, "" )]
    [TestCase( "http://co.co", NormalizedPathRootKind.None, "co.co" )]
    [TestCase( "http://co.co", NormalizedPathRootKind.RootedBySeparator, "/co.co" )]
    [TestCase( "http://", NormalizedPathRootKind.RootedBySeparator, "/" )]
    [TestCase( "http://co.co", NormalizedPathRootKind.RootedByDoubleSeparator, "//co.co" )]
    [TestCase( "http://", NormalizedPathRootKind.RootedByDoubleSeparator, "//" )]
    public void changing_RootKind( string p, NormalizedPathRootKind newKind, string result )
    {
        if( result == "ArgumentException" )
        {
            Should.Throw<ArgumentException>( () => new NormalizedPath( p ).With( newKind ) );

        }
        else
        {
            var r = new NormalizedPath( p ).With( newKind );
            r.RootKind.ShouldBe( newKind );
            r.ShouldBe( new NormalizedPath( result ) );
        }
    }

    [TestCase( "", '=', "" )]
    [TestCase( null, '=', null )]
    [TestCase( "", '=', null )]
    [TestCase( null, '=', "" )]
    [TestCase( "", '<', "a" )]
    [TestCase( "A", '<', "a" )]
    [TestCase( "", '<', "/" )]
    [TestCase( "", '<', "//" )]
    [TestCase( "/", '<', "//" )]
    [TestCase( "/", '<', "/a" )]
    [TestCase( "//", '<', "/a" )]
    [TestCase( "/", '<', "a" )]
    [TestCase( "//", '<', "a" )]
    [TestCase( "a", '=', "a" )]
    [TestCase( "a/b", '>', "a" )]
    [TestCase( "A/B", '<', "a/B" )]
    [TestCase( "a/1", '=', "a/1" )]
    [TestCase( "a/1a", '>', "a/1" )]
    [TestCase( "a/1/b", '<', "a/1/c" )]
    [TestCase( "z", '>', "a" )]
    [TestCase( "z", '<', "a/b" )]
    [TestCase( "z:", '=', "z:/" )]
    [TestCase( "git:", '=', "git://" )]
    [TestCase( "git:\\\\", '=', "git://" )]
    [TestCase( "/A", '<', "/B" )]
    public void equality_and_comparison_operators_at_work( string p1, char op, string p2 )
    {
        NormalizedPath n1 = p1;
        NormalizedPath n2 = p2;
        if( op == '=' )
        {
            n1.Equals( n2 ).ShouldBeTrue();
            (n1 == n2).ShouldBeTrue();
            (n1 != n2).ShouldBeFalse();
            (n1 <= n2).ShouldBeTrue();
            (n1 < n2).ShouldBeFalse();
            (n1 >= n2).ShouldBeTrue();
            (n1 > n2).ShouldBeFalse();
        }
        else
        {
            bool isGT = op == '>';
            n1.Equals( n2 ).ShouldBeFalse();
            (n1 == n2).ShouldBeFalse();
            (n1 != n2).ShouldBeTrue();
            (n1 <= n2).ShouldBe( !isGT );
            (n1 < n2).ShouldBe( !isGT );
            (n1 >= n2).ShouldBe( isGT );
            (n1 > n2).ShouldBe( isGT );
        }
    }

    [TestCase( "", "", false )]
    [TestCase( null, null, false )]
    [TestCase( "", "a", false )]
    [TestCase( "a", null, false )]
    [TestCase( "a", "", false )]
    [TestCase( "a", "a", false )]
    [TestCase( "a/b", "a", true )]
    [TestCase( "a/bab", "a/ba", false )]
    [TestCase( "/a/b", "a", false )]
    [TestCase( "/a/b", "/a", true )]
    [TestCase( "a\\b", "a/b", false )]
    [TestCase( "a\\bi", "a/b", false )]
    [TestCase( "a/b/c/", "a\\b", true )]
    [TestCase( "//A/B/c/", "\\\\A\\B", true )]
    [TestCase( "/a/b/c/", "a/b", false )]
    [TestCase( "a/b/c/", "a\\bc", false )]
    public void StartsWith_Path_is_strict_by_default( string start, string with, bool resultPath )
    {
        var path = new NormalizedPath( start );
        var prefix = new NormalizedPath( with );
        path.StartsWith( prefix )
            .ShouldBe( resultPath, resultPath ? "Path should Start." : "Path should NOT Start." );
        if( resultPath )
        {
            var suffix = path.RemovePrefix( with );
            var back = prefix.Combine( suffix );
            back.ShouldBe( path );
        }
    }

    [TestCase( "", "", true )]
    [TestCase( null, null, true )]
    [TestCase( "", "a", false )]
    [TestCase( "a", null, true )]
    [TestCase( "a", "", true )]
    [TestCase( "a", "a", true )]
    [TestCase( "a/b", "a", true )]
    [TestCase( "a/bab", "a/ba", true )]
    [TestCase( "/a/b", "a", false )]
    [TestCase( "/a/b", "/a", true )]
    [TestCase( "/a/b", "/a/", true )]
    [TestCase( "a\\b", "a/b", true )]
    [TestCase( "a\\bi", "a/b", true )]
    [TestCase( "a/b/c/", "a\\b", false )]
    [TestCase( "//A/B/c/", "\\\\A\\B", false )]
    [TestCase( "/a/b/c/", "a/b", false )]
    [TestCase( "a/b/c/", "a\\bc", false )]
    public void StartsWith_String_is_NOT_strict_by_default( string start, string with, bool resultString )
    {
        var path = new NormalizedPath( start );
        path.StartsWith( with )
            .ShouldBe( resultString, resultString ? "String should Start." : "String should NOT Start." );
    }

    [TestCase( "", "", true )]
    [TestCase( null, null, true )]
    [TestCase( "", "a", false )]
    [TestCase( "a", "", true )]
    [TestCase( "a", null, true )]
    [TestCase( "a", "a", true )]
    [TestCase( "a/b", "a", true )]
    [TestCase( "a\\b", "a/b", true )]
    [TestCase( "a\\bi", "a/b", false )]
    [TestCase( "a/b/c/", "a\\b", true )]
    [TestCase( "a/b/c/", "a\\bc", false )]
    public void StartsWith_Path_NOT_strict_at_work( string start, string with, bool resultPath )
    {
        var path = new NormalizedPath( start );
        path.StartsWith( new NormalizedPath( with ), strict: false )
            .ShouldBe( resultPath, resultPath ? "Path should Start." : "Path should NOT Start." );
    }

    [TestCase( "", "", false )]
    [TestCase( null, null, false )]
    [TestCase( "", "a", false )]
    [TestCase( "a", "", false )]
    [TestCase( "a", null, false )]
    [TestCase( "a", "a", false )]
    [TestCase( "a/b", "a", true )]
    [TestCase( "a\\b", "a/b", false )]
    [TestCase( "a\\bi", "a/b", true )]
    [TestCase( "a/b/c/", "a\\b", false )]
    [TestCase( "a/b/c/", "a\\bc", false )]
    public void StartsWith_String_STRICT_at_work( string start, string with, bool resultString )
    {
        new NormalizedPath( start ).StartsWith( with, strict: true )
            .ShouldBe( resultString, resultString ? "String should Start." : "String should NOT Start." );
    }

    [TestCase( "", "", false )]
    [TestCase( null, null, false )]
    [TestCase( "", "a", false )]
    [TestCase( "a", "a", false )]
    [TestCase( "a", "", false )]
    [TestCase( "a", null, false )]
    [TestCase( "a/b", "b", true )]
    [TestCase( "a\\b", "aa/b", false )]
    [TestCase( "aa\\b", "aa/b", false )]
    [TestCase( "aaa\\b", "aa/b", false )]
    [TestCase( "a/b/c/", "b\\c", true )]
    [TestCase( "a/b/c/", "b/c/", true )]
    [TestCase( "a/b/c/", "bb\\c", false )]
    public void EndsWith_Path_is_strict_by_default( string root, string end, bool resultPath )
    {
        new NormalizedPath( root ).EndsWith( new NormalizedPath( end ) )
            .ShouldBe( resultPath, resultPath ? "Path should End." : "Path should NOT End." );
    }

    [TestCase( "", "", false )]
    [TestCase( null, null, false )]
    [TestCase( "", "a", false )]
    [TestCase( "a", "a", false )]
    [TestCase( "a", "", false )]
    [TestCase( "a", null, false )]
    [TestCase( "a/b", "b", true )]
    [TestCase( "a\\b", "aa/b", false )]
    [TestCase( "aa\\b", "aa/b", false )]
    [TestCase( "aaa\\b", "aa/b", true )]
    [TestCase( "a/b/c/", "b\\c", false )]
    [TestCase( "a/b/c/", "b/c/", false )]
    [TestCase( "a/b/c/", "bb\\c", false )]
    public void EndsWith_String_STRICT_at_work( string root, string end, bool resultString )
    {
        new NormalizedPath( root ).EndsWith( end, strict: true )
            .ShouldBe( resultString, resultString ? "String should End." : "String should NOT End." );
    }

    [TestCase( "", "", true )]
    [TestCase( null, null, true )]
    [TestCase( "", "a", false )]
    [TestCase( "a", "a", true )]
    [TestCase( "a", "", true )]
    [TestCase( "a", null, true )]
    [TestCase( "a/b", "b", true )]
    [TestCase( "a\\b", "a/b", true )]
    [TestCase( "a/b/c/", "b\\c", true )]
    public void EndsWith_Path_NOT_strict_at_work( string root, string end, bool resultPath )
    {
        new NormalizedPath( root ).EndsWith( new NormalizedPath( end ), strict: false )
            .ShouldBe( resultPath, resultPath ? "Path should End." : "Path should NOT End." );
    }

    [TestCase( "", "", true )]
    [TestCase( null, null, true )]
    [TestCase( "", "a", false )]
    [TestCase( "a", "a", true )]
    [TestCase( "a", "", true )]
    [TestCase( "a", null, true )]
    [TestCase( "a/b", "b", true )]
    [TestCase( "a\\b", "a/b", true )]
    [TestCase( "a/b/c/", "b\\c", false )]
    public void EndsWith_String_is_NOT_strict_by_default( string root, string end, bool resultString )
    {
        new NormalizedPath( root ).EndsWith( end )
            .ShouldBe( resultString, resultString ? "String should End." : "String should NOT End." );
    }

    [TestCase( "", "", "" )]
    [TestCase( null, null, "" )]
    [TestCase( "", "a", "a" )]
    [TestCase( "a", "", "a" )]
    [TestCase( "", "a\\b", "a/b" )]
    [TestCase( "", "a\\b", "a/b" )]
    [TestCase( "r", "a\\b", "r/a/b" )]
    [TestCase( "//r", "a\\b", "//r/a/b" )]
    [TestCase( "r/x/", "a\\b", "r/x/a/b" )]
    [TestCase( "/r/x/", "\\a\\b\\", "/a/b" )]
    [TestCase( "/r", "\\a\\b\\", "/a/b" )]
    [TestCase( "/", "\\a\\b\\", "/a/b" )]
    [TestCase( "//", "\\a\\b\\", "/a/b" )]
    [TestCase( "//", "a/b/", "//a/b" )]
    [TestCase( "/", "a", "/a" )]
    [TestCase( "/", "", "/" )]
    public void Combine_at_work( string root, string suffix, string result )
    {
        new NormalizedPath( root ).Combine( suffix ).ShouldBe( new NormalizedPath( result ) );
    }

    [TestCase( "", "", "" )]
    [TestCase( null, null, "" )]
    [TestCase( "", "a", "a" )]
    [TestCase( "first", "a\\b", "ArgumentException" )]
    [TestCase( "", "a/b", "a/b" )]
    [TestCase( "", "/a", "/a" )]
    [TestCase( "", "a/", "a" )]
    [TestCase( "r", "a", "r/a" )]
    [TestCase( "r/x/", "a.t", "r/x/a.t" )]
    [TestCase( "/r", "a", "/r/a" )]
    [TestCase( "//r", "a", "//r/a" )]
    [TestCase( "//", "a", "//a" )]
    [TestCase( "/", "a/b", "/a/b" )]
    // Edge case: AppendPart allows the empty path to be combined with a path.
    [TestCase( "", "a/b/c", "a/b/c" )]
    [TestCase( "", "//", "//" )]
    public void AppendPart_is_like_combine_but_with_part_not_a_path( string root, string suffix, string result )
    {
        if( result == "ArgumentNullException" )
        {
            Should.Throw<ArgumentNullException>( () => new NormalizedPath( root ).AppendPart( suffix ) );

        }
        else if( result == "ArgumentException" )
        {
            Should.Throw<ArgumentException>( () => new NormalizedPath( root ).AppendPart( suffix ) );

        }
        else
        {
            new NormalizedPath( root ).AppendPart( suffix )
                    .ShouldBe( new NormalizedPath( result ) );
        }
    }

    [TestCase( "", "" )]
    [TestCase( null, "" )]
    [TestCase( "a", "a" )]
    [TestCase( "a\\b", "a/b,a" )]
    [TestCase( "a/b/c", "a/b/c,a/b,a" )]
    public void Parents_does_not_contain_the_empty_root( string p, string result )
    {
        new NormalizedPath( p ).Parents.Select( a => a.ToString() ).Concatenate()
                .ShouldBe( NormalizeExpectedResultAsStrings( result ).Concatenate() );
    }

    [TestCase( "", "", "" )]
    [TestCase( "x/y", "", "" )]
    [TestCase( "", "part", "" )]
    [TestCase( "x/y", "part", "x/y/part,x/part" )]
    [TestCase( "x/y", "p1,p2", "x/y/p1,x/y/p2,x/p1,x/p2" )]
    public void PathsToFirstPart_with_null_subPaths_at_work( string root, string parts, string result )
    {
        var nParts = parts.Split( ',' ).Where( x => x.Length > 0 );
        new NormalizedPath( root ).PathsToFirstPart( null!, nParts ).Select( a => a.ToString() ).Concatenate()
                .ShouldBe( NormalizeExpectedResultAsStrings( result ).Concatenate() );
    }

    [TestCase( "", "", "part", "" )]
    [TestCase( "", "subPath", "part", "" )]
    [TestCase( "x/y", "subPath", "", "" )]
    [TestCase( "/x/y", "subPath", "part", "/x/y/subPath/part,/x/subPath/part" )]
    [TestCase( "/x/y", "", "part", "/x/y/part,/x/part" )]
    [TestCase( "x/y", "a/b", "part", "x/y/a/b/part,x/a/b/part" )]
    [TestCase( "//x/y", "a/b", "part", "//x/y/a/b/part,//x/a/b/part" )]
    [TestCase( "x/y", "a/b,c/d", "p1,p2", "x/y/a/b/p1,x/y/a/b/p2,x/y/c/d/p1,x/y/c/d/p2,x/a/b/p1,x/a/b/p2,x/c/d/p1,x/c/d/p2" )]
    [TestCase( "c:/p", "a/b", "part", "c:/p/a/b/part,c:/a/b/part" )]
    public void PathsToFirstPart_with_paths_and_parts_at_work( string root, string paths, string parts, string result )
    {
        var nPaths = paths.Split( ',' ).Where( x => x.Length > 0 ).Select( x => new NormalizedPath( x ) );
        var nParts = parts.Split( ',' ).Where( x => x.Length > 0 );
        new NormalizedPath( root ).PathsToFirstPart( nPaths, nParts ).Select( a => a.ToString() ).Concatenate()
                .ShouldBe( NormalizeExpectedResultAsStrings( result ).Concatenate() );
    }

    [TestCase( "", "" )]
    [TestCase( ".", "" )]
    [TestCase( "..", "InvalidOperationException" )]
    [TestCase( "/..", "InvalidOperationException" )]
    [TestCase( "//..", "InvalidOperationException" )]
    [TestCase( "~/..", "InvalidOperationException" )]
    [TestCase( "c:/..", "InvalidOperationException" )]
    [TestCase( "plop://..", "InvalidOperationException" )]
    [TestCase( "a/b/../x", "a/x" )]
    [TestCase( "./a/./b/./.././x/.", "a/x" )]
    [TestCase( "a/b/../x/../..", "" )]
    [TestCase( "a/b/../x/../../..", "InvalidOperationException" )]
    public void ResolveDots( string path, string result )
    {
        if( result == "InvalidOperationException" )
        {
            Should.Throw<InvalidOperationException>( () => new NormalizedPath( path ).ResolveDots() );
        }
        else
        {
            new NormalizedPath( path ).ResolveDots()
                    .ShouldBe( new NormalizedPath( result ) );
        }
    }

    [TestCase( "..", "" )]
    [TestCase( "a/b/../x/../../..", "" )]
    [TestCase( "/a/b/../x/../../..", "/" )]
    [TestCase( "//a/b/../x/../../..", "//" )]
    [TestCase( "X:/x/../..", "X:" )]
    [TestCase( "X:/x/../../../../A", "X:/A" )]
    public void ResolveDots_with_throwOnAboveRoot_false( string path, string result )
    {
        new NormalizedPath( path ).ResolveDots( throwOnAboveRoot: false )
                .ShouldBe( new NormalizedPath( result ) );
    }

    [TestCase( "", 1, "ArgumentOutOfRangeException" )]
    [TestCase( "a", 2, "ArgumentOutOfRangeException" )]
    [TestCase( ".", 1, "." )]
    [TestCase( ".", 0, "" )]
    [TestCase( "A/..", 1, "InvalidOperationException" )]
    [TestCase( "a/b/../x", 3, "a/b/../x" )]
    [TestCase( "./a/./b/./.././x/.", 2, "./a/x" )]
    [TestCase( "a/b/../x/../..", 1, "InvalidOperationException" )]
    [TestCase( "PRO/TECT/ED/a/b/../x/../../..", 3, "InvalidOperationException" )]
    public void ResolveDots_with_locked_root( string path, int rootPartsCount, string result )
    {
        if( result == "ArgumentOutOfRangeException" )
        {
            Should.Throw<ArgumentOutOfRangeException>( () => new NormalizedPath( path ).ResolveDots( rootPartsCount: rootPartsCount ) );
        }
        else if( result == "InvalidOperationException" )
        {
            Should.Throw<InvalidOperationException>( () => new NormalizedPath( path ).ResolveDots( rootPartsCount: rootPartsCount ) );
        }
        else
        {
            new NormalizedPath( path ).ResolveDots( rootPartsCount: rootPartsCount )
                    .ShouldBe( new NormalizedPath( result ) );
        }
    }

    [TestCase( "", 0, "ArgumentOutOfRangeException" )]
    [TestCase( "a", 1, "ArgumentOutOfRangeException" )]
    [TestCase( "a/b", 2, "ArgumentOutOfRangeException" )]
    [TestCase( "a", -1, "ArgumentOutOfRangeException" )]
    [TestCase( "a/b", -1, "ArgumentOutOfRangeException" )]
    [TestCase( "a", 0, "" )]
    [TestCase( "a/b", 0, "b" )]
    [TestCase( "a/b", 1, "a" )]
    [TestCase( "/a/b/c/", 1, "/a/c" )]
    [TestCase( "//a/b/c/", 0, "b/c" )]
    public void RemovePart_at_work( string path, int index, string result )
    {
        if( result == "ArgumentOutOfRangeException" )
        {
            Should.Throw<ArgumentOutOfRangeException>( () => new NormalizedPath( path ).RemovePart( index ) );
        }
        else
        {
            new NormalizedPath( path ).RemovePart( index )
                .ShouldBe( new NormalizedPath( result ) );
        }
    }

    [Test]
    public void RemoveFirstPart_removes_the_root()
    {
        NormalizedPath winRootedPath = @"C:\dev\test.txt";
        NormalizedPath unixRootedPath = "/usr/dev/test.txt";
        NormalizedPath httpRootedPath = "http://dev/test.txt";
        NormalizedPath uncRootedPath = "//mount/dev/test.txt";

        winRootedPath.IsRooted.ShouldBeTrue();
        unixRootedPath.IsRooted.ShouldBeTrue();

        var winPart = winRootedPath.RemoveFirstPart();
        winPart.IsRooted.ShouldBeFalse();
        winPart.ToString().ShouldBe( "dev/test.txt" );
        winPart = winRootedPath.RemoveFirstPart( 2 );
        winPart.IsRooted.ShouldBeFalse();
        winPart.ToString().ShouldBe( "test.txt" );

        var unixPart = unixRootedPath.RemoveFirstPart();
        unixPart.IsRooted.ShouldBeFalse();
        unixPart.ToString().ShouldBe( "dev/test.txt" );
        unixPart = unixRootedPath.RemoveFirstPart( 2 );
        unixPart.IsRooted.ShouldBeFalse();
        unixPart.ToString().ShouldBe( "test.txt" );

        var httpPart = httpRootedPath.RemoveFirstPart();
        httpPart.IsRooted.ShouldBeFalse();
        httpPart.ToString().ShouldBe( "dev/test.txt" );
        httpPart = httpRootedPath.RemoveFirstPart( 2 );
        httpPart.IsRooted.ShouldBeFalse();
        httpPart.ToString().ShouldBe( "test.txt" );

        var uncPart = uncRootedPath.RemoveFirstPart();
        uncPart.IsRooted.ShouldBeFalse();
        uncPart.ToString().ShouldBe( "dev/test.txt" );
        uncPart = uncRootedPath.RemoveFirstPart( 2 );
        uncPart.IsRooted.ShouldBeFalse();
        uncPart.ToString().ShouldBe( "test.txt" );
    }

    [TestCase( "", 0, 0, "ArgumentOutOfRangeException" )]
    [TestCase( "a", 0, 1, "" )]
    [TestCase( "a/b", 0, 2, "" )]
    [TestCase( "a", -1, 1, "ArgumentOutOfRangeException" )]
    [TestCase( "a/b", 1, 0, "a/b" )]
    [TestCase( "a/b", 2, 0, "a/b" )]
    [TestCase( "//a/b/c/d", 0, 1, "b/c/d" )]
    [TestCase( "/a/b/c/d", 0, 2, "c/d" )]
    [TestCase( "//a/b/c/d", 1, 2, "//a/d" )]
    [TestCase( "/a/b/c/d", 1, 2, "/a/d" )]
    [TestCase( "/a/b/c/d", 2, 2, "/a/b" )]
    public void RemoveParts_at_work( string path, int startIndex, int count, string result )
    {
        if( result == "ArgumentOutOfRangeException" )
        {
            Should.Throw<ArgumentOutOfRangeException>( () => new NormalizedPath( path ).RemoveParts( startIndex, count ) );
        }
        else
        {
            new NormalizedPath( path ).RemoveParts( startIndex, count )
                .ShouldBe( new NormalizedPath( result ) );
        }
    }

    [TestCase( "", -1, "ArgumentException" )]
    [TestCase( "", 0, "" )]
    [TestCase( "", 1, "ArgumentException" )]
    [TestCase( "A", -1, "ArgumentException" )]
    [TestCase( "A", 1, "" )]
    [TestCase( "A/B", 1, "A" )]
    [TestCase( "A/B", 2, "" )]
    [TestCase( "A/B/C", 1, "A/B" )]
    [TestCase( "A/B/C", 2, "A" )]
    [TestCase( "A/B/C", 3, "" )]
    [TestCase( "A/B/C", 4, "ArgumentException" )]
    [TestCase( "A/B/C/D", -1, "ArgumentException" )]
    [TestCase( "A/B/C/D", 0, "A/B/C/D" )]
    [TestCase( "A/B/C/D", 1, "A/B/C" )]
    [TestCase( "A/B/C/D", 2, "A/B" )]
    [TestCase( "A/B/C/D", 3, "A" )]
    [TestCase( "A/B/C/D", 4, "" )]
    [TestCase( "A/B/C/D", 5, "ArgumentException" )]
    [TestCase( @"C:\Dev\CK-Database-Projects\CK-Sqlite\CK.Sqlite.Setup.Runtime\bin\Debug\netcoreapp2.1\publish", 4, @"C:\Dev\CK-Database-Projects\CK-Sqlite\CK.Sqlite.Setup.Runtime" )]
    public void RemoveLastPart_at_work( string path, int count, string result )
    {
        if( result == "ArgumentException" )
        {
            Should.Throw<ArgumentException>( () => new NormalizedPath( path ).RemoveLastPart( count ) );
        }
        else
        {
            new NormalizedPath( path ).RemoveLastPart( count )
                    .ShouldBe( new NormalizedPath( result ) );
        }
    }
    [TestCase( "", -1, "ArgumentException" )]
    [TestCase( "", 0, "" )]
    [TestCase( "", 1, "ArgumentException" )]
    [TestCase( "A", -1, "ArgumentException" )]
    [TestCase( "A", 1, "" )]
    [TestCase( "A/B", 1, "B" )]
    [TestCase( "A/B", 2, "" )]
    [TestCase( "A/B/C", 1, "B/C" )]
    [TestCase( "A/B/C", 2, "C" )]
    [TestCase( "A/B/C", 3, "" )]
    [TestCase( "A/B/C", 4, "ArgumentException" )]
    [TestCase( "A/B/C/D", -1, "ArgumentException" )]
    [TestCase( "A/B/C/D", 0, "A/B/C/D" )]
    [TestCase( "A/B/C/D", 1, "B/C/D" )]
    [TestCase( "A/B/C/D", 2, "C/D" )]
    [TestCase( "A/B/C/D", 3, "D" )]
    [TestCase( "A/B/C/D", 4, "" )]
    [TestCase( "A/B/C/D", 5, "ArgumentException" )]
    [TestCase( @"C:\Dev\CK-Database-Projects\CK-Sqlite\CK.Sqlite.Setup.Runtime\bin\Debug\netcoreapp2.1\publish", 4, @"CK.Sqlite.Setup.Runtime\bin\Debug\netcoreapp2.1\publish" )]
    public void RemoveFirstPart_at_work( string path, int count, string result )
    {
        if( result == "ArgumentException" )
        {
            Should.Throw<ArgumentException>( () => new NormalizedPath( path ).RemoveFirstPart( count ) );
        }
        else
        {
            new NormalizedPath( path ).RemoveFirstPart( count )
                    .ShouldBe( new NormalizedPath( result ) );
        }
    }

    static IEnumerable<string> NormalizeExpectedResultAsStrings( string result ) => NormalizeExpectedResult( result ).Select( x => x.ToString() );

    static IEnumerable<NormalizedPath> NormalizeExpectedResult( string result )
    {
        return result.Split( ',' )
                            .Where( x => x.Length > 0 )
                            .Select( x => new NormalizedPath( x ) );
    }

    [Test]
    public void Convertible_tests()
    {
        var p = new NormalizedPath( "A\\B" );
        var s = Convert.ChangeType( p, typeof( string ) );
        s.ShouldBe( "A/B" );
        // ChangeType doesn't use the target type [TypeConverter(...)] nor
        // its potential IConvertible interface.
    }

    [TestCase( "//a/b", "//a/b", "" )]
    [TestCase( "/", "/", "" )]
    [TestCase( "/a", "/b", "../b" )]
    [TestCase( "//", "//a/b", "a/b" )]
    [TestCase( "//a/b", "//", "../.." )]
    [TestCase( "//a/b", "//a", ".." )]
    [TestCase( "c:/", "c:/a", "a" )]
    [TestCase( "c:/a", "c:/a/b", "b" )]
    [TestCase( "c:/a/b", "c:/", "../.." )]
    [TestCase( "http://a.b/c/d", "http://a.b", "../.." )]
    [TestCase( "http://a.b/c/d", "http://a.b/c", ".." )]
    [TestCase( "http://a.b/c/d", "http://a.b/c/e", "../e" )]
    [TestCase( "http://", "http://", "" )]
    public void GetRelativePath_valid_test( string source, string target, string expected )
    {
        var s = new NormalizedPath( source );
        s.TryGetRelativePathTo( target, out var relative ).ShouldBeTrue();
        relative.ShouldBe( new NormalizedPath( expected ) );
        s.Combine( relative ).ResolveDots().ShouldBe( target );
    }

    [TestCase( "//a/b", "" )]
    [TestCase( "", "/a" )]
    [TestCase( "a", "a" )]
    [TestCase( "http://a", "http://b" )]
    public void GetRelativePath_invalid_test( string source, string target )
    {
        new NormalizedPath( source ).TryGetRelativePathTo( target, out var _ ).ShouldBeFalse();
    }

}
