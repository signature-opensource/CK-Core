using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CK.Core.Tests;

public class SortedArrayKeyListTests
{
    [Test]
    public void sorting_Lexicographic_integers()
    {
        var a = new CKSortedArrayKeyList<int, string>( i => i.ToString() );
        a.AddRangeArray( 1, 2, 3 );
        CheckList( a, 1, 2, 3 );

        a.AddRangeArray( 10, 20, 30 );
        CheckList( a, 1, 10, 2, 20, 3, 30 );

        a.AddRangeArray( 10, 20, 30 );
        CheckList( a, 1, 10, 2, 20, 3, 30 );

        a.AddRangeArray( 10000, 1000, 100, 10, 1, 56 );
        CheckList( a, 1, 10, 100, 1000, 10000, 2, 20, 3, 30, 56 );

        a.AddRangeArray( 10000, 1000, 100, 10, 1, 46 );
        CheckList( a, 1, 10, 100, 1000, 10000, 2, 20, 3, 30, 46, 56 );
    }

    [Test]
    public void SortedArrayKeyList_can_allow_duplicates()
    {
        var a = new CKSortedArrayKeyList<int, string>( i => i.ToString() );

        a.AddRangeArray( 1, 10, 100, 100, 1000, 10000, 2, 20, 3, 30, 100, 46, 56 );
        CheckList( a, 1, 10, 100, 1000, 10000, 2, 20, 3, 30, 46, 56 );

        a.IndexOf( 1 ).ShouldBe( 0 );
        a.IndexOf( 2 ).ShouldBe( 5 );
        a.IndexOf( 3 ).ShouldBe( 7 );

        a.KeyCount( "100" ).ShouldBe( 1 );

        object? o;
        o = "2";
        a.IndexOf( o ).ShouldBe( 5 );
        o = 2;
        a.IndexOf( o ).ShouldBe( 5 );
        o = null;
        a.IndexOf( o! ).ShouldBe( Int32.MinValue );
        o = new ClassToTest( "A" );
        a.IndexOf( o ).ShouldBe( Int32.MinValue );
        o = "42";
        a.Contains( o ).ShouldBeFalse();

        a.Count.ShouldBe( 11 );
        a.KeyCount( "10" ).ShouldBe( 1 );
        a.Remove( "10" );
        a.KeyCount( "10" ).ShouldBe( 0 );
        a.Count.ShouldBe( 10 );

        CheckList( a, 1, 100, 1000, 10000, 2, 20, 3, 30, 46, 56 );
        a.Remove( "20" );
        CheckList( a, 1, 100, 1000, 10000, 2, 3, 30, 46, 56 );
        a.Remove( "100" );
        a.KeyCount( "100" ).ShouldBe( 0 );
        CheckList( a, 1, 1000, 10000, 2, 3, 30, 46, 56 );
        a.Remove( "Nothing" ).ShouldBeFalse();
    }

    [Test]
    public void SortedArrayKeyList_does_not_accept_null_entries()
    {
        var b = new CKSortedArrayKeyList<ClassToTest, string>( i => i.ToString(), false );
        ClassToTest classToTest = new ClassToTest( "A" );

        b.Add( classToTest );
        b.Add( new ClassToTest( "B" ) );

        b.Contains( classToTest ).ShouldBeTrue();
        b.IndexOf( classToTest ).ShouldBe( 0 );
        Should.Throw<ArgumentNullException>( () => b.IndexOf( (ClassToTest)null! ) );
    }

    [Test]
    public void SortedArrayKeyList_without_duplicates()
    {
        var a = new CKSortedArrayKeyList<int, string>( i => i.ToString() );
        a.AddRangeArray( 3, 2, 1 );

        bool exists;
        a.GetByKey( "1", out exists ).ShouldBe( 1 ); exists.ShouldBeTrue();
        a.GetByKey( "10", out exists ).ShouldBe( 0 ); exists.ShouldBeFalse();
        a.GetByKey( "2", out exists ).ShouldBe( 2 ); exists.ShouldBeTrue();

        a.Contains( "2" ).ShouldBeTrue();
        a.Contains( "1" ).ShouldBeTrue();
        a.Contains( "21" ).ShouldBeFalse();

        object? o;
        o = "2";
        a.Contains( o ).ShouldBeTrue( "Using the key." );
        o = 2;
        a.Contains( o ).ShouldBeTrue( "Using the value itself." );
        o = null;
        a.Contains( o! ).ShouldBeFalse();
        o = 42;
        a.Contains( o ).ShouldBeFalse();
        o = "42";
        a.Contains( o ).ShouldBeFalse();

        a.Add( 3 ).ShouldBeFalse();
        a.Add( 2 ).ShouldBeFalse();
        a.Add( 1 ).ShouldBeFalse();

        CheckList( a.GetAllByKey( "2" ), 2 );
    }


    [Test]
    public void another_test_with_duplicates_in_SortedArrayKeyList()
    {
        var a = new CKSortedArrayKeyList<int, string>( i => (i % 100).ToString(), true );
        a.AddRangeArray( 2, 1 );

        bool exists;
        a.GetByKey( "1", out exists ).ShouldBe( 1 ); exists.ShouldBeTrue();
        a.GetByKey( "2", out exists ).ShouldBe( 2 ); exists.ShouldBeTrue();

        a.Add( 102 );
        a.Add( 101 );

        int v1 = a.GetByKey( "1" );
        v1.ShouldBeOneOf( [1, 101], "It is one or the other that is returned." );
        int v2 = a.GetByKey( "2" );
        v2.ShouldBeOneOf( [2, 102], "It is one or the other that is returned." );

        a.KeyCount( "2" ).ShouldBe( 2 );
        CheckList( a.GetAllByKey( "2" ).OrderBy( Util.FuncIdentity ), 2, 102 );

        a.Add( 102 );
        a.Add( 102 );
        a.Add( 102 );
        a.Add( 202 );
        a.Add( 302 );

        a.KeyCount( "2" ).ShouldBe( 7 );
        CheckList( a.GetAllByKey( "2" ).OrderBy( Util.FuncIdentity ), 2, 102, 102, 102, 102, 202, 302 );

        a.KeyCount( "5454" ).ShouldBe( 0 );
        a.GetAllByKey( "5454" ).ShouldBeEmpty();

    }

    private static void CheckList( IEnumerable<int> a, params int[] p )
    {
        a.Select( a => a.ToString() ).Concatenate().ShouldBe( p.Select( p => p.ToString() ).Concatenate() );
    }

    class ClassToTest
    {
        public ClassToTest( string name )
        {
            Name = name;
        }

        public string Name { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

}
