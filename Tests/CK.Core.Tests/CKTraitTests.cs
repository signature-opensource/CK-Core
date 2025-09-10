using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using System.Diagnostics;

namespace CK.Core.Tests;


/// <summary>
/// This class test operations on CKTrait (FindOrCreate, Intersect, etc.).
/// </summary>
public class CKTraitTests
{
    static CKTraitContext ContextWithPlusSeparator() => CKTraitContext.Create( "Test", '+' );

    [Test]
    public void Comparing_tags()
    {
        CKTraitContext c1 = CKTraitContext.Create( "C1" );
        CKTraitContext c2 = CKTraitContext.Create( "C2" );

        c1.CompareTo( c1 ).ShouldBe( 0 );
        c1.CompareTo( c2 ).ShouldBeLessThan( 0 );

        var tAc1 = c1.FindOrCreate( "A" );
        var tBc1 = c1.FindOrCreate( "B" );
        var tABc1 = c1.FindOrCreate( "A|B" );
        var tAc2 = c2.FindOrCreate( "A" );

        tAc1.CompareTo( tAc1 ).ShouldBe( 0 );
        tAc1.CompareTo( tBc1 ).ShouldBeGreaterThan( 0, "In the same context, A is stronger than B." );
        tABc1.CompareTo( tBc1 ).ShouldBeGreaterThan( 0, "In the same context, A|B is stronger than B." );
        tAc1.CompareTo( tAc2 ).ShouldBeLessThan( 0, "Between different contexts, the context ordering drives the ordering." );
        tABc1.CompareTo( tAc2 ).ShouldBeLessThan( 0, "Between different contexts, the context ordering drives the ordering." );
    }

    [Test]
    public void Tags_must_belong_to_the_same_context()
    {
        Action a = () => CKTraitContext.Create( null! );
        a.ShouldThrow<ArgumentException>();

        a = () => CKTraitContext.Create( "  " );
        a.ShouldThrow<ArgumentException>();

        CKTraitContext c1 = CKTraitContext.Create( "C1" );
        CKTraitContext c2 = CKTraitContext.Create( "C2" );

        var t1 = c1.FindOrCreate( "T1" );
        var t2 = c2.FindOrCreate( "T2" );
        t1.ShouldNotBeSameAs( t2 );
        Should.Throw<ArgumentException>( () => t1.Union( t2 ) );
        Should.Throw<ArgumentException>( () => t1.Intersect( t2 ) );
        Should.Throw<ArgumentException>( () => t1.Except( t2 ) );
        Should.Throw<ArgumentException>( () => t1.SymmetricExcept( t2 ) );

        Should.Throw<ArgumentException>( () => t1.Overlaps( t2 ) );
        Should.Throw<ArgumentException>( () => t1.IsSupersetOf( t2 ) );

        Should.Throw<ArgumentNullException>( () => t1.Union( null! ) );
        Should.Throw<ArgumentNullException>( () => t1.Intersect( null! ) );
        Should.Throw<ArgumentNullException>( () => t1.Except( null! ) );
        Should.Throw<ArgumentNullException>( () => t1.SymmetricExcept( null! ) );

        Should.Throw<ArgumentNullException>( () => t1.Overlaps( null! ) );
        Should.Throw<ArgumentNullException>( () => t1.IsSupersetOf( null! ) );
    }

    [Test]
    public void EmptyTag_is_everywhere()
    {
        var c = ContextWithPlusSeparator();
        CKTrait m = c.EmptyTrait;
        m.ToString().ShouldBeSameAs( string.Empty, "Empty tag is the empty string." );
        m.IsAtomic.ShouldBeTrue( "Empty tag is considered as atomic." );
        m.AtomicTraits.ShouldBeEmpty( "Empty tag has no atomic tags inside." );

        c.FindOrCreate( null! ).ShouldBeSameAs( m, "Null gives the empty tag." );
        c.FindOrCreate( "" ).ShouldBeSameAs( m, "Obtaining empty string gives the empty tag." );
        c.FindOrCreate( "+" ).ShouldBeSameAs( m, "Obtaining '+' gives the empty tag." );
        Should.Throw<ArgumentException>( () => c.FindOrCreate( " \t \n  " ), "No \n inside." );
        Should.Throw<ArgumentException>( () => c.FindOrCreate( " \r " ), "No \r inside." );
        c.FindOrCreate( "+ \t +" ).ShouldBeSameAs( m, "Leading and trailing '+' are ignored." );
        c.FindOrCreate( "++++" ).ShouldBeSameAs( m, "Multiple + are ignored" );
        c.FindOrCreate( "++  +++  + \t +" ).ShouldBeSameAs( m, "Multiple empty strings leads to empty tag." );

        c.FindOnlyExisting( null! ).ShouldBeNull();
        c.FindOnlyExisting( "" ).ShouldBeNull();
        c.FindOnlyExisting( " " ).ShouldBeNull();
        c.FindOnlyExisting( " ++  + " ).ShouldBeNull();
        c.FindOnlyExisting( "NONE" ).ShouldBeNull();
        c.FindOnlyExisting( "NO+NE" ).ShouldBeNull();
        c.FindOnlyExisting( "N+O+N+E" ).ShouldBeNull();
    }

    [Test]
    public void test_AtomicTag_parsing()
    {
        var c = ContextWithPlusSeparator();
        CKTrait m = c.FindOrCreate( "Alpha" );
        m.IsAtomic.ShouldBeTrue();
        m.AtomicTraits.Count.ShouldBe( 1, "Not a combined one." );
        m.AtomicTraits[0].ShouldBeSameAs( m, "Atomic tags are self-contained." );

        c.FindOrCreate( " \t Alpha\t\t  " ).ShouldBeSameAs( m, "Strings are trimmed." );
        c.FindOrCreate( "+ \t Alpha+" ).ShouldBeSameAs( m, "Leading and trailing '+' are ignored." );
        c.FindOrCreate( "+Alpha+++" ).ShouldBeSameAs( m, "Multiple + are ignored" );
        c.FindOrCreate( "++ Alpha +++ \t\t  + \t +" ).ShouldBeSameAs( m, "Multiple empty strings are ignored." );

        var notExist1 = Guid.NewGuid().ToString();
        var notExist2 = Guid.NewGuid().ToString();
        c.FindOnlyExisting( notExist1 ).ShouldBeNull();
        c.FindOnlyExisting( $"{notExist1}+{notExist2}" ).ShouldBeNull();
        c.FindOnlyExisting( "Alpha" ).ShouldBeSameAs( m );
        c.FindOnlyExisting( $"{notExist1}+{notExist2}+Alpha" ).ShouldBeSameAs( m );
    }

    [Test]
    public void test_Combined_tags_parsing()
    {
        var c = ContextWithPlusSeparator();

        CKTrait m = c.FindOrCreate( "Beta+Alpha" );
        m.IsAtomic.ShouldBeFalse();
        m.AtomicTraits.Count.ShouldBe( 2, "Combined tag." );
        m.AtomicTraits[0].ShouldBeSameAs( c.FindOrCreate( "Alpha" ), "Atomic Alpha is the first one." );
        m.AtomicTraits[1].ShouldBeSameAs( c.FindOrCreate( "Beta" ), "Atomic Beta is the second one." );

        c.FindOrCreate( "Alpha+Beta" ).ShouldBeSameAs( m, "Canonical order is ensured." );
        c.FindOrCreate( "+ +\t++ Alpha+++Beta++" ).ShouldBeSameAs( m, "Extra characters and empty tags are ignored." );

        c.FindOrCreate( "Alpha+Beta+Alpha" ).ShouldBeSameAs( m, "Multiple identical tags are removed." );
        c.FindOrCreate( "Alpha+ +Beta\t ++Beta+ + Alpha +    Beta   ++ " )
            .ShouldBeSameAs( m, "Multiple identical tags are removed." );

        CKTrait m2 = c.FindOrCreate( "Beta+Alpha+Zeta+Tau+Pi+Omega+Epsilon" );
        c.FindOrCreate( "++Beta+Zeta+Omega+Epsilon+Alpha+Zeta+Epsilon+Zeta+Tau+Epsilon+Pi+Tau+Beta+Zeta+Omega+Beta+Pi+Alpha" )
            .ShouldBeSameAs( m2, "Unicity of Atomic tag is ensured." );

        var notExists1 = Guid.NewGuid().ToString();
        var notExists2 = Guid.NewGuid().ToString();
        var notExists3 = Guid.NewGuid().ToString();
        c.FindOnlyExisting( "Beta" )!.ToString().ShouldBe( "Beta" );
        c.FindOnlyExisting( $"Beta+{notExists1}" )!.ToString().ShouldBe( "Beta" );
        c.FindOnlyExisting( $"Beta+{notExists1}+{notExists2}+Alpha+{notExists3}" )!.ShouldBeSameAs( m );
        c.FindOnlyExisting( $"Beta+  {notExists1} + {notExists2} +Alpha+{notExists3}+Tau+Pi" )!.ToString().ShouldBe( "Alpha+Beta+Pi+Tau" );
    }

    [Test]
    public void test_FindOnlyExisting_with_its_optional_collector()
    {
        var c = ContextWithPlusSeparator();

        List<string> collector = new List<string>();
        c.FindOrCreate( "Beta+Alpha+Tau+Pi" );

        var noExists1 = "A" + Guid.NewGuid().ToString();
        var noExists2 = "B" + Guid.NewGuid().ToString();
        var noExists3 = "C" + Guid.NewGuid().ToString();
        var noExists4 = "D" + Guid.NewGuid().ToString();
        var f = c.FindOnlyExisting( $"Beta+{noExists1}+{noExists2}+Alpha+{noExists3}+Tau+Pi+{noExists4}", t => { collector.Add( t ); return true; } );
        Debug.Assert( f != null );
        f.ToString().ShouldBe( "Alpha+Beta+Pi+Tau" );
        String.Join( ",", collector ).ShouldBe( $"{noExists1},{noExists2},{noExists3},{noExists4}" );

        collector.Clear();
        f = c.FindOnlyExisting( $"Beta+{noExists1}+{noExists2}+Alpha+{noExists3}+Tau+Pi", t => { collector.Add( t ); return t != noExists3; } );
        Debug.Assert( f != null );
        f.ToString().ShouldBe( "Alpha+Beta" );
        String.Join( ",", collector ).ShouldBe( $"{noExists1},{noExists2},{noExists3}" );
    }

    [Test]
    public void test_Intersect_between_tags()
    {
        var c = ContextWithPlusSeparator();

        CKTrait m1 = c.FindOrCreate( "Beta+Alpha+Fridge+Combo" );
        CKTrait m2 = c.FindOrCreate( "Xtra+Combo+Another+Fridge+Alt" );

        m1.Intersect( m2 ).ToString().ShouldBe( "Combo+Fridge", "Works as expected :-)" );
        m2.Intersect( m1 ).ShouldBeSameAs( m1.Intersect( m2 ), "Same object in both calls." );

        m2.Intersect( c.EmptyTrait ).ShouldBeSameAs( c.EmptyTrait, "Intersecting empty gives empty." );
    }

    [Test]
    public void test_Union_of_tags()
    {
        var c = ContextWithPlusSeparator();
        CKTrait m1 = c.FindOrCreate( "Beta+Alpha+Fridge+Combo" );
        CKTrait m2 = c.FindOrCreate( "Xtra+Combo+Another+Fridge+Alt" );

        m1.Union( m2 ).ToString().ShouldBe( "Alpha+Alt+Another+Beta+Combo+Fridge+Xtra", "Works as expected :-)" );
        m2.Union( m1 ).ShouldBeSameAs( m1.Union( m2 ), "Same in both calls." );
    }

    [Test]
    public void test_Except_of_tags()
    {
        var c = ContextWithPlusSeparator();
        CKTrait m1 = c.FindOrCreate( "Beta+Alpha+Fridge+Combo" );
        CKTrait m2 = c.FindOrCreate( "Xtra+Combo+Another+Fridge+Alt" );

        m1.Except( m2 ).ToString().ShouldBe( "Alpha+Beta", "Works as expected :-)" );
        m2.Except( m1 ).ToString().ShouldBe( "Alt+Another+Xtra", "Works as expected..." );

        m2.Except( c.EmptyTrait ).ShouldBeSameAs( m2, "Removing empty does nothing." );
        m1.Except( c.EmptyTrait ).ShouldBeSameAs( m1, "Removing empty does nothing." );
    }


    [Test]
    public void Contains_is_IsSupersetOf()
    {
        var c = ContextWithPlusSeparator();

        CKTrait m = c.FindOrCreate( "Beta+Alpha+Fridge+Combo" );

        c.EmptyTrait.IsSupersetOf( c.EmptyTrait ).ShouldBeTrue( "Empty is contained by definition in itself." );
        m.IsSupersetOf( c.EmptyTrait ).ShouldBeTrue( "Empty is contained by definition." );
        m.IsSupersetOf( c.FindOrCreate( "Fridge+Alpha" ) ).ShouldBeTrue();
        m.IsSupersetOf( c.FindOrCreate( "Fridge" ) ).ShouldBeTrue();
        m.IsSupersetOf( c.FindOrCreate( "Fridge+Alpha+Combo" ) ).ShouldBeTrue();
        m.IsSupersetOf( c.FindOrCreate( "Fridge+Alpha+Beta+Combo" ) ).ShouldBeTrue();
        m.IsSupersetOf( c.FindOrCreate( "Fridge+Lol" ) ).ShouldBeFalse();
        m.IsSupersetOf( c.FindOrCreate( "Murfn" ) ).ShouldBeFalse();
        m.IsSupersetOf( c.FindOrCreate( "Fridge+Alpha+Combo+Lol" ) ).ShouldBeFalse();
        m.IsSupersetOf( c.FindOrCreate( "Lol+Fridge+Alpha+Beta+Combo" ) ).ShouldBeFalse();

        m.Overlaps( c.FindOrCreate( "Fridge+Alpha" ) ).ShouldBeTrue();
        m.Overlaps( c.FindOrCreate( "Nimp+Fridge+Mourfn" ) ).ShouldBeTrue();
        m.Overlaps( c.FindOrCreate( "Fridge+Alpha+Combo+Albert" ) ).ShouldBeTrue();
        m.Overlaps( c.FindOrCreate( "ZZF+AAlp+BBeBe+Combo" ) ).ShouldBeTrue();
        m.Overlaps( c.FindOrCreate( "AFridge+ALol" ) ).ShouldBeFalse();
        m.Overlaps( c.FindOrCreate( "Murfn" ) ).ShouldBeFalse();
        m.Overlaps( c.FindOrCreate( "QF+QA+QC+QL" ) ).ShouldBeFalse();
        m.Overlaps( c.EmptyTrait ).ShouldBeFalse( "Empty is NOT contained 'ONE' since EmptyTag.AtomicTags.Count == 0..." );
        c.EmptyTrait.Overlaps( c.EmptyTrait ).ShouldBeFalse( "Empty is NOT contained 'ONE' in itself." );
    }

    [Test]
    public void tag_separator_can_be_changed_from_the_default_pipe()
    {
        var c = CKTraitContext.Create( "SemiColonContext", ';' );
        CKTrait m = c.FindOrCreate( "Beta;Alpha;Fridge;Combo" );

        c.EmptyTrait.IsSupersetOf( c.EmptyTrait ).ShouldBeTrue( "Empty is contained by definition in itself." );
        m.IsSupersetOf( c.EmptyTrait ).ShouldBeTrue( "Empty is contained by definition." );
        m.IsSupersetOf( c.FindOrCreate( "Fridge;Alpha" ) ).ShouldBeTrue();
        m.IsSupersetOf( c.FindOrCreate( "Fridge" ) ).ShouldBeTrue();
        m.IsSupersetOf( c.FindOrCreate( "Fridge;Alpha;Combo" ) ).ShouldBeTrue();
        m.IsSupersetOf( c.FindOrCreate( "Fridge;Alpha;Beta;Combo" ) ).ShouldBeTrue();
        m.IsSupersetOf( c.FindOrCreate( "Fridge;Lol" ) ).ShouldBeFalse();
        m.IsSupersetOf( c.FindOrCreate( "Murfn" ) ).ShouldBeFalse();
        m.IsSupersetOf( c.FindOrCreate( "Fridge;Alpha;Combo+Lol" ) ).ShouldBeFalse();
        m.IsSupersetOf( c.FindOrCreate( "Lol;Fridge;Alpha;Beta;Combo" ) ).ShouldBeFalse();

        m.Overlaps( c.FindOrCreate( "Fridge;Alpha" ) ).ShouldBeTrue();
        m.Overlaps( c.FindOrCreate( "Nimp;Fridge;Mourfn" ) ).ShouldBeTrue();
        m.Overlaps( c.FindOrCreate( "Fridge;Alpha;Combo;Albert" ) ).ShouldBeTrue();
        m.Overlaps( c.FindOrCreate( "ZZF;AAlp;BBeBe;Combo" ) ).ShouldBeTrue();
        m.Overlaps( c.FindOrCreate( "AFridge;ALol" ) ).ShouldBeFalse();
        m.Overlaps( c.FindOrCreate( "Murfn" ) ).ShouldBeFalse();
        m.Overlaps( c.FindOrCreate( "QF;QA;QC;QL" ) ).ShouldBeFalse();
        m.Overlaps( c.EmptyTrait ).ShouldBeFalse( "Empty is NOT contained 'ONE' since EmptyTag.AtomicTags.Count == 0..." );
        c.EmptyTrait.Overlaps( c.EmptyTrait ).ShouldBeFalse( "Empty is NOT contained 'ONE' in itself." );
    }

    [Test]
    public void Toggle_is_SymmetricExcept()
    {
        var c = ContextWithPlusSeparator();
        CKTrait m = c.FindOrCreate( "Beta+Alpha+Fridge+Combo" );
        m.SymmetricExcept( c.FindOrCreate( "Beta" ) ).ToString().ShouldBe( "Alpha+Combo+Fridge" );
        m.SymmetricExcept( c.FindOrCreate( "Fridge+Combo" ) ).ToString().ShouldBe( "Alpha+Beta" );
        m.SymmetricExcept( c.FindOrCreate( "Beta+Fridge+Combo" ) ).ToString().ShouldBe( "Alpha" );
        m.SymmetricExcept( c.FindOrCreate( "Beta+Fridge+Combo+Alpha" ) ).ToString().ShouldBe( "" );

        m.SymmetricExcept( c.FindOrCreate( "" ) ).ToString().ShouldBe( "Alpha+Beta+Combo+Fridge" );
        m.SymmetricExcept( c.FindOrCreate( "Xtra" ) ).ToString().ShouldBe( "Alpha+Beta+Combo+Fridge+Xtra" );
        m.SymmetricExcept( c.FindOrCreate( "Alpha+Xtra" ) ).ToString().ShouldBe( "Beta+Combo+Fridge+Xtra" );
        m.SymmetricExcept( c.FindOrCreate( "Zenon+Alpha+Xtra+Fridge" ) ).ToString().ShouldBe( "Beta+Combo+Xtra+Zenon" );
    }

    [Test]
    public void Fallbacks_generation()
    {
        var c = ContextWithPlusSeparator();
        {
            CKTrait m = c.FindOrCreate( "" );
            IReadOnlyList<CKTrait> f = m.Fallbacks.ToArray();
            m.FallbacksCount.ShouldBe( f.Count );
            f.Count.ShouldBe( 1 );
            f[0].ToString().ShouldBe( "" );
        }
        {
            CKTrait m = c.FindOrCreate( "Alpha" );
            IReadOnlyList<CKTrait> f = m.Fallbacks.ToArray();
            m.FallbacksCount.ShouldBe( f.Count );
            f.Count.ShouldBe( 1 );
            f[0].ToString().ShouldBe( "" );
        }
        {
            CKTrait m = c.FindOrCreate( "Alpha+Beta" );
            IReadOnlyList<CKTrait> f = m.Fallbacks.ToArray();
            m.FallbacksCount.ShouldBe( f.Count );
            f.Count.ShouldBe( 3 );
            f[0].ToString().ShouldBe( "Alpha" );
            f[1].ToString().ShouldBe( "Beta" );
            f[2].ToString().ShouldBe( "" );
        }
        {
            CKTrait m = c.FindOrCreate( "Alpha+Beta+Combo" );
            IReadOnlyList<CKTrait> f = m.Fallbacks.ToArray();
            m.FallbacksCount.ShouldBe( f.Count );
            f.Count.ShouldBe( 7 );
            f[0].ToString().ShouldBe( "Alpha+Beta" );
            f[1].ToString().ShouldBe( "Alpha+Combo" );
            f[2].ToString().ShouldBe( "Beta+Combo" );
            f[3].ToString().ShouldBe( "Alpha" );
            f[4].ToString().ShouldBe( "Beta" );
            f[5].ToString().ShouldBe( "Combo" );
            f[6].ToString().ShouldBe( "" );
        }
        {
            CKTrait m = c.FindOrCreate( "Alpha+Beta+Combo+Fridge" );
            IReadOnlyList<CKTrait> f = m.Fallbacks.ToArray();
            m.FallbacksCount.ShouldBe( f.Count );
            f.Count.ShouldBe( 15 );
            f[0].ToString().ShouldBe( "Alpha+Beta+Combo" );
            f[1].ToString().ShouldBe( "Alpha+Beta+Fridge" );
            f[2].ToString().ShouldBe( "Alpha+Combo+Fridge" );
            f[3].ToString().ShouldBe( "Beta+Combo+Fridge" );
            f[4].ToString().ShouldBe( "Alpha+Beta" );
            f[5].ToString().ShouldBe( "Alpha+Combo" );
            f[6].ToString().ShouldBe( "Alpha+Fridge" );
            f[7].ToString().ShouldBe( "Beta+Combo" );
            f[8].ToString().ShouldBe( "Beta+Fridge" );
            f[9].ToString().ShouldBe( "Combo+Fridge" );
            f[10].ToString().ShouldBe( "Alpha" );
            f[11].ToString().ShouldBe( "Beta" );
            f[12].ToString().ShouldBe( "Combo" );
            f[13].ToString().ShouldBe( "Fridge" );
            f[14].ToString().ShouldBe( "" );
        }
    }

    [Test]
    public void Fallbacks_ordering()
    {
        var c = ContextWithPlusSeparator();
        {
            CKTrait m = c.FindOrCreate( "Alpha+Beta+Combo+Fridge" );
            IReadOnlyList<CKTrait> f = m.Fallbacks.ToArray();

            CKTrait[] sorted = f.ToArray();
            Array.Sort( sorted );
            Array.Reverse( sorted );
            sorted.SequenceEqual( f ).ShouldBeTrue( "CKTrait.CompareTo respects the fallbacks (fallbacks is in reverse order)." );
        }
        {
            CKTrait m = c.FindOrCreate( "Alpha+Beta+Combo+Fridge+F+K+Ju+J+A+B" );
            IReadOnlyList<CKTrait> f = m.Fallbacks.ToArray();
            f.OrderBy( tag => tag ).Reverse().SequenceEqual( f ).ShouldBeTrue( "CKTrait.CompareTo is ok." );
        }
        {
            CKTrait m = c.FindOrCreate( "xz+lz+ded+az+zer+t+zer+ce+ret+ert+ml+a+nzn" );
            IReadOnlyList<CKTrait> f = m.Fallbacks.ToArray();
            f.OrderBy( tag => tag ).Reverse().SequenceEqual( f ).ShouldBeTrue( "CKTrait.CompareTo is ok." );
        }
    }

    [Test]
    public void FindIfAllExist_tests()
    {
        var c = CKTraitContext.Create( "Indep", '+', shared: false );

        CKTrait m = c.FindOrCreate( "Alpha+Beta+Combo+Fridge" );

        c.FindIfAllExist( "" ).ShouldBe( c.EmptyTrait );
        c.FindIfAllExist( "bo" ).ShouldBeNull();
        var alpha = c.FindOrCreate( "Alpha" );
        c.FindIfAllExist( "Alpha" ).ShouldBe( alpha );
        c.FindIfAllExist( "bo+pha" ).ShouldBeNull();
        c.FindIfAllExist( "Fridge+Combo+Alpha+Beta" ).ShouldBeSameAs( m );
    }

    [Test]
    public void independent_or_shared_contexts()
    {
        var shared = CKTraitContext.Create( "Shared", '+' );
        Action notPossible = () => CKTraitContext.Create( "Shared", '-' );
        notPossible.ShouldThrow<InvalidOperationException>();

        var here = shared.FindOrCreate( "Here!" );

        var independent1 = CKTraitContext.Create( "Shared", '-', shared: false );
        independent1.FindOnlyExisting( "Here!" ).ShouldBeNull();

        independent1.FindOrCreate( "In Independent n°1" );

        var independent2 = CKTraitContext.Create( "Shared", '-', shared: false );
        independent2.FindOnlyExisting( "Here!" ).ShouldBeNull();
        independent2.FindOnlyExisting( "In Independent n°1" ).ShouldBeNull();

        var anotherShared = CKTraitContext.Create( "Shared", '+' );
        anotherShared.FindOnlyExisting( "Here!" ).ShouldBeSameAs( here );

        (anotherShared == shared).ShouldBeTrue();
        (anotherShared != shared).ShouldBeFalse();
        anotherShared.Equals( shared ).ShouldBeTrue();
        anotherShared.CompareTo( shared ).ShouldBe( 0 );

        var here1 = independent1.FindOrCreate( "Here!" );
        var here2 = independent2.FindOrCreate( "Here!" );

        here.ShouldNotBeSameAs( here1 );
        here.ShouldNotBeSameAs( here2 );
        here1.ShouldNotBeSameAs( here2 );

        // Comparisons across contexts: a unique index "sorts" the independent contexts.
        // This index is positive for independent contexts: shared context's tags come first.

        here.CompareTo( here1 ).ShouldBeNegative();
        here1.CompareTo( here2 ).ShouldBeNegative();
        here.CompareTo( here2 ).ShouldBeNegative();

        here1.CompareTo( here ).ShouldBePositive();
        here2.CompareTo( here1 ).ShouldBePositive();
        here2.CompareTo( here ).ShouldBePositive();
    }

    [Test]
    public void comparison_operators_work()
    {
        var c = CKTraitContext.Create( "Indep", '+', shared: false );
        var t1 = c.FindOrCreate( "A" );
        var t2 = c.FindOrCreate( "B" );
        //
        (t1 > t2).ShouldBeTrue( "A is better than B." );
        (t1 >= t2).ShouldBeTrue();
        (t1 != t2).ShouldBeTrue();
        (t1 == t2).ShouldBeFalse();
        (t1 < t2).ShouldBeFalse();
        (t1 <= t2).ShouldBeFalse();
        //
        t2 = null;
        (t1 > t2).ShouldBeTrue( "Any tag is better than null." );
        (t1 >= t2).ShouldBeTrue();
        (t1 != t2).ShouldBeTrue();
        (t1 == t2).ShouldBeFalse();
        (t1 < t2).ShouldBeFalse();
        (t1 <= t2).ShouldBeFalse();
        //
        t1 = null;
        (t1 > t2).ShouldBeFalse( "null is the same as null." );
        (t1 < t2).ShouldBeFalse();
        (t1 >= t2).ShouldBeTrue( "null is the same as null." );
        (t1 <= t2).ShouldBeTrue();
        (t1 == t2).ShouldBeTrue();
        (t1 != t2).ShouldBeFalse();
        //
        t2 = c.FindOrCreate( "B" );
        (t1 > t2).ShouldBeFalse( "null is smaller that any tag." );
        (t1 >= t2).ShouldBeFalse();
        (t1 != t2).ShouldBeTrue();
        (t1 == t2).ShouldBeFalse();
        (t1 < t2).ShouldBeTrue();
        (t1 <= t2).ShouldBeTrue();
    }

    [Test]
    public void comparison_operators_work_cross_contexts()
    {
        var c = CKTraitContext.Create( "Indep", '+', shared: false );
        var cAfter = CKTraitContext.Create( "Indep+", '+', shared: false );
        var t1 = cAfter.FindOrCreate( "B" );
        var t2 = c.FindOrCreate( "B" );
        //
        (t1 > t2).ShouldBeTrue( "Tag with same name relies on context's separator and then name ('greater' separator and then name are better)." );
        (t1 >= t2).ShouldBeTrue();
        (t1 != t2).ShouldBeTrue();
        (t1 == t2).ShouldBeFalse();
        (t1 < t2).ShouldBeFalse();
        (t1 <= t2).ShouldBeFalse();
    }

    [Test]
    public void binary_operators_work()
    {
        var ctx = CKTraitContext.Create( "Indep", ',', shared: false );
        var a = ctx.FindOrCreate( "A" );
        var b = ctx.FindOrCreate( "B" );
        var c = ctx.FindOrCreate( "C" );
        var d = ctx.FindOrCreate( "D" );
        var ab = ctx.FindOrCreate( "A,B" );
        var cd = ctx.FindOrCreate( "C,D" );
        var abc = ctx.FindOrCreate( "A,B,C" );
        var abcd = ctx.FindOrCreate( "A,B,C,D" );
        (ab + cd).ShouldBeSameAs( abcd );
        (ab + cd).ShouldBeSameAs( c + d + b + a );
        (ab + cd).ShouldBeSameAs( cd + ab );
        (ab + cd).ShouldBeSameAs( abc | d );
        (ab + cd).ShouldBeSameAs( a | b | c | d );

        (ab & cd).ShouldBeSameAs( ctx.EmptyTrait );
        (abc & cd).ShouldBeSameAs( c );
        (abcd & cd).ShouldBeSameAs( cd );

        (ab ^ cd).ShouldBeSameAs( abcd );
        (abc ^ cd).ShouldBeSameAs( a | b | d );
        ((a | b | d) ^ cd).ShouldBeSameAs( abc );
        (abcd ^ cd).ShouldBeSameAs( ab );

        CKTrait v = a;
        v += b;
        v.ShouldBeSameAs( ab );
        v += d;
        v.ShouldBeSameAs( a | b | d );
        v -= c;
        v.ShouldBeSameAs( a | b | d );
        v -= abcd;
        v.IsEmpty.ShouldBeTrue();

        v |= abcd;
        v &= cd;
        v.ShouldBeSameAs( cd );
        v &= d;
        v.ShouldBeSameAs( d );
        v |= abcd;
        v.ShouldBeSameAs( abcd );
        v ^= b;
        v.ShouldBeSameAs( a | cd );
        v ^= ab;
        v.ShouldBeSameAs( b | cd );

        v &= ctx.EmptyTrait;
        v.IsEmpty.ShouldBeTrue();
    }

    [Test]
    public void Binary_serialization_of_CKTraitCOntext()
    {
        var ctx = CKTraitContext.Create( "Independent", ',', shared: false );
        ctx.FindOrCreate( "A" );
        ctx.FindOrCreate( "B" );
        ctx.FindOrCreate( "C" );
        ctx.FindOrCreate( "D" );
        ctx.FindOrCreate( "A,B" );

        using( var m = Util.RecyclableStreamManager.GetStream() )
        using( var w = new CKBinaryWriter( m ) )
        {
            ctx.Write( w, writeAllTags: true );
            m.Position = 0;
            using( var r = new CKBinaryReader( m ) )
            {
                var ctx2 = CKTraitContext.Read( r );
                ctx2.FindIfAllExist( "A,B,C,D" ).ShouldNotBeNull();
            }
        }
    }
}
