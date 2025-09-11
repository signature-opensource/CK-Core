using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace CK.Core;


/// <summary>
/// A date and time stamp encapsulates a <see cref="TimeUtc"/> (<see cref="DateTime"/> guaranteed to be in Utc) and a <see cref="Uniquifier"/>.
/// </summary>
[TypeConverter( typeof( Converter ) )]
public readonly struct DateTimeStamp : IComparable<DateTimeStamp>, IEquatable<DateTimeStamp>, ICKSimpleBinarySerializable, ISpanFormattable
{
    sealed class Converter : TypeConverter
    {
        public override bool CanConvertFrom( ITypeDescriptorContext? context, Type sourceType )
        {
            return sourceType == typeof( string );
        }

        public override bool CanConvertTo( ITypeDescriptorContext? context, Type? destinationType )
        {
            return destinationType == typeof( string );
        }

        public override object? ConvertFrom( ITypeDescriptorContext? context, CultureInfo? culture, object value )
        {
            if( value is string s && TryParse( s, out var r ) ) return r;
            return default;
        }

        public override object? ConvertTo( ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType )
        {
            return value is DateTimeStamp s ? s.ToString() : string.Empty;
        }
    }

    /// <summary>
    /// Represents the smallest possible value for a DateTimeStamp object.         
    /// </summary>
    static public readonly DateTimeStamp MinValue = new DateTimeStamp( Util.UtcMinValue, 0 );

    /// <summary>
    /// Represents an unknown, default, DateTimeStamp object.
    /// This is available to have a more expressive code than <c>new DateTimeStamp()</c>.
    /// </summary>
    static public readonly DateTimeStamp Unknown = new DateTimeStamp();

    /// <summary>
    /// Represents the largest possible value for a DateTimeStamp object.         
    /// </summary>
    static public readonly DateTimeStamp MaxValue = new DateTimeStamp( Util.UtcMaxValue, Byte.MaxValue );

    /// <summary>
    /// Represents an invalid DateTimeStamp object. See <see cref="IsInvalid"/>.
    /// </summary>
    static public readonly DateTimeStamp Invalid = new DateTimeStamp( 0 );

    /// <summary>
    /// DateTime in Utc.
    /// </summary>
    public readonly DateTime TimeUtc;

    /// <summary>
    /// Uniquifier: non zero when <see cref="TimeUtc"/> collides.
    /// </summary>
    public readonly byte Uniquifier;

    DateTimeStamp( int justForInvalidOne )
    {
        TimeUtc = new DateTime( DateTime.MinValue.Ticks, DateTimeKind.Local );
        Uniquifier = 0;
    }

    /// <summary>
    /// Deserialization constructor.
    /// </summary>
    /// <param name="r">The reader.</param>
    public DateTimeStamp( ICKBinaryReader r )
    {
        TimeUtc = r.ReadDateTime();
        Uniquifier = r.ReadByte();
    }

    /// <summary>
    /// Writes this DateTimeStamp.
    /// </summary>
    /// <param name="w">The writer.</param>
    public void Write( ICKBinaryWriter w )
    {
        w.Write( TimeUtc );
        w.Write( Uniquifier );
    }

    /// <summary>
    /// Initializes a new <see cref="DateTimeStamp"/>.
    /// </summary>
    /// <param name="timeUtc">The log time. <see cref="DateTime.Kind"/> must be <see cref="DateTimeKind.Utc"/>.</param>
    /// <param name="uniquifier">Optional non zero uniquifier.</param>
    public DateTimeStamp( DateTime timeUtc, byte uniquifier = 0 )
    {
        Throw.CheckArgument( timeUtc.Kind == DateTimeKind.Utc );
        TimeUtc = timeUtc;
        Uniquifier = uniquifier;
    }

    /// <summary>
    /// Initializes a new <see cref="DateTimeStamp"/> that is that is guaranteed to be unique and ascending (unless <paramref name="ensureGreaterThanLastOne"/> 
    /// is false) regarding <paramref name="lastOne"/>.
    /// </summary>
    /// <param name="lastOne">Last time stamp.</param>
    /// <param name="time">Time (generally current <see cref="DateTime.UtcNow"/>).</param>
    /// <param name="ensureGreaterThanLastOne">False to only check for time equality collision instead of guarantying ascending time.</param>
    public DateTimeStamp( DateTimeStamp lastOne, DateTime time, bool ensureGreaterThanLastOne = true )
    {
        Throw.CheckArgument( time.Kind == DateTimeKind.Utc );
        if( ensureGreaterThanLastOne ? time <= lastOne.TimeUtc : time != lastOne.TimeUtc )
        {
            if( lastOne.Uniquifier == Byte.MaxValue )
            {
                TimeUtc = new DateTime( lastOne.TimeUtc.Ticks + 1, DateTimeKind.Utc );
                Uniquifier = 1;
            }
            else
            {
                TimeUtc = lastOne.TimeUtc;
                Uniquifier = (Byte)(lastOne.Uniquifier + 1);
            }
        }
        else
        {
            TimeUtc = time;
            Uniquifier = 0;
        }
    }

    /// <summary>
    /// Initializes a new <see cref="DateTimeStamp"/> that is that is guaranteed to be unique and ascending regarding <paramref name="lastOne"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="Uniquifier"/> is optimized if possible (this simply calls <see cref="DateTimeStamp(DateTimeStamp,DateTime,bool)"/> with ensureGreaterThanLastOne sets to true).
    /// </remarks>
    /// <param name="lastOne">Last time stamp.</param>
    /// <param name="newTime">DateTimeStamp to combine.</param>
    public DateTimeStamp( DateTimeStamp lastOne, DateTimeStamp newTime )
        : this( lastOne, newTime.TimeUtc, ensureGreaterThanLastOne: true )
    {
    }

    /// <summary>
    /// Gets whether this <see cref="DateTimeStamp"/> is initialized.
    /// The default constructor of a structure can not be defined and it initializes the <see cref="TimeUtc"/> with a zero that is <see cref="DateTime.MinValue"/>
    /// with a <see cref="DateTime.Kind"/> set to <see cref="DateTimeKind.Unspecified"/>.
    /// </summary>
    public bool IsKnown
    {
        get { return TimeUtc.Kind == DateTimeKind.Utc; }
    }

    /// <summary>
    /// Gets whether this <see cref="DateTimeStamp"/> is the <see cref="Invalid"/> one.
    /// <see cref="TimeUtc"/> has a <see cref="DateTime.Kind"/> set to <see cref="DateTimeKind.Local"/>.
    /// </summary>
    public bool IsInvalid
    {
        get { return TimeUtc.Kind == DateTimeKind.Local; }
    }

    /// <summary>
    /// Gets the current <see cref="DateTime.UtcNow"/> as a DateTimeStamp.
    /// </summary>
    public static DateTimeStamp UtcNow
    {
        get { return new DateTimeStamp( DateTime.UtcNow ); }
    }

    /// <summary>
    /// Compares this <see cref="DateTimeStamp"/> to another one.
    /// </summary>
    /// <param name="other">The other DateTimeStamp to compare.</param>
    /// <returns>Positive value when this is greater than other, 0 when they are equal, a negative value otherwise.</returns>
    public int CompareTo( DateTimeStamp other )
    {
        int cmp = TimeUtc.CompareTo( other.TimeUtc );
        if( cmp == 0 ) cmp = Uniquifier.CompareTo( other.Uniquifier );
        return cmp;
    }

    /// <summary>
    /// Checks equality.
    /// </summary>
    /// <param name="other">Other DateTimeStamp.</param>
    /// <returns>True when this is equal to other.</returns>
    public bool Equals( DateTimeStamp other )
    {
        return TimeUtc == other.TimeUtc && Uniquifier == other.Uniquifier;
    }

    /// <summary>
    /// Compares this DateTimeStamp to another object that must also be a stamp.
    /// </summary>
    /// <param name="value">The object to compare.</param>
    /// <returns>Positive value when this is greater than other, 0 when they are equal, a negative value otherwise.</returns>
    public int CompareTo( object value )
    {
        if( value == null ) return 1;
        return CompareTo( (DateTimeStamp)value );
    }

    /// <summary>
    /// Overridden to check equality.
    /// </summary>
    /// <param name="other">Other object.</param>
    /// <returns>True when this is equal to other.</returns>
    public override bool Equals( object? other ) => other is DateTimeStamp o && Equals( o );

    /// <summary>
    /// Overridden to match <see cref="Equals(DateTimeStamp)"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => TimeUtc.GetHashCode() ^ Uniquifier;

    /// <summary>
    /// @"{0:yyyy-MM-dd HH\hmm.ss.fffffff}({1})" is the format that will be used to format log time when the <see cref="Uniquifier"/> is not zero.
    /// It is based on <see cref="FileUtil.FileNameUniqueTimeUtcFormat"/> (that is used as-is when the Uniquifier is zero) for the date time format.
    /// </summary>
    public static readonly string FormatWhenUniquifier = "{0:" + FileUtil.FileNameUniqueTimeUtcFormat + "}({1})";

    /// <summary>
    /// Overridden to return a string based on <see cref="FormatWhenUniquifier"/> or <see cref="FileUtil.FileNameUniqueTimeUtcFormat"/>.
    /// <para>
    /// This string contains between 27 and 32 characters.
    /// </para>
    /// </summary>
    /// <returns>A string that can be successfully matched.</returns>
    public override string ToString()
    {
        var u = Uniquifier;
        int len = 27;
        if( u != 0 ) len += 3 + (u >= 100 ? 2 : u < 10 ? 0 : 1);
        return String.Create( len, this, static ( s, t ) => t.TryFormat( s, out _ ) );
    }

    /// <summary>
    /// Tries to format this DatetimeStamp into the provided span of characters.
    /// The destination must be at least between 27 and 32 long.
    /// </summary>
    /// <param name="destination">
    /// When this method returns, this instance's value formatted as a span of characters.
    /// </param>
    /// <param name="charsWritten">When this method returns, the number of characters that were written in destination.</param>
    /// <param name="format">Ignored: no custom format exists.</param>
    /// <param name="provider">Ignored: the format is culture invariant.</param>
    /// <returns>True if the formatting was successful; otherwise, False.</returns>
    public bool TryFormat( Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null )
    {
        Debug.Assert( FileUtil.FileNameUniqueTimeUtcFormat.Replace( "\\", "" ).Length == 27 );
        if( Uniquifier != 0 )
        {
            int len = 27 + 3 + (Uniquifier >= 100 ? 2 : Uniquifier < 10 ? 0 : 1);
            if( destination.Length < len )
            {
                charsWritten = 0;
                return false;
            }
            TimeUtc.TryFormat( destination, out charsWritten, FileUtil.FileNameUniqueTimeUtcFormat.AsSpan(), CultureInfo.InvariantCulture );
            destination = destination.Slice( charsWritten );
            destination[0] = '(';
            destination = destination.Slice( 1 );
            Uniquifier.TryFormat( destination, out charsWritten, ReadOnlySpan<char>.Empty, null );
            destination[charsWritten] = ')';
            charsWritten = len;
            return true;
        }
        if( destination.Length < 27 )
        {
            charsWritten = 0;
            return false;
        }
        return TimeUtc.TryFormat( destination, out charsWritten, FileUtil.FileNameUniqueTimeUtcFormat.AsSpan(), CultureInfo.InvariantCulture );
    }

    string IFormattable.ToString( string? format, IFormatProvider? formatProvider ) => ToString();

    /// <summary>
    /// Tries to match a <see cref="DateTimeStamp"/> and forwards the <paramref name="head"/> on success.
    /// </summary>
    /// <param name="head">This parsing head.</param>
    /// <param name="time">Resulting time stamp on successful match; <see cref="DateTimeStamp.Unknown"/> otherwise.</param>
    /// <returns>True on success, false otherwise.</returns>
    static public bool TryMatch( ref ReadOnlySpan<char> head, out DateTimeStamp time ) => TryMatch( ref head, out time, false );

    /// <summary>
    /// Tries to parse a <see cref="DateTimeStamp"/>.
    /// <para>
    /// The extension method <see cref="DateTimeStampExtension.TryMatchDateTimeStamp(ref ROSpanCharMatcher, out DateTimeStamp)"/>
    /// is also available.
    /// </para>
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="time">Resulting time stamp on successful match; <see cref="DateTimeStamp.Unknown"/> otherwise.</param>
    /// <returns>True on success, false otherwise.</returns>
    static public bool TryParse( ReadOnlySpan<char> s, out DateTimeStamp time ) => TryMatch( ref s, out time, true );

    static bool TryMatch( ref ReadOnlySpan<char> head, out DateTimeStamp time, bool parse )
    {
        var savedHead = head;
        if( !head.TryMatchFileNameUniqueTimeUtcFormat( out var t ) ) goto error;
        byte uniquifier = 0;
        if( head.TryMatch( '(' ) )
        {
            if( !head.TryMatchInteger( out uniquifier ) || !head.TryMatch( ')' ) ) goto error;
        }
        if( !parse || head.IsEmpty )
        {
            time = new DateTimeStamp( t, uniquifier );
            return true;
        }
        error:
        time = Unknown;
        head = savedHead;
        return false;
    }

    /// <summary>
    /// Parses a <see cref="DateTimeStamp"/> or throws a <see cref="FormatException"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>The DateTimeStamp.</returns>
    static public DateTimeStamp Parse( ReadOnlySpan<char> s )
    {
        if( !TryParse( s, out var time ) ) Throw.FormatException( $"Invalid DateTimeStamp: '{s}'." );
        return time;
    }

    /// <summary>
    /// Checks equality.
    /// </summary>
    /// <param name="t1">First stamp.</param>
    /// <param name="t2">Second stamp.</param>
    /// <returns>True when stamps are equals.</returns>
    public static bool operator ==( DateTimeStamp t1, DateTimeStamp t2 )
    {
        return t1.Equals( t2 );
    }

    /// <summary>
    /// Checks inequality.
    /// </summary>
    /// <param name="t1">First stamp.</param>
    /// <param name="t2">Second stamp.</param>
    /// <returns>True when stamps are different.</returns>
    public static bool operator !=( DateTimeStamp t1, DateTimeStamp t2 )
    {
        return !t1.Equals( t2 );
    }

    /// <summary>
    /// Strict greater than operator.
    /// </summary>
    /// <param name="t1">First stamp.</param>
    /// <param name="t2">Second stamp.</param>
    /// <returns>True when stamps first is greater than second.</returns>
    public static bool operator >( DateTimeStamp t1, DateTimeStamp t2 )
    {
        return t1.CompareTo( t2 ) > 0;
    }

    /// <summary>
    /// Large greater than operator.
    /// </summary>
    /// <param name="t1">First stamp.</param>
    /// <param name="t2">Second stamp.</param>
    /// <returns>True when stamps first is greater than or equal to second.</returns>
    public static bool operator >=( DateTimeStamp t1, DateTimeStamp t2 )
    {
        return t1.CompareTo( t2 ) >= 0;
    }

    /// <summary>
    /// Strict lower than operator.
    /// </summary>
    /// <param name="t1">First stamp.</param>
    /// <param name="t2">Second stamp.</param>
    /// <returns>True when stamps first is lower than second.</returns>
    public static bool operator <( DateTimeStamp t1, DateTimeStamp t2 )
    {
        return t1.CompareTo( t2 ) < 0;
    }

    /// <summary>
    /// Large lower than operator.
    /// </summary>
    /// <param name="t1">First stamp.</param>
    /// <param name="t2">Second stamp.</param>
    /// <returns>True when stamps first is lower than or equal to second.</returns>
    public static bool operator <=( DateTimeStamp t1, DateTimeStamp t2 )
    {
        return t1.CompareTo( t2 ) <= 0;
    }
}
