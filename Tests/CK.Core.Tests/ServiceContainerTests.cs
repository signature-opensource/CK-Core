using Shouldly;
using System;
using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;

namespace CK.Core.Tests;


#region IAddService

public interface IAddServiceBase
{
}

public interface IAddService : IAddServiceBase
{
    int Add( int a, int b );
}

public interface IAddServiceDerived : IAddService
{
}

public class AddServiceImpl : IAddService
{
    public int Add( int a, int b )
    {
        return a + b;
    }
}

#endregion

#region ISubstractService

public interface ISubstractService
{
    int Substract( int a, int b );
}
public class SubstractServiceImpl : ISubstractService
{
    public int Substract( int a, int b )
    {
        return a - b;
    }
}

#endregion

#region IMultService

public interface IMultService
{
    int Mult( int a, int b );
}

public class MultServiceImpl : IMultService
{
    public int Mult( int a, int b )
    {
        return a * b;
    }
}

#endregion

public class ProvidedClass
{
    public int Age { get; set; }

    public ProvidedClass( int age )
    {
        Age = age;
    }
}

public class DisposableThatReenterClearWhenDisposed : IDisposable
{
    public ISimpleServiceContainer? ServiceContainer { get; set; }

    public void Dispose()
    {
        ServiceContainer?.Clear();
    }
}

public class EmptyClass
{
}


[ExcludeFromCodeCoverage]
public class ServiceContainerTests
{
    [Test]
    public void registering_a_simple_class()
    {
        ISimpleServiceContainer container = new SimpleServiceContainer();
        ProvidedClass providedClass = new ProvidedClass( 5 );
        container.Add( providedClass );

        // NotNullWhen works.
        if( container.TryGetService<ProvidedClass>( out var retrievedObject, false ) )
        {
            retrievedObject.ShouldNotBeNull();
            retrievedObject.Age.ShouldBe( 5 );
        }
        else Assert.Fail();
    }


    [Test]
    public void registering_an_implementation()
    {
        int removedServicesCount = 0;

        IServiceContainerConformanceTest( new SimpleServiceContainer() );

        ISimpleServiceContainer baseProvider = new SimpleServiceContainer();
        IMultService multService = new MultServiceImpl();
        multService.Mult( 3, 7 ).ShouldBe( 21 );
        baseProvider.Add( typeof( IMultService ), multService, o => removedServicesCount++ );
        ISimpleServiceContainer container = new SimpleServiceContainer( baseProvider );

        IServiceContainerConformanceTest( container, baseProvider, baseProvider.GetService<IMultService>( false ) );

        removedServicesCount.ShouldBe( 1 );
    }

    [Test]
    public void removing_a_registered_service()
    {
        int removedServicesCount = 0;

        SimpleServiceContainer container = new SimpleServiceContainer();
        container.Add( typeof( IAddService ), new AddServiceImpl(), o => removedServicesCount++ );

        IAddService service = container.GetService<IAddService>( false );
        service.ShouldBeOfType<AddServiceImpl>();

        removedServicesCount.ShouldBe( 0 );
        container.Remove( typeof( IAddService ) );
        removedServicesCount.ShouldBe( 1 );

        container.GetService( typeof( IAddService ) ).ShouldBeNull();
        container.GetService<IAddService>( false ).ShouldBeNull();
        Should.Throw<Exception>( () => container.GetService<IAddService>( true ) );
    }

    [Test]
    public void clearing_a_container_disposes_all_its_registered_IDisposable_objects_and_remove_reentrancy_is_handled()
    {
        SimpleServiceContainer container = new SimpleServiceContainer();
        DisposableThatReenterClearWhenDisposed disposableClass = new DisposableThatReenterClearWhenDisposed();
        disposableClass.ServiceContainer = container;
        container.Add( disposableClass, Util.ActionDispose );

        DisposableThatReenterClearWhenDisposed service = container.GetService<DisposableThatReenterClearWhenDisposed>( false );
        service.ShouldNotBeNull();
        service.GetType().ShouldBe( typeof( DisposableThatReenterClearWhenDisposed ) );

        container.Clear();

        container.GetService( typeof( DisposableThatReenterClearWhenDisposed ) ).ShouldBeNull();
        container.GetService<DisposableThatReenterClearWhenDisposed>( false ).ShouldBeNull();
        Should.Throw<Exception>( () => container.GetService<DisposableThatReenterClearWhenDisposed>( true ) );
    }

    [Test]
    public void using_onRemove_action_reentrancy_of_remove_is_handled()
    {

        SimpleServiceContainer container = new SimpleServiceContainer();
        DisposableThatReenterClearWhenDisposed disposableClass = new DisposableThatReenterClearWhenDisposed();
        EmptyClass stupidObject = new EmptyClass();

        disposableClass.ServiceContainer = container;
        container.Add( disposableClass, Util.ActionDispose );
        container.Add( stupidObject, e => container.Clear() );

        DisposableThatReenterClearWhenDisposed service = container.GetService<DisposableThatReenterClearWhenDisposed>( false );
        service.ShouldBeOfType<DisposableThatReenterClearWhenDisposed>();

        container.Remove<EmptyClass>();

        container.GetService( typeof( DisposableThatReenterClearWhenDisposed ) ).ShouldBeNull();
        container.GetService<DisposableThatReenterClearWhenDisposed>( false ).ShouldBeNull();
        Should.Throw<Exception>( () => container.GetService<DisposableThatReenterClearWhenDisposed>( true ) );
    }

    [Test]
    public void SimpleServiceContainer_exposes_its_own_IServiceProvier_and_ISimpleServiceContainer_implementation()
    {
        SimpleServiceContainer container = new SimpleServiceContainer();
        container.GetService<IServiceProvider>( false ).ShouldBeSameAs( container );
        container.GetService<ISimpleServiceContainer>( false ).ShouldBeSameAs( container );

        Should.Throw<Exception>( () => container.Add<ISimpleServiceContainer>( container ) );
        Should.Throw<Exception>( () => container.Add<ISimpleServiceContainer>( new SimpleServiceContainer() ) );
        Should.Throw<Exception>( () => container.Add<ISimpleServiceContainer>( JustAFunc<ISimpleServiceContainer> ) );

        Should.Throw<Exception>( () => container.Add<IServiceProvider>( container ) );
        Should.Throw<Exception>( () => container.Add<IServiceProvider>( new SimpleServiceContainer() ) );
        Should.Throw<Exception>( () => container.Add<IServiceProvider>( JustAFunc<IServiceProvider> ) );
        Should.Throw<Exception>( () => container.AddDisabled( typeof( IServiceProvider ) ) );
    }

    [Test]
    public void when_registering_types_they_must_match()
    {
        SimpleServiceContainer container = new SimpleServiceContainer();

        Should.Throw<Exception>( () => container.Add( typeof( int ), new ProvidedClass( 5 ), null ) );

        container.Add( typeof( ProvidedClass ), () => { return new EmptyClass(); }, null );
        Should.Throw<Exception>( () => container.GetService<ProvidedClass>( false ) );
    }

    [ExcludeFromCodeCoverage]
    static object JustAFunc() { return null!; }

    [ExcludeFromCodeCoverage]
    static T JustAFunc<T>() where T : class { return null!; }

    [Test]
    public void checking_null_arguments()
    {
        ISimpleServiceContainer container = new SimpleServiceContainer();

        //SimpleServiceContainer.Add( Type serviceType, object serviceInstance, Action<Object> onRemove = null )
        Should.Throw<ArgumentNullException>( () => container.Add( null!, new ProvidedClass( 5 ) ) );
        Should.Throw<ArgumentNullException>( () => container.Add( typeof( ProvidedClass ), (object)null! ) );

        //SimpleServiceContainer.Add( Type serviceType, Func<Object> serviceInstance, Action<Object> onRemove = null )
        Should.Throw<ArgumentNullException>( () => container.Add( null!, JustAFunc ) );
        Should.Throw<ArgumentNullException>( () => container.Add( typeof( ProvidedClass ), (Func<Object>)null! ) );

        Should.Throw<ArgumentNullException>( () => container.AddDisabled( null! ) );
        Should.Throw<ArgumentNullException>( () => container.GetService( null! ) );
        Should.Throw<ArgumentNullException>( () => container.Add<ProvidedClass>( JustAFunc<ProvidedClass>, null! ) );
    }

    [Test]
    public void container_can_be_chained_an_loops_are_detected()
    {
        SimpleServiceContainer firstContainer = new SimpleServiceContainer();
        SimpleServiceContainer secondContainer = new SimpleServiceContainer();
        SimpleServiceContainer thirdContainer = new SimpleServiceContainer();

        Should.Throw<Exception>(() => firstContainer.BaseProvider = firstContainer);

        //firstContainer( secondContainer )
        firstContainer.BaseProvider = secondContainer;
        Should.Throw<Exception>(() => secondContainer.BaseProvider = firstContainer);

        //firstContainer( secondContainer( thirdContainer ) )
        secondContainer.BaseProvider = thirdContainer;
        Should.Throw<Exception>(() => thirdContainer.BaseProvider = firstContainer);
        Should.Throw<Exception>(() => thirdContainer.BaseProvider = secondContainer);

        //firstContainer( thirdContainer ) and secondContainer( thirdContainer ) 
        firstContainer.BaseProvider = thirdContainer;
        Should.Throw<Exception>(() => thirdContainer.BaseProvider = secondContainer);
        Should.Throw<Exception>(() => thirdContainer.BaseProvider = firstContainer);

    }

    /// <summary>
    /// Tests the fact that the ISimpleServiceContainer set as parameter is conform to the way the interface should be used.
    /// </summary>
    /// <param name="container">the ISimpleServiceContainer implementation to test</param>
    public void IServiceContainerConformanceTest( ISimpleServiceContainer container )
    {
        IServiceContainerConformanceTest<object>( container, null!, null! );
    }

    /// <summary>
    /// Tests the fact that the ISimpleServiceContainer set as parameter is conform to the way the interface should be used.
    /// </summary>
    /// <typeparam name="T">the service implemented by the servicecontainer's baseprovider </typeparam>
    /// <param name="container">the ISimpleServiceContainer implementation to test</param>
    /// <param name="baseProviderServiceToTest"></param>
    public void IServiceContainerConformanceTest<T>( ISimpleServiceContainer container,
                                                     ISimpleServiceContainer baseProvider,
                                                     T baseProviderServiceToTest )
        where T : class
    {
        Func<IAddService> creatorFunc = () => new AddServiceImpl();

        IServiceContainerCoAndContravariance( container, creatorFunc );

        IServiceContainerConformanceAddRemove( container, creatorFunc );

        IServiceContainerConformanceAddFailsWhenExisting( container, creatorFunc );

        IServiceContainerConformanceRemoveRecursive( container );

        container.Add<IAddService>( creatorFunc );
        container.Add<ISubstractService>( new SubstractServiceImpl() );

        IAddService service = container.GetService<IAddService>( false );
        service.ShouldNotBeNull();
        service.GetType().ShouldBe( typeof( AddServiceImpl ) );
        service.Add( 1, 1 ).ShouldBe( 2 );

        ISubstractService substractService = container.GetService<ISubstractService>( false );
        substractService.ShouldNotBeNull();
        substractService.GetType().ShouldBe( typeof( SubstractServiceImpl ) );
        substractService.Substract( 1, 1 ).ShouldBe( 0 );

        //clear test
        container.Clear();

        container.GetService<IAddService>( false ).ShouldBeNull();
        container.GetService<ISubstractService>( false ).ShouldBeNull();

        //base provider test
        if( baseProvider != null && baseProviderServiceToTest != null )
        {
            T baseService = container.GetService<T>( false );
            baseService.ShouldNotBeNull( "The baseProvider contains the specified service." );

            container.Remove( typeof( T ) );
            baseService = container.GetService<T>( false );
            baseService.ShouldNotBeNull( "Trying to remove a base service from a child provider does nothing." );

            container.AddDisabled( typeof( T ) );
            container.GetService<T>( false ).ShouldBeNull( "Access to this service is disabled" );

            baseProvider.Remove( typeof( T ) );
            container.GetService<T>( false ).ShouldBeNull( "Access to this service is disabled & The service doesn't exist anymore on the baseProvider" );

            container.Remove( typeof( T ) );
            container.GetService<T>( false ).ShouldBeNull( "The service doesn't exist anymore on the baseProvider" );

            baseProvider.Add( baseProviderServiceToTest );
            container.GetService<T>( false ).ShouldNotBeNull( "Back to the beginning's state, the service is retrieved from the base provider." );
        }
    }

    private static void IServiceContainerConformanceRemoveRecursive( ISimpleServiceContainer container )
    {
        bool removedCall = false;
        container.Add<IAddService>( new AddServiceImpl(), s => { removedCall = true; container.Remove( typeof( IAddService ) ); } );
        container.GetService<IAddService>( false ).ShouldNotBeNull();
        container.Remove( typeof( IAddService ) );
        removedCall.ShouldBeTrue( "OnRemove has been called and can safely remove the service again without stack overflow exception." );
    }

    private static void IServiceContainerConformanceAddFailsWhenExisting( ISimpleServiceContainer container, Func<IAddService> creatorFunc )
    {
        container.Add<IAddService>( new AddServiceImpl() );
        Should.Throw<Exception>( () => container.Add( creatorFunc ) );
        Should.Throw<Exception>( () => container.Add<IAddService>( creatorFunc, s => { } ) );
        Should.Throw<Exception>( () => container.Add( typeof( IAddService ), new AddServiceImpl() ) );
        Should.Throw<Exception>( () => container.Add( typeof( IAddService ), new AddServiceImpl(), s => { } ) );
        Should.Throw<Exception>( () => container.Add<IAddService>( new AddServiceImpl() ) );
        Should.Throw<Exception>( () => container.Add<IAddService>( new AddServiceImpl(), s => { } ) );
        Should.Throw<Exception>( () => container.AddDisabled( typeof( IAddService ) ) );
        container.Remove( typeof( IAddService ) );
    }

    private static void IServiceContainerConformanceAddRemove( ISimpleServiceContainer container, Func<IAddService> creatorFunc )
    {
        container.GetService<IAddService>( false ).ShouldBeNull( "Starting with no IAddService." );

        container.Add( creatorFunc );
        Should.Throw<Exception>( () => container.Add( creatorFunc ), "Adding an already existing service throws an exception." );

        container.GetService<IAddService>( false ).ShouldNotBeNull( "Deferred creation occured." );
        container.Remove( typeof( IAddService ) );
        container.GetService<IAddService>( false ).ShouldBeNull( "Remove works." );

        // Removing an unexisting service is okay.
        container.Remove( typeof( IAddService ) );

        bool removed = false;
        container.Add<IAddService>( creatorFunc, s => removed = true );
        container.Remove( typeof( IAddService ) );
        removed.ShouldBeFalse( "Since the service has never been required, it has not been created, hence, OnRemove action has not been called." );

        container.Add<IAddService>( creatorFunc, s => removed = true );
        container.GetService<IAddService>( false ).ShouldNotBeNull( "Service has been created." );
        container.Remove( typeof( IAddService ) );
        removed.ShouldBeTrue( "This time, OnRemove action has been called." );

        removed = false;
        container.Add<IAddService>( new AddServiceImpl(), s => removed = true );
        container.Remove( typeof( IAddService ) );
        removed.ShouldBeTrue( "Since the service instance has been added explicitely, OnRemove action has been called." );
    }

    private static void IServiceContainerCoAndContravariance( ISimpleServiceContainer container, Func<IAddService> creatorFunc )
    {
        {
            _onRemoveServiceCalled = false;
            container.Add<IAddService>( new AddServiceImpl(), OnRemoveService );
            container.Remove( typeof( IAddService ) );
            _onRemoveServiceCalled.ShouldBeTrue( "OnRemoveService has been called." );

            _onRemoveServiceCalled = false;
            container.Add<IAddService>( new AddServiceImpl(), OnRemoveBaseServiceType );
            container.Remove( typeof( IAddService ) );
            _onRemoveServiceCalled.ShouldBeTrue( "OnRemoveBaseServiceType has been called." );

            _onRemoveServiceCalled = false;
            container.Add<IAddService>( new AddServiceImpl(), OnRemoveServiceObject );
            container.Remove( typeof( IAddService ) );
            _onRemoveServiceCalled.ShouldBeTrue( "OnRemoveServiceObject has been called." );

            //container.Add<IAddService>( new AddServiceImpl(), OnRemoveDerivedServiceType );
            //container.Remove( typeof( IAddService ) );

            //container.Add<IAddService>( new AddServiceImpl(), OnRemoveUnrelatedType );
            //container.Remove( typeof( IAddService ) );
        }
        {
            _onRemoveServiceCalled = false;
            container.Add( creatorFunc, OnRemoveService );
            container.Remove( typeof( IAddService ) );
            _onRemoveServiceCalled.ShouldBeFalse( "Service has never been created." );

            container.Add<IAddService>( creatorFunc, OnRemoveBaseServiceType );
            container.Remove( typeof( IAddService ) );
            _onRemoveServiceCalled.ShouldBeFalse( "Service has never been created." );

            container.Add<IAddService>( creatorFunc, OnRemoveServiceObject );
            container.Remove( typeof( IAddService ) );
            _onRemoveServiceCalled.ShouldBeFalse( "Service has never been created." );
        }
    }

    static bool _onRemoveServiceCalled;
    static void OnRemoveService( IAddService s )
    {
        _onRemoveServiceCalled = true;
    }

    static void OnRemoveServiceObject( object o )
    {
        _onRemoveServiceCalled = true;
    }

    static void OnRemoveBaseServiceType( IAddServiceBase baseType )
    {
        _onRemoveServiceCalled = true;
    }

}
