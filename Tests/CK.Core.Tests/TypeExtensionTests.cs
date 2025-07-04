using Shouldly;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Diagnostics;

namespace CK.Core.Tests;

[TestFixture]
public class TypeExtensionTests
{
#pragma warning disable CS0693
    public class Above<T1>
    {
        // This triggers a CS0693 warning:
        // Type parameter 'T1' has the same name as the type parameter from outer type 'ToCSharpNameTests.Above<T1>'
        public class Below<T1>
        {
        }
    }
#pragma warning restore CS0693

    public class G<T>
    {
        public class Nested
        {
            public A<int?>.C<string>? Prop { get; set; }
        }
    }

    public class A<TB>
    {
        public class C<TD> { }
    }

    public class H<T1, T2> { }

    public class Another
    {
        public class I<T3> { }
    }

    [TestCase( typeof( Dictionary<,>.KeyCollection ), "System.Collections.Generic.Dictionary<,>.KeyCollection" )]
    [TestCase( typeof( Dictionary<int, string>.KeyCollection ), "System.Collections.Generic.Dictionary<int,string>.KeyCollection" )]
    [TestCase( typeof( Above<>.Below<> ), "CK.Core.Tests.TypeExtensionTests.Above<>.Below<>" )]
    [TestCase( typeof( Above<long>.Below<int> ), "CK.Core.Tests.TypeExtensionTests.Above<long>.Below<int>" )]
    [TestCase( typeof( TypeExtensionTests ), "CK.Core.Tests.TypeExtensionTests" )]
    [TestCase( typeof( List<string> ), "System.Collections.Generic.List<string>" )]
    [TestCase( typeof( List<Dictionary<int, string>> ), "System.Collections.Generic.List<System.Collections.Generic.Dictionary<int,string>>" )]
    [TestCase( typeof( Nullable<Guid> ), "System.Guid?" )]
    [TestCase( typeof( Guid? ), "System.Guid?" )]
    [TestCase( typeof( Another ), "CK.Core.Tests.TypeExtensionTests.Another" )]
    [TestCase( typeof( G<> ), "CK.Core.Tests.TypeExtensionTests.G<>" )]
    [TestCase( typeof( G<string> ), "CK.Core.Tests.TypeExtensionTests.G<string>" )]
    [TestCase( typeof( G<Another> ), "CK.Core.Tests.TypeExtensionTests.G<CK.Core.Tests.TypeExtensionTests.Another>" )]
    [TestCase( typeof( H<,> ), "CK.Core.Tests.TypeExtensionTests.H<,>" )]
    [TestCase( typeof( H<string, Another> ), "CK.Core.Tests.TypeExtensionTests.H<string,CK.Core.Tests.TypeExtensionTests.Another>" )]
    [TestCase( typeof( Another.I<> ), "CK.Core.Tests.TypeExtensionTests.Another.I<>" )]
    [TestCase( typeof( Another.I<int> ), "CK.Core.Tests.TypeExtensionTests.Another.I<int>" )]
    [TestCase( typeof( G<>.Nested ), "CK.Core.Tests.TypeExtensionTests.G<>.Nested" )]
    [TestCase( typeof( G<string>.Nested ), "CK.Core.Tests.TypeExtensionTests.G<string>.Nested" )]
    [TestCase( typeof( A<>.C<> ), "CK.Core.Tests.TypeExtensionTests.A<>.C<>" )]
    [TestCase( typeof( A<int>.C<string> ), "CK.Core.Tests.TypeExtensionTests.A<int>.C<string>" )]
    public void ToCSharpName_without_generic_parameter_names( Type type, string expected )
    {
        type.ToCSharpName( typeDeclaration: false ).ShouldBe( expected );
    }

    [TestCase( typeof( List<IEnumerable<string>> ), "List<IEnumerable<string>>" )]
    [TestCase( typeof( H<string, Another> ), "TypeExtensionTests.H<string,TypeExtensionTests.Another>" )]
    public void ToCSharpName_without_namespace( Type type, string expected )
    {
        type.ToCSharpName( withNamespace: false ).ShouldBe( expected );
    }

    [TestCase( typeof( long* ), "long*" )]
    [TestCase( typeof( Guid*** ), "System.Guid***" )]
    public void ToCSharpName_handles_pointers( Type type, string expected )
    {
        type.ToCSharpName().ShouldBe( expected );
    }

    [Test]
    public void ToCSharpName_handles_byRef()
    {
        typeof( long ).MakeByRefType().ToCSharpName().ShouldBe( "long&" );
        typeof( List<int> ).MakeByRefType().ToCSharpName().ShouldBe( "System.Collections.Generic.List<int>&" );
        typeof( long ).MakeByRefType().ToCSharpName().ShouldBe( "long&" );
    }

    [TestCase( typeof( Dictionary<,>.KeyCollection ), "System.Collections.Generic.Dictionary<TKey,TValue>.KeyCollection" )]
    [TestCase( typeof( Dictionary<int, string>.KeyCollection ), "System.Collections.Generic.Dictionary<int,string>.KeyCollection" )]
    [TestCase( typeof( Above<>.Below<> ), "CK.Core.Tests.TypeExtensionTests.Above<T1>.Below<T1>" )]
    [TestCase( typeof( Nullable<> ), "System.Nullable<T>" )]
    [TestCase( typeof( G<> ), "CK.Core.Tests.TypeExtensionTests.G<T>" )]
    [TestCase( typeof( H<,> ), "CK.Core.Tests.TypeExtensionTests.H<T1,T2>" )]
    [TestCase( typeof( Another.I<> ), "CK.Core.Tests.TypeExtensionTests.Another.I<T3>" )]
    [TestCase( typeof( G<>.Nested ), "CK.Core.Tests.TypeExtensionTests.G<T>.Nested" )]
    [TestCase( typeof( A<>.C<> ), "CK.Core.Tests.TypeExtensionTests.A<TB>.C<TD>" )]
    [TestCase( typeof( int[] ), "int[]" )]
    [TestCase( typeof( Byte[,,,] ), "byte[,,,]" )]
    [TestCase( typeof( int? ), "int?" )]
    [TestCase( typeof( int?[] ), "int?[]" )]
    public void ToCSharpName_with_default_parameters( Type type, string expected )
    {
        type.ToCSharpName().ShouldBe( expected );
    }

    class Nested<T> { }


    [TestCase( true, typeof( ValueTuple<int?, string> ), true, "(int?,string)" )]
    [TestCase( true, typeof( (int?, string) ), false, "System.ValueTuple<int?,string>" )]
    [TestCase( true, typeof( (int, (string, float?)) ), true, "(int,(string,float?))" )]
    [TestCase( true, typeof( (int, (string, float?)) ), false, "System.ValueTuple<int,System.ValueTuple<string,float?>>" )]
    // 8 Slots: the rest must be handled.
    // Note the trailing ValueTuple<T> (singleton) that is chained.
    [TestCase( true, typeof( (int, string, float, int, string, float, int, float?) ), true, "(int,string,float,int,string,float,int,float?)" )]
    [TestCase( true, typeof( (int, string, float, int, string, float, int, float?) ), false, "System.ValueTuple<int,string,float,int,string,float,int,System.ValueTuple<float?>>" )]
    // And more...
    [TestCase( true, typeof( (int, string, float, int, string, float, int?, string?, double?, float?) ),
               true, "(int,string,float,int,string,float,int?,string,double?,float?)" )]
    [TestCase( true, typeof( (int, string, float, int, string, float, int?, string?, double?, float?) ),
               false, "System.ValueTuple<int,string,float,int,string,float,int?,System.ValueTuple<string,double?,float?>>" )]
    //
    [TestCase( false, typeof( Nested<Dictionary<int, (string, int)>> ), true, "TypeExtensionTests.Nested<Dictionary<int,(string,int)>>" )]
    [TestCase( false, typeof( Nested<Dictionary<int, (string, int)>> ), false, "TypeExtensionTests.Nested<Dictionary<int,ValueTuple<string,int>>>" )]
    public void ToCSharpName_for_value_tuples( bool withNamespace, Type t, bool useValueTupleParentheses, string expected )
    {
        t.ToCSharpName( withNamespace, useValueTupleParentheses: useValueTupleParentheses )
            .ShouldBe( expected );
    }

    [TestCase( false, typeof( ValueTuple<,,,,,,,> ), true, "ValueTuple<T1,T2,T3,T4,T5,T6,T7,TRest>" )]
    [TestCase( false, typeof( ValueTuple<,,,,,,,> ), false, "ValueTuple<T1,T2,T3,T4,T5,T6,T7,TRest>" )]
    public void ToCSharpName_for_type_definition( bool withNamespace, Type t, bool useValueTupleParentheses, string expected )
    {
        t.ToCSharpName( withNamespace, useValueTupleParentheses: useValueTupleParentheses )
            .ShouldBe( expected );
    }

    [Test]
    public void ToCSharpName_Tuple_long()
    {
        var mRef = GetType().GetMethod( "CreateLongRef", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static );
        mRef.ShouldNotBeNull()
            .ReturnType.ToCSharpName().ShouldBe( "System.Tuple<T1,T2,T3,T4,T5,T6,T7,TRest>" );

        var mVal = GetType().GetMethod( "CreateLong", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static );
        mVal.ShouldNotBeNull()
            .ReturnType.ToCSharpName().ShouldBe( "System.ValueTuple<T1,T2,T3,T4,T5,T6,T7,TRest>" );
        // Note that ToCSharpName works even if here, AssemblyQualifiedName and FullName of the ReturnType are both null.
        mVal.ReturnType.FullName.ShouldBeNull();
        mVal.ReturnType.AssemblyQualifiedName.ShouldBeNull();
    }

    static ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> CreateLong<T1, T2, T3, T4, T5, T6, T7, TRest>( T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, TRest rest )
            where TRest : struct, ITuple
            => new ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>( item1, item2, item3, item4, item5, item6, item7, rest );

    static Tuple<T1, T2, T3, T4, T5, T6, T7, TRest> CreateLongRef<T1, T2, T3, T4, T5, T6, T7, TRest>( T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, TRest rest )
        where TRest : ITuple
        => new Tuple<T1, T2, T3, T4, T5, T6, T7, TRest>( item1, item2, item3, item4, item5, item6, item7, rest );

    [Test]
    public void GetWeakAssemblyQualifiedName_test()
    {
        foreach( var t in AppDomain.CurrentDomain.GetAssemblies().SelectMany( a => a.ExportedTypes ) )
        {
            var clean = t.GetWeakAssemblyQualifiedName().ShouldNotBeNull();
            var resolved = Type.GetType( clean, throwOnError: true )!;
            // No reference equality pitfalls.
            (t == resolved).ShouldBe( t.Equals( resolved ) );

            // Load contexts are different for some assemblies (VSTest, NUnit...).
            // In this case, type instances are not the same but the type is actually the same.
            // This is why CK.Engine never plays with LoadContext, we always work in the default load context.
            if( t != resolved )
            {
                // No ActivityMonitor here...
                Debug.WriteLine( $"Type '{t.ToCSharpName()}': WeakAssemblyQualifiedName='{clean}', resolved: '{resolved.ToCSharpName()}'." );
                resolved.AssemblyQualifiedName.ShouldBe( t.AssemblyQualifiedName );

                var resolved2 = Type.GetType( clean, throwOnError: true )!;
                resolved2.ShouldBeSameAs( resolved );

                var resolvedFromAQN = Type.GetType( t.AssemblyQualifiedName!, throwOnError: true )!;
                resolvedFromAQN.ShouldBeSameAs( resolved );

            }
        }
    }

}
