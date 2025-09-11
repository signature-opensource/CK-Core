using NUnit.Framework;
using System.Linq;
using System;
using System.Collections.Generic;
using Shouldly;

namespace CK.Core.Tests;

public class TestCollection<T> : ICKReadOnlyCollection<T>
{
    public List<T> Content;

    public event EventHandler? CountCalled;
    public event EventHandler? ContainsCalled;

    public TestCollection()
    {
        Content = new List<T>();
    }

    public bool Contains( object item )
    {
        if( ContainsCalled != null ) ContainsCalled( this, EventArgs.Empty );
        return item is T && Content.Contains( (T)item );
    }

    public int Count
    {
        get
        {
            if( CountCalled != null ) CountCalled( this, EventArgs.Empty );
            return Content.Count;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return Content.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return Content.GetEnumerator();
    }

}

public class TestCollectionThatImplementsICollection<T> : ICKReadOnlyCollection<T>, ICollection<T>
{
    public List<T> Content;

    public event EventHandler? CountCalled;
    public event EventHandler? ContainsCalled;

    public TestCollectionThatImplementsICollection()
    {
        Content = new List<T>();
    }

    public bool Contains( object item )
    {
        if( ContainsCalled != null ) ContainsCalled( this, EventArgs.Empty );
        return item is T t && Content.Contains( t );
    }

    public int Count
    {
        get
        {
            if( CountCalled != null ) CountCalled( this, EventArgs.Empty );
            return Content.Count;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return Content.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return Content.GetEnumerator();
    }


    #region ICollection<T> Members

    void ICollection<T>.Add( T item )
    {
        throw new NotSupportedException();
    }

    void ICollection<T>.Clear()
    {
        throw new NotSupportedException();
    }

    bool ICollection<T>.Contains( T item )
    {
        return Contains( item! );
    }

    void ICollection<T>.CopyTo( T[] array, int arrayIndex )
    {
        throw new NotSupportedException();
    }

    bool ICollection<T>.IsReadOnly
    {
        get { return true; }
    }

    bool ICollection<T>.Remove( T item )
    {
        throw new NotSupportedException();
    }

    #endregion
}


public class ReadOnlyTests
{
    [Test]
    public void linq_with_mere_IReadOnlyCollection_implementation_is_not_optimal_for_Count()
    {
        TestCollection<int> c = new TestCollection<int>();
        c.Content.Add( 2 );

        bool containsCalled = false, countCalled = false;
        c.ContainsCalled += ( o, e ) => { containsCalled = true; };
        c.CountCalled += ( o, e ) => { countCalled = true; };

        c.Count.ShouldBe( 1 );
        countCalled.ShouldBeTrue( "Count property on the concrete type logs the calls." ); countCalled = false;

        c.Count().ShouldBe( 1, "Use Linq extension methods (on the concrete type)." );
        countCalled.ShouldBeFalse( "The Linq extension method did NOT call our Count." );

        IEnumerable<int> cLinq = c;

        cLinq.Count().ShouldBe( 1, "Linq can not use our implementation..." );
        countCalled.ShouldBeFalse( "...it did not call our Count property." );

        // Addressing the concrete type: it is our method that is called.
        c.Contains( 2 ).ShouldBeTrue();
        containsCalled.ShouldBeTrue( "It is our Contains method that is called (not the Linq one)." ); containsCalled = false;
        c.Contains( 56 ).ShouldBeFalse();
        containsCalled.ShouldBeTrue( "It is our Contains method that is called." ); containsCalled = false;
        c.Contains( null! ).ShouldBeFalse( "Contains should accept ANY object without any error." );
        containsCalled.ShouldBeTrue( "It is our Contains method that is called." ); containsCalled = false;

        // Unfortunately, addressing the IEnumerable base type, Linq has no way to use our methods...
        cLinq.Contains( 2 ).ShouldBeTrue();
        containsCalled.ShouldBeFalse( "Linq use the enumerator to do the job." );
        cLinq.Contains( 56 ).ShouldBeFalse();
        containsCalled.ShouldBeFalse();
        // Linq Contains() accept only parameter of the generic type.
        // !cLinq.Contains( null ), "Contains should accept ANY object without any error." );
    }

    [Test]
    public void linq_on_ICollection_implementation_uses_Count_property()
    {
        TestCollectionThatImplementsICollection<int> c = new TestCollectionThatImplementsICollection<int>();
        c.Content.Add( 2 );

        bool containsCalled = false, countCalled = false;
        c.ContainsCalled += ( o, e ) => { containsCalled = true; };
        c.CountCalled += ( o, e ) => { countCalled = true; };

        c.Count.ShouldBe( 1 );
        countCalled.ShouldBeTrue( "Count property on the concrete type logs the calls." ); countCalled = false;

        IEnumerable<int> cLinq = c;

        cLinq.Count().ShouldBe( 1, "Is it our Count implementation that is called?" );
        countCalled.ShouldBeTrue( "Yes!" ); countCalled = false;

        c.Count.ShouldBe( 1, "Linq DOES use our implementation..." );
        countCalled.ShouldBeTrue( "...our Count property has been called." ); countCalled = false;

        // What's happening for Contains? 
        // The ICollection<T>.Contains( T ) is more precise than our Contains( object )...

        // Here we target the concrete type.
        c.Contains( 2 ).ShouldBeTrue();
        containsCalled.ShouldBeTrue( "It is our Contains method that is called (not the Linq one)." ); containsCalled = false;

        // Here we use the IEnumerable<int>. 
        // It shows that this is not the (slow) enumeration that is used here: it uses a direct call to Contains that can be much more efficient.
        // It works only because TestCollectionThatImplementsICollection relays the call to our Contains.
        cLinq.Contains( 2 ).ShouldBeTrue();
        containsCalled.ShouldBeTrue( "It is our Contains method that is called (not the Linq one)." ); containsCalled = false;

        cLinq.Contains( 56 ).ShouldBeFalse();
        containsCalled.ShouldBeTrue( "It is our Contains method that is called." ); containsCalled = false;

    }

    [Test]
    public void covariant_Contains_accepts_any_types()
    {
        TestCollection<Animal> c = new TestCollection<Animal>();
        Animal oneElement = new Animal( null! );
        c.Content.Add( oneElement );

        bool containsCalled = false;
        c.ContainsCalled += ( o, e ) => { containsCalled = true; };
        c.Contains( oneElement ).ShouldBeTrue();
        containsCalled.ShouldBeTrue( "It is our Contains method that is called." ); containsCalled = false;
        c.Contains( 56 ).ShouldBeFalse( "Contains should accept ANY object without any error." );
        containsCalled.ShouldBeTrue( "It is our Contains method that is called." ); containsCalled = false;
        c.Contains( null! ).ShouldBeFalse( "Contains should accept ANY object without any error." );
        containsCalled.ShouldBeTrue(); containsCalled = false;
    }

    [Test]
    public void IndexOf_on_IReadOnlyList()
    {
        IReadOnlyList<int> l = new[] { 3, 7, 9, 1, 3, 8 };
        l.IndexOf( i => i == 3 ).ShouldBe( 0 );
        l.IndexOf( i => i == 7 ).ShouldBe( 1 );
        l.IndexOf( i => i == 8 ).ShouldBe( 5 );
        l.IndexOf( i => i == 0 ).ShouldBe( -1 );
        Should.Throw<ArgumentNullException>( () => l.IndexOf( null! ) );
    }

}
