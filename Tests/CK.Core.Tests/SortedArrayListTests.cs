using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CK.Core.Tests;

public class SortedArrayListTests
{
    [Test]
    public void SortedArrayListSimpleTest()
    {
        var a = new CKSortedArrayList<int>();
        a.AddRangeArray( 12, -34, 7, 545, 12 );
        a.AllowDuplicates.ShouldBeFalse();
        a.Count.ShouldBe( 4 );
        a.ShouldBeInOrder();

        a.Contains( 14 ).ShouldBeFalse();
        a.IndexOf( 12 ).ShouldBe( 2 );

        object? o = 21;
        a.Contains( o ).ShouldBeFalse();
        a.IndexOf( o ).ShouldBeLessThan( 0 );

        o = 12;
        a.Contains( o ).ShouldBeTrue();
        a.IndexOf( o ).ShouldBe( 2 );

        o = null;
        a.Contains( o! ).ShouldBeFalse();
        a.IndexOf( o! ).ShouldBe( int.MinValue );

        int[] arrayToTest = new int[5];
        a.CopyTo( arrayToTest, 1 );
        arrayToTest[0].ShouldBe( 0 );
        arrayToTest[1].ShouldBe( -34 );
        arrayToTest[4].ShouldBe( 545 );
    }

    [Test]
    public void SortedArrayListAllowDuplicatesTest()
    {
        var b = new CKSortedArrayList<int>( true );
        b.AddRangeArray( 12, -34, 7, 545, 12 );
        b.AllowDuplicates.ShouldBeTrue();
        b.Count.ShouldBe( 5 );
        b.ShouldBeInOrder();
        b.IndexOf( 12 ).ShouldBe( 2 );
        b.CheckPosition( 2 ).ShouldBe( 2 );
        b.CheckPosition( 3 ).ShouldBe( 3 );
    }

    [Test]
    public void Covariance_support_via_ICKReadOnlyList_and_ICKWritableCollection()
    {
        var a = new CKSortedArrayList<Mammal>( ( a1, a2 ) => a1.Name.CompareTo( a2.Name ) )
        {
            new Mammal( "B", 12 ),
            new Canidae( "A", 12, true )
        };

        IReadOnlyList<Animal> baseObjects = a;
        for( int i = 0; i < baseObjects.Count; ++i )
        {
            baseObjects[i].ShouldBeAssignableTo<Animal>( "This does not test anything. It's just to be read." );
        }
        ICKWritableCollection<Canidae> dogs = a;
        dogs.Add( new Canidae( "C", 8, false ) );
    }

    class TestMammals : CKSortedArrayList<Mammal>
    {
        public TestMammals( Comparison<Mammal> m, bool allowDuplicated = false )
            : base( m, allowDuplicated )
        {
        }

        public Mammal[] Tab { get { return Store; } }
    }

    [Test]
    public void CheckPosition_locally_reorders_the_items()
    {
        var a = new TestMammals( ( a1, a2 ) => a1.Name.CompareTo( a2.Name ) )
        {
            new Mammal( "B" ),
            new Mammal( "A" ),
            new Mammal( "D" ),
            new Mammal( "F" ),
            new Mammal( "C" ),
            new Mammal( "E" )
        };
        String.Join( "", a.Select( m => m.Name ) ).ShouldBe( "ABCDEF" );

        for( int i = 0; i < a.Count; ++i )
        {
            a.CheckPosition( i ).ShouldBe( i, "Nothing changed." );
        }
        CheckList( a, "ABCDEF" );

        a[0].Name = "Z";
        CheckList( a, "ZBCDEF" );
        a.CheckPosition( 0 ).ShouldBe( 5 );
        CheckList( a, "BCDEFZ" );
        a[5].Name = "Z+";
        CheckList( a, "BCDEFZ+" );
        a.CheckPosition( 5 ).ShouldBe( 5 );
        CheckList( a, "BCDEFZ+" );
        a[5].Name = "A";
        a.CheckPosition( 5 ).ShouldBe( 0 );
        CheckList( a, "ABCDEF" );

        a[1].Name = "A";
        a.CheckPosition( 1 ).ShouldBeLessThan( 0 );
        CheckList( a, "AACDEF" );

        a[1].Name = "B";
        a.CheckPosition( 1 ).ShouldBe( 1 );
        CheckList( a, "ABCDEF" );

        a[1].Name = "C";
        a.CheckPosition( 1 ).ShouldBeLessThan( 0 );
        CheckList( a, "ACCDEF" );

        a[1].Name = "Z";
        a.CheckPosition( 1 ).ShouldBe( 5 );
        CheckList( a, "ACDEFZ" );

        a[5].Name = "D+";
        a.CheckPosition( 5 ).ShouldBe( 3 );
        CheckList( a, "ACDD+EF" );

        a[3].Name = "D";
        a.CheckPosition( 3 ).ShouldBeLessThan( 0 );
        CheckList( a, "ACDDEF" );

        a[3].Name = "B";
        a.CheckPosition( 3 ).ShouldBe( 1 );
        CheckList( a, "ABCDEF" );

        var b = new TestMammals( ( a1, a2 ) => a1.Name.CompareTo( a2.Name ) )
        {
            new Mammal( "B" ),
            new Mammal( "A" )
        };
        String.Join( "", b.Select( m => m.Name ) ).ShouldBe( "AB" );

        b[0].Name = "Z";
        CheckList( b, "ZB" );
        b.CheckPosition( 0 ).ShouldBe( 1 );
        CheckList( b, "BZ" );

        var c = new TestMammals( ( a1, a2 ) => a1.Name.CompareTo( a2.Name ), true )
        {
            new Mammal( "B" ),
            new Mammal( "A" )
        };
        String.Join( "", c.Select( m => m.Name ) ).ShouldBe( "AB" );

        c[0].Name = "Z";
        CheckList( c, "ZB" );
        c.CheckPosition( 0 ).ShouldBe( 1 );
        CheckList( c, "BZ" );

        var d = new TestMammals( ( a1, a2 ) => a1.Name.CompareTo( a2.Name ) )
        {
            new Mammal( "B" ),
            new Mammal( "C" )
        };
        String.Join( "", d.Select( m => m.Name ) ).ShouldBe( "BC" );

        d[1].Name = "A";
        CheckList( d, "BA" );
        d.CheckPosition( 1 ).ShouldBe( 0 );
        CheckList( d, "AB" );
    }

    [Test]
    public void using_binary_search_algorithms_on_SortedArrayList()
    {
        var a = new TestMammals( ( a1, a2 ) => a1.Name.CompareTo( a2.Name ) )
        {
            new Mammal( "B" ),
            new Mammal( "A" ),
            new Mammal( "D" ),
            new Mammal( "F" ),
            new Mammal( "C" ),
            new Mammal( "E" )
        };

        int idx;

        // External use of Util.BinarySearch on the exposed Store of the SortedArrayList.
        {
            idx = Util.BinarySearch( a.Tab, 0, a.Count, "E", ( m, name ) => m.Name.CompareTo( name ) );
            idx.ShouldBe( 4 );

            idx = Util.BinarySearch( a.Tab, 0, a.Count, "A", ( m, name ) => m.Name.CompareTo( name ) );
            idx.ShouldBe( 0 );

            idx = Util.BinarySearch( a.Tab, 0, a.Count, "Z", ( m, name ) => m.Name.CompareTo( name ) );
            idx.ShouldBe( ~6 );
        }
        // Use of the extended SortedArrayList.IndexOf().
        {
            idx = a.IndexOf( "E", ( m, name ) => m.Name.CompareTo( name ) );
            idx.ShouldBe( 4 );

            idx = a.IndexOf( "A", ( m, name ) => m.Name.CompareTo( name ) );
            idx.ShouldBe( 0 );

            idx = a.IndexOf( "Z", ( m, name ) => m.Name.CompareTo( name ) );
            idx.ShouldBe( ~6 );
        }
    }

    private static void CheckList( TestMammals a, string p )
    {
        HashSet<Mammal> dup = new HashSet<Mammal>();
        int i = 0;
        while( i < a.Count )
        {
            a[i].ShouldNotBeNull();
            dup.Add( a[i] ).ShouldBeTrue();
            ++i;
        }
        while( i < a.Tab.Length )
        {
            a.Tab[i].ShouldBeNull();
            ++i;
        }
        string.Join( "", a.Select( m => m.Name ) ).ShouldBe( p );
    }


    class TestInt : CKSortedArrayList<int>
    {
        public TestInt()
        {
        }

        public int[] Tab => Store;

        public void CheckList()
        {
            this.IsSortedStrict();
        }
    }

    private static void CheckList( TestInt a, params int[] p )
    {
        a.CheckList();
        a.SequenceEqual( p ).ShouldBeTrue();
    }

    [Test]
    public void testing_add_and_remove_items()
    {
        var a = new TestInt();
        a.CheckList();
        Should.Throw<IndexOutOfRangeException>( () => a.RemoveAt( -1 ) );
        Should.Throw<IndexOutOfRangeException>( () => a.RemoveAt( 0 ) );
        Should.Throw<IndexOutOfRangeException>( () => a.RemoveAt( 1 ) );

        a.Remove( -1 ).ShouldBeFalse();
        a.Remove( 0 ).ShouldBeFalse();
        a.Remove( 1 ).ShouldBeFalse();

        a.Add( 204 );
        a.CheckList();
        Should.Throw<IndexOutOfRangeException>( () => a.RemoveAt( -1 ) );
        Should.Throw<IndexOutOfRangeException>( () => a.RemoveAt( 1 ) );

        a.RemoveAt( 0 );
        a.Count.ShouldBe( 0 );
        a.CheckList();

        a.Add( 206 );
        a.Add( 205 );
        a.Add( 204 );
        CheckList( a, 204, 205, 206 );

        a.RemoveAt( 1 );
        CheckList( a, 204, 206 );
        Should.Throw<IndexOutOfRangeException>( () => a.RemoveAt( 2 ) );
        a.RemoveAt( 1 );
        CheckList( a, 204 );
        a.RemoveAt( 0 );
        CheckList( a );

        a.Add( 206 );
        a.Add( 205 );
        a.Add( 204 );
        a.Add( 207 );
        a.Add( 208 );
        CheckList( a, 204, 205, 206, 207, 208 );
        Should.Throw<IndexOutOfRangeException>( () => a.RemoveAt( 5 ) );
        a.RemoveAt( 0 );
        CheckList( a, 205, 206, 207, 208 );
        a.RemoveAt( 3 );
        CheckList( a, 205, 206, 207 );
        a.RemoveAt( 1 );
        CheckList( a, 205, 207 );
        a.RemoveAt( 1 );
        CheckList( a, 205 );
        a.RemoveAt( 0 );
        CheckList( a );

        a.Add( 206 );
        a.Add( 205 );
        a.Add( 204 );
        a.Add( 207 );
        a.Add( 208 );
        CheckList( a, 204, 205, 206, 207, 208 );
        a.Remove( 203 ).ShouldBeFalse();
        CheckList( a, 204, 205, 206, 207, 208 );
        a.Remove( 204 ).ShouldBeTrue();
        CheckList( a, 205, 206, 207, 208 );
        a.Remove( 208 ).ShouldBeTrue();
        CheckList( a, 205, 206, 207 );
        a.Remove( 208 ).ShouldBeFalse();
        CheckList( a, 205, 206, 207 );
        a.Remove( 206 ).ShouldBeTrue();
        CheckList( a, 205, 207 );
        a.Remove( 207 ).ShouldBeTrue();
        CheckList( a, 205 );
        a.Remove( 205 ).ShouldBeTrue();
        CheckList( a );

    }

    [Test]
    public void testing_capacity_changes()
    {
        var a = new CKSortedArrayList<Mammal>( ( a1, a2 ) => a1.Name.CompareTo( a2.Name ) );

        a.Capacity.ShouldBe( 0 );
        a.Capacity = 3;
        a.Capacity.ShouldBe( 4 );
        a.Capacity = 0;
        a.Capacity.ShouldBe( 0 );

        a.Add( new Mammal( "1" ) );

        Should.Throw<ArgumentException>( () => a.Capacity = 0 );

        a.Add( new Mammal( "2" ) );
        a.Add( new Mammal( "3" ) );
        a.Add( new Mammal( "4" ) );
        a.Add( new Mammal( "5" ) );

        a.Capacity.ShouldBe( 8 );
        a.Capacity = 5;
        a.Capacity.ShouldBe( 5 );

        a.Add( new Mammal( "6" ) );
        a.Add( new Mammal( "7" ) );
        a.Add( new Mammal( "8" ) );
        a.Add( new Mammal( "9" ) );
        a.Add( new Mammal( "10" ) );

        a.Capacity.ShouldBe( 10 );

        a.Clear();

        a.Capacity.ShouldBe( 10 );

    }

    [Test]
    public void testing_expected_Argument_InvalidOperation_and_IndexOutOfRangeException()
    {
        var a = new CKSortedArrayList<Mammal>( ( a1, a2 ) => a1.Name.CompareTo( a2.Name ) );

        Should.Throw<ArgumentNullException>( () => a.IndexOf( null! ) );
        Should.Throw<ArgumentNullException>( () => a.IndexOf( null! ) );
        Should.Throw<ArgumentNullException>( () => a.IndexOf<Mammal>( new Mammal( "Nothing" ), null! ) );
        Should.Throw<ArgumentNullException>( () => a.Add( null! ) );

        a.Add( new Mammal( "A" ) );
        a.Add( new Mammal( "B" ) );

        Should.Throw<IndexOutOfRangeException>( () => { Mammal test = a[2]; } );
        Should.Throw<IndexOutOfRangeException>( () => a.CheckPosition( 2 ) );
        Should.Throw<IndexOutOfRangeException>( () => { Mammal test = a[-1]; } );

        //Enumerator Exception (considering the non generic version since generics have weaken the invariants).
        var enumerator = ((System.Collections.IEnumerable)a).GetEnumerator();
        Should.Throw<InvalidOperationException>( () => { object? temp = enumerator.Current; } );
        enumerator.MoveNext();
        enumerator.Current.ShouldBe( a[0] );
        enumerator.Reset();
        Should.Throw<InvalidOperationException>( () => { object? temp = enumerator.Current; } );
        a.Clear(); //change _version
        Should.Throw<InvalidOperationException>( () => enumerator.Reset() );
        Should.Throw<InvalidOperationException>( () => enumerator.MoveNext() );

        //Exception
        IList<Mammal> testException = new CKSortedArrayList<Mammal>
        {
            new Mammal( "Nothing" )
        };
        Should.Throw<IndexOutOfRangeException>( () => testException[-1] = new Mammal( "A" ) );
        Should.Throw<IndexOutOfRangeException>( () => testException[1] = new Mammal( "A" ) );
        Should.Throw<ArgumentNullException>( () => testException[0] = null! );
        Should.Throw<IndexOutOfRangeException>( () => testException.Insert( -1, new Mammal( "A" ) ) );
        Should.Throw<IndexOutOfRangeException>( () => testException.Insert( 2, new Mammal( "A" ) ) );

        Should.Throw<ArgumentNullException>( () => testException.Insert( 0, null! ) );
    }

    [Test]
    public void SortedArrayList_can_be_cast_into_IList_or_ICollection()
    {
        var a = new CKSortedArrayList<int>();
        a.AddRangeArray( 12, -34, 7, 545, 12 );

        //Cast IList
        IList<int> listToTest = (IList<int>)a;

        listToTest[0].ShouldBe( -34 );
        listToTest[1].ShouldBe( 7 );
        listToTest[2].ShouldBe( 12 );
        listToTest[3].ShouldBe( 545 );

        listToTest.Add( 12345 );
        listToTest.Add( 1234 );
        listToTest[4].ShouldBe( 1234 );
        listToTest[5].ShouldBe( 12345 );

        listToTest[0] = -33;
        listToTest[0].ShouldBe( -33 );
        listToTest[0] = 123456;
        listToTest[0].ShouldBe( 123456 );

        listToTest.Insert( 0, -33 );
        listToTest[0].ShouldBe( -33 );
        listToTest.Insert( 0, 123456 );
        listToTest[0].ShouldBe( 123456 );

        //Cast ICollection
        a.Clear();
        a.AddRangeArray( 12, -34, 7, 545, 12 );
        ICollection<int> collectionToTest = (ICollection<int>)a;

        collectionToTest.IsReadOnly.ShouldBeFalse();

        collectionToTest.Add( 123 );
        collectionToTest.Contains( 123 ).ShouldBeTrue();
        collectionToTest.Contains( -34 ).ShouldBeTrue();
        collectionToTest.Contains( 7 ).ShouldBeTrue();
    }

}
