using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace CK.Core.Tests;


public class EnumerableExtensionTests
{
    [Test]
    public void test_IsSortedStrict_and_IsSortedLarge_extension_methods()
    {
        List<int> listWithDuplicate = new List<int>();
        listWithDuplicate.AddRangeArray<int>( 1, 2, 2, 3, 3, 5 );

        List<int> listWithoutDuplicate = new List<int>();
        listWithoutDuplicate.AddRangeArray<int>( 1, 2, 3, 5 );

        listWithDuplicate.IsSortedStrict().ShouldBeFalse();
        listWithDuplicate.IsSortedLarge().ShouldBeTrue();

        listWithoutDuplicate.IsSortedStrict().ShouldBeTrue();
        listWithoutDuplicate.IsSortedLarge().ShouldBeTrue();

        listWithDuplicate.Reverse( 0, listWithDuplicate.Count );
        listWithoutDuplicate.Reverse( 0, listWithoutDuplicate.Count );

        listWithDuplicate.IsSortedStrict().ShouldBeFalse();
        listWithDuplicate.IsSortedLarge().ShouldBeFalse();

        listWithoutDuplicate.IsSortedStrict().ShouldBeFalse();
        listWithoutDuplicate.IsSortedLarge().ShouldBeFalse();

        //test with 2 items
        listWithoutDuplicate = new List<int>();
        listWithoutDuplicate.AddRangeArray<int>( 1, 5 );

        listWithoutDuplicate.IsSortedStrict().ShouldBeTrue();
        listWithoutDuplicate.IsSortedLarge().ShouldBeTrue();


        listWithDuplicate = new List<int>();
        listWithDuplicate.AddRangeArray<int>( 5, 5 );

        listWithDuplicate.IsSortedStrict().ShouldBeFalse();
        listWithDuplicate.IsSortedLarge().ShouldBeTrue();

        listWithDuplicate.Reverse( 0, listWithDuplicate.Count );
        listWithoutDuplicate.Reverse( 0, listWithoutDuplicate.Count );

        listWithDuplicate.IsSortedStrict().ShouldBeFalse();
        listWithDuplicate.IsSortedLarge().ShouldBeTrue();

        listWithoutDuplicate.IsSortedStrict().ShouldBeFalse();
        listWithoutDuplicate.IsSortedLarge().ShouldBeFalse();

        //test with 1 items
        listWithoutDuplicate = new List<int>();
        listWithoutDuplicate.Add( 1 );

        listWithoutDuplicate.IsSortedStrict().ShouldBeTrue();
        listWithoutDuplicate.IsSortedLarge().ShouldBeTrue();

        //Empty test
        listWithoutDuplicate = new List<int>();

        listWithoutDuplicate.IsSortedStrict().ShouldBeTrue();
        listWithoutDuplicate.IsSortedLarge().ShouldBeTrue();

        listWithDuplicate = new List<int>();

        listWithoutDuplicate.IsSortedStrict().ShouldBeTrue();
        listWithoutDuplicate.IsSortedLarge().ShouldBeTrue();

        listWithDuplicate = null!;
        Should.Throw<NullReferenceException>( () => listWithDuplicate.IsSortedLarge() );
        Should.Throw<NullReferenceException>( () => listWithDuplicate.IsSortedStrict() );
    }

    [Test]
    public void test_IndexOf_extension_method()
    {
        var listToTest = new List<int>();
        listToTest.AddRangeArray<int>( 1, 2 );

        listToTest.IndexOf( a => a == 0 ).ShouldBe( -1 );
        listToTest.IndexOf( a => a == 1 ).ShouldBe( 0 );
        listToTest.IndexOf( a => a == 2 ).ShouldBe( 1 );
        listToTest.IndexOf( ( a, b ) => b == listToTest.IndexOf<int>( c => c == 0 ) && a == 0 ).ShouldBe( -1 );
        listToTest.IndexOf( ( a, b ) => b == listToTest.IndexOf<int>( c => c == 1 ) && a == 1 ).ShouldBe( 0 );
        listToTest.IndexOf( ( a, b ) => b == listToTest.IndexOf<int>( c => c == 2 ) && a == 2 ).ShouldBe( 1 );

        listToTest.Add( 2 );

        listToTest.IndexOf( a => a == 2 ).ShouldBe( 1 );
        listToTest.IndexOf( ( a, idx ) => idx == 2 && a == 2 ).ShouldBe( 2 );
    }

    [Test]
    public void test_Append_extension_method()
    {
        int[] t = new int[0];
        t.Append( 5 ).ToArray().ShouldBe( new[] { 5 } );
        t.Append( 2 ).Append( 5 ).ToArray().ShouldBe( new[] { 2, 5 } );
        t.Append( 2 ).Append( 3 ).Append( 5 ).ToArray().ShouldBe( new[] { 2, 3, 5 } );

        var tX = t.Append( 2 ).Append( 3 ).Append( 4 ).Append( 5 );
        tX.ToArray().ShouldBe( new[] { 2, 3, 4, 5 } );
    }

    [Test]
    public void MaxBy_throws_InvalidOperationException_on_empty_sequence()
    {
        Action a = () => new int[0].MaxBy( i => -i );
        a.ShouldThrow<InvalidOperationException>();
        a = () => new int[0].MaxBy( i => -i, null );
        a.ShouldThrow<InvalidOperationException>();
    }

    [Test]
    public void test_MaxBy_extension_method()
    {
        int[] t = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

        t.MaxBy( Util.FuncIdentity ).ShouldBe( 12 );
        t.MaxBy( i => -i ).ShouldBe( 0 );
        t.MaxBy( i => (i + 1) % 6 == 0 ).ShouldBe( 5 );
        t.MaxBy( i => i.ToString() ).ShouldBe( 9, "Lexicographical ordering." );

        t.MaxBy( i => i, ( x, y ) => x - y ).ShouldBe( 12 );

        Should.Throw<ArgumentNullException>( () => t.MaxBy<int, int>( null! ) );
        t = null!;
        Should.Throw<NullReferenceException>( () => t.MaxBy( Util.FuncIdentity ) );
    }
}
