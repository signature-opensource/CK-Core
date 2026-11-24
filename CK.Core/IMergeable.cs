using System;

namespace CK.Core;


/// <summary>
/// Simple interface to support merging of information from external objects.
/// </summary>
public interface IMergeable
{
    /// <summary>
    /// Attempts to merge this object with the given one.
    /// This method should not raise any exception. Instead, false should be returned. 
    /// If an exception is raised, callers should handle the exception and behaves as if the method returned false.
    /// </summary>
    /// <param name="source">Source object to merge into this one.</param>
    /// <param name="services">Optional services (can be null) that can be injected into the merge process.</param>
    /// <returns>True if the merge succeeded, false if the merge failed or is not possible.</returns>
    bool Merge( object source, IServiceProvider? services = null );
}
