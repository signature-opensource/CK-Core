namespace CK.Core;

/// <summary>
/// Exposes extension methods on <see cref="DateTimeStamp"/>.
/// </summary>
public static class DateTimeStampExtension
{
    /// <summary>
    /// Tries to match a <see cref="DateTimeStamp"/>.
    /// </summary>
    /// <param name="m">This matcher.</param>
    /// <param name="time">Resulting time stamp on successful match; <see cref="DateTimeStamp.Unknown"/> otherwise.</param>
    /// <returns>True on success, false otherwise.</returns>
    static public bool TryMatchDateTimeStamp( this ref ROSpanCharMatcher m, out DateTimeStamp time )
    {
        var savedHead = m.Head;
        using( m.OpenExpectations( "DateTimeStamp" ) )
        {
            if( !m.TryMatchFileNameUniqueTimeUtcFormat( out var t ) ) goto error;
            byte uniquifier = 0;
            if( m.Head.TryMatch( '(' ) )
            {
                if( !m.TryMatchInteger( out uniquifier ) || !m.TryMatch( ')' ) ) goto error;
            }
            time = new DateTimeStamp( t, uniquifier );
            return m.SetSuccess();

            error:
            time = DateTimeStamp.Unknown;
            m.Head = savedHead;
            return false;
        }
    }
}
