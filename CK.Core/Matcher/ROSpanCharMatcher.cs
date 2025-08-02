using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Core;

/// <summary>
/// Low level implementation of the "Match and Forward" pattern with a ref struct that exposes a mutable ReadOnly&lt;char&gt; <see cref="Head"/>
/// and handles "expectations" that are potential errors as a tree-like structure.
/// </summary>
public ref partial struct ROSpanCharMatcher
{
    /// <summary>
    /// The mutable current head.
    /// </summary>
    public ReadOnlySpan<char> Head;

    /// <summary>
    /// The immutable whole initial text.
    /// </summary>
    public readonly ReadOnlySpan<char> AllText;

    interface ITracker
    {
        int ErrorCount { get; }
        bool AddExpOrErr( bool isError, int pos, string expect, string caller );
        bool ClearExpectations();
        bool SingleExpectationMode { get; set; }
        IDisposable OpenSubTracker( int pos, string? scopedExpectation, string callerName );
        IEnumerable<(int Pos, int Depth, bool IsError, string Expectation, string CallerName)> GetErrors( int allTextLength );
    }

    sealed class ErrorTracker : ITracker
    {
        ITracker _current;
        // O or positive Depth is an expectation.
        // Negative Depth is for errors and are unary complement of the actual Depth.
        // This avoids yet another (boolean) field to discriminate between errors and expectations.
        (int P, int D, string? E, string C)[]? _errors;
        Sub? _firstFree;
        int _errorCount;
        bool _singleExpectation;

        sealed class Sub : ITracker, IDisposable
        {
            readonly ErrorTracker _tracker;
            int _depth;
            int _idxHeader;
            ITracker _prev;
            internal Sub? _parentOrNextFree;
            bool _singleExpectation;
            bool _clearCalled;
            bool _parentSingleExpectation;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            public Sub( ErrorTracker t )
            {
                _tracker = t;
            }
#pragma warning restore CS8618 

            internal Sub Initialize( int pos, int depth, bool singleExpectation, string? header, string callerName, Sub? parent )
            {
                _tracker.AddExpectation( pos, depth++, false, header ?? callerName, callerName );
                _idxHeader = _tracker._errorCount;
                _depth = depth;
                _prev = _tracker._current;
                _tracker._current = this;
                _singleExpectation = _parentSingleExpectation = singleExpectation;
                _parentOrNextFree = parent;
                _clearCalled = false;
                return this;
            }

            public int ErrorCount => _tracker._errorCount - _idxHeader;

            public bool AddExpOrErr( bool isError, int pos, string expect, string caller )
            {
                _clearCalled = false;
                return _singleExpectation
                        ? _tracker.SetSingleExpectation( _idxHeader, pos, _depth, isError, expect, caller )
                        : _tracker.AddExpectation( pos, _depth, isError, expect, caller );
            }

            public bool ClearExpectations()
            {
                _clearCalled = true;
                return _tracker.ClearExpectations( _idxHeader );
            }

            public bool SingleExpectationMode
            {
                get => _singleExpectation;
                set
                {
                    if( _singleExpectation != value && (_singleExpectation = value) ) _tracker.LiftLastError( _idxHeader );
                }
            }

            public IDisposable OpenSubTracker( int pos, string? scopedExpectation, string callerName )
            {
                return _tracker.AcquireSub().Initialize( pos, _depth, _singleExpectation, scopedExpectation, callerName, this );
            }

            public void Dispose()
            {
                if( _tracker._current != this ) Throw.InvalidOperationException( "OpenExpectations dispose mismatch." );
                if( ErrorCount > 0 )
                {
                    if( _parentSingleExpectation )
                    {
                        _tracker.ClearExpectations( _idxHeader );
                        _tracker.LiftLastError( _parentOrNextFree?._idxHeader ?? 0 );
                    }
                }
                else
                {
                    if( _clearCalled ) _tracker.ClearExpectations( _idxHeader - 1 );
                }
                _tracker._current = _prev;
                _tracker.ReleaseSub( this );
            }

            public IEnumerable<(int Pos, int Depth, bool IsError, string Expectation, string CallerName)> GetErrors( int allTextLength )
            {
                if( ErrorCount == 0 ) return Enumerable.Empty<(int, int, bool, string, string)>();
                Debug.Assert( _tracker._errors != null );
                return _tracker._errors.Skip( _idxHeader ).Take( _tracker._errorCount - _idxHeader ).Select( e => (allTextLength - e.P, e.D < 0 ? ~e.D : e.D, e.D < 0, e.E!, e.C) );
            }
        }

        Sub AcquireSub()
        {
            if( _firstFree == null ) return new Sub( this );
            var s = _firstFree;
            _firstFree = s._parentOrNextFree;
            return s;
        }

        void ReleaseSub( Sub s )
        {
            s._parentOrNextFree = _firstFree;
            _firstFree = s;
        }

        public ErrorTracker()
        {
            _current = this;
        }

        public int ErrorCount => _current.ErrorCount;

        int ITracker.ErrorCount => _errorCount;

        public bool AddExpOrErr( bool isError, int pos, string expect, string caller ) => _current.AddExpOrErr( isError, pos, expect, caller );

        bool ITracker.AddExpOrErr( bool isError, int pos, string expect, string caller ) => _singleExpectation
                                                                                    ? SetSingleExpectation( 0, pos, 0, isError, expect, caller )
                                                                                    : AddExpectation( pos, 0, isError, expect, caller );

        bool AddExpectation( int pos, int depth, bool isError, string expect, string caller )
        {
            if( _errors == null ) _errors = new (int, int, string?, string)[16];
            else if( _errorCount == _errors.Length )
            {
                Array.Resize( ref _errors, _errorCount * 2 );
            }
            _errors[_errorCount++] = (pos, isError ? ~depth : depth, expect, caller);
            return false;
        }

        bool SetSingleExpectation( int startPos, int pos, int depth, bool isError, string expect, string caller )
        {
            Debug.Assert( startPos == _errorCount - 1 || startPos == _errorCount );
            if( startPos != _errorCount )
            {
                Debug.Assert( _errors != null );
                _errors[startPos] = (pos, isError ? ~depth : depth, expect, caller);
                return false;
            }
            return AddExpectation( pos, depth, isError, expect, caller );
        }

        public bool ClearExpectations() => _current.ClearExpectations();

        bool ITracker.ClearExpectations() => ClearExpectations( 0 );

        bool ClearExpectations( int errorCount )
        {
            _errorCount = errorCount;
            return true;
        }

        public bool SingleExpectationMode
        {
            get => _current.SingleExpectationMode;
            set => _current.SingleExpectationMode = value;
        }

        bool ITracker.SingleExpectationMode
        {
            get => _singleExpectation;
            set
            {
                if( _singleExpectation != value && (_singleExpectation = value) )
                {
                    LiftLastError( 0 );
                }
            }
        }

        public void LiftLastError( int idxError )
        {
            int c = idxError + 1;
            if( _errorCount > c )
            {
                Debug.Assert( _errors != null );
                _errors[idxError] = _errors[_errorCount - 1];
                _errorCount = c;
            }
        }

        public IDisposable OpenSubTracker( int pos, string? scopedExpectation, string callerName ) => _current.OpenSubTracker( pos, scopedExpectation, callerName );

        IDisposable ITracker.OpenSubTracker( int pos, string? scopedExpectation, string callerName )
        {
            return AcquireSub().Initialize( pos, 0, _singleExpectation, scopedExpectation, callerName, null );
        }

        public IEnumerable<(int Pos, int Depth, bool IsError, string Expectation, string CallerName)> GetErrors( int allTextLength ) => _current.GetErrors( allTextLength );

        IEnumerable<(int Pos, int Depth, bool IsError, string Expectation, string CallerName)> ITracker.GetErrors( int allTextLength )
        {
            if( _errorCount == 0 ) return Enumerable.Empty<(int, int, bool, string, string)>();
            Debug.Assert( _errors != null );
            return _errors.Take( _errorCount ).Select( e => (allTextLength - e.P, e.D < 0 ? ~e.D : e.D, e.D < 0, e.E!, e.C) );
        }
    }

    readonly ErrorTracker _tracker;

    /// <summary>
    /// Initializes a new <see cref="ROSpanCharMatcher"/>.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    public ROSpanCharMatcher( ReadOnlySpan<char> text )
    {
        Head = text;
        AllText = text;
        _tracker = new ErrorTracker();
    }

    /// <summary>
    /// Gets whether this matcher has unsatisfied expectations.
    /// </summary>
    public readonly bool HasError => _tracker.ErrorCount > 0;

    /// <summary>
    /// Gets or sets whether only one expectation or error must be kept.
    /// </summary>
    public readonly bool SingleExpectationMode
    {
        get => _tracker.SingleExpectationMode;
        set => _tracker.SingleExpectationMode = value;
    }

    /// <summary>
    /// Adds an expectation. <paramref name="expect"/>> must be written without "expect" word, only with the
    /// description of what was expected: "Json string", "Date", "Numbered item (starting at 2)" without trailing dot.
    /// </summary>
    /// <param name="expect">The expected pattern description without trailing dot.</param>
    /// <param name="callerName">Method name of the caller (automatically set by the compiler).</param>
    /// <returns>Always false so it can be directly returned by the TryMatch function.</returns>
    /// <remarks>
    /// We use the CallerName here because it cannot be the awful mangled name of a code generated lambda since ReadOnlySpan
    /// as a ref struct cannot be used in a lambda (except in a <see cref="System.Buffers.ReadOnlySpanAction{T, TArg}"/> that
    /// has few chances to used here).
    /// </remarks>
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public readonly bool AddExpectation( string expect, [CallerMemberName] string? callerName = null ) => _tracker.AddExpOrErr( false, Head.Length, expect, callerName! );

    /// <summary>
    /// Adds an error. <paramref name="error"/>> must be written with trailing dot (just like usual error or log messages).
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="callerName">Method name of the caller (automatically set by the compiler).</param>
    /// <returns>Always false so it can be directly returned by the TryMatch function.</returns>
    /// <remarks>
    /// We use the CallerName here because it cannot be the awful mangled name of a code generated lambda since ReadOnlySpan
    /// as a ref struct cannot be used in a lambda (except in a <see cref="System.Buffers.ReadOnlySpanAction{T, TArg}"/> that
    /// has few chances to used here).
    /// </remarks>
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public readonly bool AddError( string error, [CallerMemberName] string? callerName = null ) => _tracker.AddExpOrErr( true, Head.Length, error, callerName! );

    /// <summary>
    /// Adds an expectation. <paramref name="expect"/>> must be written without "expect" word, only with the
    /// description of what was expected: "Json string", "Date", "Numbered item (starting at 2)" without trailing dot.
    /// </summary>
    /// <param name="offset">Offset of the expectation relative to the <see cref="Head"/>.</param>
    /// <param name="expect">The expected pattern description without trailing dot.</param>
    /// <param name="callerName">Method name of the caller (automatically set by the compiler).</param>
    /// <returns>Always false so it can be directly returned by the TryMatch function.</returns>
    /// <remarks>
    /// We use the CallerName here because it cannot be the awful mangled name of a code generated lambda since ReadOnlySpan
    /// as a ref struct cannot be used in a lambda (except in a <see cref="System.Buffers.ReadOnlySpanAction{T, TArg}"/>
    /// or other explicit delegate signatures that has few chances to used here).
    /// </remarks>
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public readonly bool AddExpectation( int offset, string expect, [CallerMemberName] string? callerName = null ) => _tracker.AddExpOrErr( false, Head.Length - offset, expect, callerName! );

    /// <summary>
    /// Adds an error. <paramref name="error"/>> must be written with trailing dot (just like usual error or log messages).
    /// </summary>
    /// <param name="offset">Offset of the error relative to the <see cref="Head"/>.</param>
    /// <param name="error">The error message.</param>
    /// <param name="callerName">Method name of the caller (automatically set by the compiler).</param>
    /// <returns>Always false so it can be directly returned by the TryMatch function.</returns>
    /// <remarks>
    /// We use the CallerName here because it cannot be the awful mangled name of a code generated lambda since ReadOnlySpan
    /// as a ref struct cannot be used in a lambda (except in a <see cref="System.Buffers.ReadOnlySpanAction{T, TArg}"/> that
    /// has few chances to used here).
    /// </remarks>
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public readonly bool AddError( int offset, string error, [CallerMemberName] string? callerName = null ) => _tracker.AddExpOrErr( true, Head.Length - offset, error, callerName! );

    /// <summary>
    /// Clears any recorded expectations and always returns true.
    /// </summary>
    /// <returns>Always true so it can be directly returned by the TryMatch function.</returns>
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public readonly bool SetSuccess() => _tracker.ClearExpectations();

    /// <summary>
    /// Opens a group of subordinated expectations that must be disposed.
    /// <para>
    /// Because of ref struct limitation this is not possible for this disposable to capture the current head and automatically
    /// restores it if <see cref="SetSuccess"/> has not been called. This must be done explicitly. A typical pattern is:
    /// <code>
    /// var savedHead = matcher.Head;
    /// using( OpenExpectations( "Thing" ) )
    /// {
    ///   if( !matcher.TryMatchXXX( ... ) ) goto error:
    ///   if( !matcher.TryMatchXXX( ... ) ) goto error:
    ///
    ///   return matcher.SetSuccess();
    ///
    ///   error:
    ///   matcher.Head = savedHead;
    ///   return false;
    /// }
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="expectHeader">Optional header for the subordinated expectations.</param>
    /// <param name="callerName">Method name of the caller (automatically set by the compiler).</param>
    /// <returns>The disposable to close the group.</returns>
    public readonly IDisposable OpenExpectations( string? expectHeader = null, [CallerMemberName] string? callerName = null ) => _tracker.OpenSubTracker( Head.Length, expectHeader, callerName! );

    /// <summary>
    /// Gets the list of expectations if any: the expected position, the expected expression and name of the
    /// method that failed, and its depth in the parsing.
    /// </summary>
    /// <returns>The set of expectations.</returns>
    public readonly IEnumerable<(int Pos, int Depth, bool IsError, string Expectation, string CallerName)> GetRawErrors() => _tracker.GetErrors( AllText.Length );

    ref struct LineColumnFinder
    {
        readonly ReadOnlySpan<char> _allText;
        SpanLineEnumerator _lines;
        int _linePos;
        int _nextLinePos;
        int _currentPos;
        int _currentLine;
        int _currentCol;

        public LineColumnFinder( ReadOnlySpan<char> text )
        {
            _allText = text;
            _lines = text.EnumerateLines();
            _linePos = 0;
            _nextLinePos = 0;
            _currentPos = 0;
            _currentLine = 0;
            _currentCol = 1;
            NextLine();
        }

        void NextLine()
        {
            bool f = _lines.MoveNext();
            Debug.Assert( f, "Never called on the last line." );
            _currentLine++;
            _linePos = _nextLinePos;
            _nextLinePos += _lines.Current.Length + 1;
            if( _nextLinePos < _allText.Length && _allText[_nextLinePos] == '\n' ) ++_nextLinePos;
        }

        public (int, int) Get( int nextPos )
        {
            Debug.Assert( nextPos <= _allText.Length );
            if( nextPos < _currentPos )
            {
                _lines = _allText.EnumerateLines();
                _linePos = 0;
                _nextLinePos = 0;
                _currentPos = 0;
                _currentLine = 0;
                _currentCol = 1;
                NextLine();
            }
            if( nextPos == _currentPos ) return (_currentLine, _currentCol);
            while( nextPos >= _nextLinePos ) NextLine();
            _currentPos = nextPos;
            _currentCol = nextPos - _linePos + 1;
            return (_currentLine, _currentCol);
        }

    }

    /// <summary>
    /// Gets the errors with each line and column.
    /// </summary>
    /// <param name="maxDepth">Optional depth restriction.</param>
    /// <returns>The set of errors.</returns>
    public readonly Memory<(int Pos, int Line, int Col, int Depth, bool IsError, string Expectation, string CallerName)> GetErrors( int maxDepth = 0 )
    {
        int count = _tracker.ErrorCount;
        if( count == 0 ) return Memory<(int, int, int, int, bool, string, string)>.Empty;

        var all = new (int Pos, int Line, int Col, int Depth, bool IsError, string Expectation, string CallerName)[count];
        var lc = new LineColumnFinder( AllText );
        int i = 0;
        foreach( var (pos, depth, isError, expectation, callerName) in GetRawErrors() )
        {
            if( maxDepth == 0 || depth <= maxDepth )
            {
                var (l, c) = lc.Get( pos );
                all[i++] = (pos, l, c, depth, isError, expectation, callerName);
            }
        }
        return all.AsMemory( 0, i );
    }

    /// <summary>
    /// Forwards <see cref="Head"/> by <paramref name="length"/> even if actual head's length is shorter and
    /// returns the count of remaining characters (the new head's length).
    /// </summary>
    /// <param name="length">The length. Must be 0 or positive otherwise an ArgumentOutOfRangeException is thrown.</param>
    /// <returns>The remainder's head length.</returns>
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public int SafeForward( int length ) => Head.SafeForward( length );

    /// <summary>
    /// Tries to match a specific string.
    /// </summary>
    /// <param name="value">The string value to match.</param>
    /// <param name="comparison">How to compare.</param>
    /// <returns>True on success, false otherwise.</returns>
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public bool TryMatch( ReadOnlySpan<char> value, StringComparison comparison = StringComparison.Ordinal )
        => Head.TryMatch( value, comparison ) ? SetSuccess() : AddExpectation( $"String '{value}'" );

    /// <summary>
    /// Tries to match a character.
    /// </summary>
    /// <param name="value">The character to match.</param>
    /// <param name="comparison">How to compare.</param>
    /// <returns>True on success</returns>
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public bool TryMatch( char value, StringComparison comparison = StringComparison.Ordinal )
        => Head.TryMatch( value, comparison )
            ? SetSuccess()
            : AddExpectation( $"Character '{value}'" );

    /// <summary>
    /// Tries to parse a Guid.
    /// </summary>
    /// <remarks>
    /// Any of the 5 forms of Guid can be matched:
    /// <list type="table">
    /// <item><term>N</term><description>00000000000000000000000000000000</description></item>
    /// <item><term>D</term><description>00000000-0000-0000-0000-000000000000</description></item>
    /// <item><term>B</term><description>{00000000-0000-0000-0000-000000000000}</description></item>
    /// <item><term>P</term><description>(00000000-0000-0000-0000-000000000000)</description></item>
    /// <item><term>X</term><description>{0x00000000,0x0000,0x0000,{0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00}}</description></item>
    /// </list>
    /// </remarks>
    /// <param name="id">The result Guid. <see cref="Guid.Empty"/> on failure.</param>
    /// <returns>True on success, false otherwise.</returns>
    public bool TryMatchGuid( out Guid id )
        => Head.TryMatchGuid( out id )
            ? SetSuccess()
            : AddExpectation( "Guid" );

    /// <summary>
    /// Tries to skip a sequence of white spaces.
    /// Use <paramref name="minCount"/> = 0 to skip any number of white spaces and always return true.
    /// </summary>
    /// <param name="minCount">Minimal number of white spaces to skip.</param>
    /// <returns>True on success, false if <paramref name="minCount"/> white spaces cannot be skipped before the end of the head).</returns>
    public bool TrySkipWhiteSpaces( int minCount = 1 )
        => Head.TrySkipWhiteSpaces( minCount )
            ? SetSuccess()
            : AddExpectation( minCount == 1 ? "At least one white space" : $"At least {minCount} white space(s)" );

    /// <summary>
    /// Skips any number of white spaces and always returns true.
    /// </summary>
    /// <returns>Always true.</returns>
    public bool SkipWhiteSpaces() => Head.SkipWhiteSpaces();

    /// <summary>
    /// Tries to match an Int32 value. A signed integer starts with a '-' and must not be followed by white spaces.
    /// If the value is too big for an Int32, it fails.
    /// <para>
    /// When <paramref name="allowLeadingZeros"/> is false, the value must not start with '0' ('0' is valid but '0d', where d is any digit, is not)
    /// and '-0' is valid but '-0d' (where d is any digit) is not.
    /// </para>
    /// </summary>
    /// <param name="i">The result integer. 0 on failure.</param>
    /// <param name="minValue">Optional minimal value.</param>
    /// <param name="maxValue">Optional maximal value.</param>
    /// <param name="allowLeadingZeros">True to allow leading zeros.</param>
    /// <returns>True on success, false otherwise.</returns>
    public bool TryMatchInt32( out int i, int minValue = int.MinValue, int maxValue = int.MaxValue, bool allowLeadingZeros = false )
    {
        if( Head.TryMatchInt32( out i, minValue, maxValue, allowLeadingZeros ) )
        {
            return SetSuccess();
        }
        string s;
        if( minValue >= 0 )
        {
            s = $"Integer between {minValue} and {maxValue}";
        }
        else
        {
            s = $"Signed integer between {minValue} and {maxValue}";
        }
        if( !allowLeadingZeros ) s += " (without leading zeros)";
        return AddExpectation( s );
    }

    /// <summary>
    /// Tries to skip a double value. This skips a pattern like the regular expression "^-?[0-9]+(\.[0-9]+)?((e|E)(\+|-)?[0-9]+)?".
    /// </summary>
    /// <returns>True on success, false otherwise.</returns>
    public bool TrySkipDouble()
        => Head.TrySkipDouble()
            ? SetSuccess()
            : AddExpectation( $"Floating number" );

    /// <summary>
    /// Tries to match a double value. See <see cref="ReadOnlySpanCharExtensions.TryMatchDouble(ref ReadOnlySpan{char}, out double)"/>.
    /// </summary>
    /// <param name="value">The result double. 0.0 on failure.</param>
    /// <returns></returns>
    public bool TryMatchDouble( out double value )
        => Head.TryMatchDouble( out value )
            ? SetSuccess()
            : AddExpectation( "Floating number" );

    /// <summary>
    /// Tries to parse an hexadecimal values of 1 to 16 '0'-'9', 'A'-'F' or 'a'-'f' digits.
    /// </summary>
    /// <param name="value">Resulting value on success.</param>
    /// <param name="minDigit">Minimal digit count. Must be between 1 and 16 and smaller or equal to <paramref name="maxDigit"/>.</param>
    /// <param name="maxDigit">Maximal digit count. Must be between 1 and 16.</param>
    /// <returns>True on success, false otherwise.</returns>
    public bool TryMatchHexNumber( out ulong value, int minDigit = 1, int maxDigit = 16 )
        => Head.TryMatchHexNumber( out value, minDigit, maxDigit )
            ? SetSuccess()
            : AddExpectation( minDigit == maxDigit ? $"{minDigit} digits hexadecimal number" : $"Hexadecimal number ({minDigit} to {maxDigit} digits)" );

    /// <summary>
    /// Skips any white spaces or JS comments (//... or /* ... */) and always returns true.
    /// </summary>
    /// <returns>Always true to ease composition.</returns>
    public bool SkipWhiteSpacesAndJSComments() => Head.SkipWhiteSpacesAndJSComments();

    /// <summary>
    /// Tries to skip a quoted string. This handles escaped \" and \\ but not other
    /// escaped characters: the string may be invalid regarding JSON string grammar.
    /// See the string definition https://www.json.org/json-en.html.
    /// </summary>
    /// <param name="allowNull">True to allow 'null' token.</param>
    /// <returns>True on success, false otherwise.</returns>
    public bool TrySkipJSONQuotedString( bool allowNull = false )
        => Head.TrySkipJsonQuotedString( allowNull )
            ? SetSuccess()
            : AddExpectation( allowNull ? "JSON string or null" : "JSON string" );

    /// <summary>
    /// Tries to match a JSON quoted string. Invalid escaped characters (like \' or \x) are invalid and will fail.
    /// See the string definition https://www.json.org/json-en.html.
    /// </summary>
    /// <param name="content">Extracted content.</param>
    /// <param name="allowNull">True to allow 'null' token.</param>
    /// <returns>True on success, false otherwise.</returns>
    public bool TryMatchJSONQuotedString( out string? content, bool allowNull = false )
    {
        content = null;
        bool isEmpty = Head.Length == 0;
        if( isEmpty || Head[0] != '"' )
        {
            if( !isEmpty && allowNull && Head.TryMatch( "null" ) )
            {
                return SetSuccess();
            }
            return AddExpectation( allowNull ? "JSON string or null" : "JSON string" );
        }
        int i = 1;
        int len = Head.Length - 1;
        StringBuilder? b = null;
        while( len >= 0 )
        {
            if( len == 0 ) return AddExpectation( 1, "Ending quote" ); ;
            char c = Head[i++];
            --len;
            if( c == '"' ) break;
            if( c == '\\' )
            {
                if( len == 0 ) return AddExpectation( i - 1, "Valid JSON escape character" ); ;
                if( b == null )
                {
                    b = new StringBuilder();
                    b.Append( Head.Slice( 1, i - 1 ) );
                }
                switch( (c = Head[i++]) )
                {
                    case 'r': c = '\r'; break;
                    case 'n': c = '\n'; break;
                    case 'b': c = '\b'; break;
                    case 't': c = '\t'; break;
                    case 'f': c = '\f'; break;
                    case 'u':
                    {
                        var h = Head.Slice( i );
                        if( !h.TryMatchHexNumber( out var u, 4, 4 ) )
                        {
                            return AddExpectation( i - 1, "4 digits hexadecimal number" );
                        }
                        len -= 4;
                        i += 4;
                        c = (char)u;
                        break;
                    }
                    case '\\': // These are the only other valid escaped characters in JSON.
                    case '"':
                    case '/': break;
                    default:
                    {
                        return AddExpectation( i - 1, "Valid JSON escape character" );
                    }
                }
            }
            if( b != null ) b.Append( c );
        }
        if( b != null ) content = b.ToString();
        else content = new string( Head.Slice( 1, i - 2 ) );
        Head = Head.Slice( i );
        return SetSuccess();
    }

    /// <summary>
    /// Tries to skip a JSON terminal value: a "string", null, a number (double value), true or false.
    /// </summary>
    /// <returns>True on success, false otherwise.</returns>
    public bool TrySkipJSONTerminalValue()
        => Head.TrySkipJsonTerminalValue()
            ? SetSuccess()
            : AddExpectation( "null, true, false, a floating number or a \"string\"" );

    /// <summary>
    /// Tries to match a JSON terminal value: a "string", null, a number (double value), true or false.
    /// </summary>
    /// <param name="value">The parsed value.</param>
    /// <returns>True if a JSON value has been matched, false otherwise.</returns>
    public bool TryMatchJSONTerminalValue( out object? value )
    {
        if( TryMatch( "true" ) )
        {
            value = true;
        }
        else if( TryMatch( "false" ) )
        {
            value = false;
        }
        else if( TryMatchJSONQuotedString( out string? s, true ) )
        {
            value = s;
        }
        else if( TryMatchDouble( out double d ) )
        {
            value = d;
        }
        else
        {
            value = null;
            return false;
        }
        return SetSuccess();
    }

    /// <summary>
    /// Matches a very simple version of a JSON object content: this match stops at the first closing }.
    /// White spaces and JS comments (//... or /* ... */) are skipped.
    /// </summary>
    /// <param name="o">The read object on success as a list of tuples.</param>
    /// <returns>True on success, false on error.</returns>
    public bool TryMatchJSONObjectContent( [NotNullWhen( true )] out List<(string, object?)>? o )
    {
        o = new List<(string, object?)>();
        var savedHead = Head;
        using( OpenExpectations( "JSON object properties" ) )
        {
            while( Head.Length != 0 )
            {
                Head.SkipWhiteSpacesAndJSComments();
                if( Head.TryMatch( '}' ) )
                {
                    return SetSuccess();
                }
                if( !TryMatchJSONQuotedString( out string? propName ) )
                {
                    AddExpectation( "Quoted JSON Property Name" );
                    goto error;
                }
                Debug.Assert( propName != null );
                Head.SkipWhiteSpacesAndJSComments();
                if( !TryMatch( ':' ) || !TryMatchAnyJSON( out object? value ) ) goto error;
                o.Add( (propName, value) );
                Head.SkipWhiteSpacesAndJSComments();
                // This accepts a trailing comma at the end of a property list: ..."a":0,} is not an error.
                Head.TryMatch( ',' );
            }
            error:
            o = null;
            Head = savedHead;
            return false;
        }
    }

    /// <summary>
    /// Matches a JSON array content: the match ends with the first ].
    /// White spaces and JS comments (//... or /* ... */) are skipped.
    /// </summary>
    /// <param name="value">The list of objects on success.</param>
    /// <returns>True on success, false otherwise.</returns>
    public bool TryMatchJSONArrayContent( [NotNullWhen( true )] out List<object?>? value )
    {
        value = new List<object?>();
        var savedHead = Head;
        using( OpenExpectations( "JSON array values" ) )
        {
            while( Head.Length != 0 )
            {
                Head.SkipWhiteSpacesAndJSComments();
                if( Head.TryMatch( ']' ) )
                {
                    return SetSuccess();
                }
                if( !TryMatchAnyJSON( out object? cell ) ) goto error;
                value.Add( cell );
                Head.SkipWhiteSpacesAndJSComments();
                // Allow trailing comma: ,] is valid.
                Head.TryMatch( ',' );
            }
            error:
            value = null;
            Head = savedHead;
            return false;
        }
    }

    /// <summary>
    /// Tries to match a { "JSON" : "object" }, a ["JSON", "array"] or a terminal value (string, null, double, true or false) 
    /// and any combination of them.
    /// White spaces and JS comments (//... or /* ... */) are skipped.
    /// </summary>
    /// <param name="value">
    /// A list of nullable objects (for array), a list of tuples (string,object?) for object or
    /// a double, string, boolean or null (for null).
    /// </param>
    /// <returns>True on success, false on error.</returns>
    public bool TryMatchAnyJSON( out object? value )
    {
        value = null;
        var savedHead = Head;
        using( OpenExpectations( "Any JSON token or object" ) )
        {
            Head.SkipWhiteSpacesAndJSComments();
            if( Head.TryMatch( '{' ) )
            {
                if( !TryMatchJSONObjectContent( out var c ) ) goto error;
                value = c;
                Debug.Assert( !HasError );
                return true;
            }
            if( Head.TryMatch( '[' ) )
            {
                if( !TryMatchJSONArrayContent( out var t ) ) goto error;
                value = t;
                Debug.Assert( !HasError );
                return true;
            }
            if( TryMatchJSONTerminalValue( out value ) )
            {
                Debug.Assert( !HasError );
                return true;
            }
            error:
            value = null;
            Head = savedHead;
            return false;
        }
    }

}
