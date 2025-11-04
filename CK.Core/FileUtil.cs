using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Core;

/// <summary>
/// Helper functions related to file system.
/// </summary>
public static partial class FileUtil
{
    /// <summary>
    /// Combination of <see cref="FileAttributes"/> that can not exist: it can be used to 
    /// tag non existing files among other existing (i.e. valid) file attributes.
    /// </summary>
    static readonly public FileAttributes InexistingFile = FileAttributes.Normal | FileAttributes.Offline;

    /// <summary>
    /// The file header for gzipped files.
    /// </summary>
    static readonly public byte[] GzipFileHeader = new byte[] { 0x1f, 0x8b };

    /// <summary>
    /// Canonicalizes the path: all '/' and '\' are mapped to <see cref="Path.DirectorySeparatorChar"/> 
    /// (and <see cref="Path.AltDirectorySeparatorChar"/> will also be transformed).
    /// </summary>
    /// <param name="path">The path to standardize (must be not be null). It is trimmed and if the path is empty, the empty string is returned.</param>
    /// <param name="ensureTrailingBackslash">
    /// Ensures that the normalized path will end with a <see cref="Path.DirectorySeparatorChar"/>.
    /// It should be true for path to directories because we consider that a directory path SHOULD end with 
    /// the slash as often as possible.
    /// When <paramref name="path"/> is empty, this is not applied to preserve the fact that the string is empty.
    /// </param>
    /// <returns>
    /// A standardized path, whatever the actual <c>Path.DirectorySeparatorChar</c> is
    /// on the current platform.
    /// </returns>
    static public string NormalizePathSeparator( string path, bool ensureTrailingBackslash )
    {
        Throw.CheckNotNullArgument( path );
        path = path.Trim();
        if( path.Length == 0 ) return path;
        if( Path.DirectorySeparatorChar != '/' && Path.AltDirectorySeparatorChar != '/' )
            path = path.Replace( '/', Path.DirectorySeparatorChar );
        if( Path.DirectorySeparatorChar != '\\' && Path.AltDirectorySeparatorChar != '\\' )
            path = path.Replace( '\\', Path.DirectorySeparatorChar );
        path = path.Replace( Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar );
        if( ensureTrailingBackslash && path[^1] != Path.DirectorySeparatorChar )
        {
            path += Path.DirectorySeparatorChar;
        }
        return path;
    }

    static readonly char[] _invalidPathChars = Path.GetInvalidPathChars();
    static readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Gets the <see cref="Path.DirectorySeparatorChar"/> as a string.
    /// </summary>
    public static readonly string DirectorySeparatorString = Path.DirectorySeparatorChar.ToString();

    /// <summary>
    /// Gets the <see cref="Path.AltDirectorySeparatorChar"/> as a string.
    /// </summary>
    public static readonly string AltDirectorySeparatorString = Path.AltDirectorySeparatorChar.ToString();

    /// <summary>
    /// A display format for <see cref="DateTime"/> that supports round-trips, is readable and can be used in path 
    /// or url (the DateTime should be in UTC since <see cref="DateTime.Kind"/> is ignored).
    /// Use <see cref="TryParseFileNameUniqueTimeUtcFormat"/> or <see cref="TryParseFileNameUniqueTimeUtcFormat"/> to parse it (it uses the correct <see cref="DateTimeStyles"/>).
    /// It is: @"yyyy-MM-dd HH\hmm.ss.fffffff"
    /// </summary>
    public static readonly string FileNameUniqueTimeUtcFormat = @"yyyy-MM-dd HH\hmm.ss.fffffff";

    /// <summary>
    /// The time returned by <see cref="File.GetLastWriteTimeUtc(string)"/> when the file does not exist.
    /// From MSDN: If the file described in the path parameter does not exist, this method returns 12:00 midnight, January 1, 1601 A.D. (C.E.) Coordinated Universal Time (UTC).
    /// </summary>
    public static readonly DateTime MissingFileLastWriteTimeUtc = new DateTime( 1601, 1, 1, 0, 0, 0, DateTimeKind.Utc );

    /// <summary>
    /// Tries to match a DateTime in the <see cref="FileNameUniqueTimeUtcFormat"/> format.
    /// </summary>
    /// <param name="head">This head.</param>
    /// <param name="time">Result time on success; otherwise <see cref="Util.UtcMinValue"/>.</param>
    /// <returns>True on success, false otherwise.</returns>
    public static bool TryMatchFileNameUniqueTimeUtcFormat( this ref ReadOnlySpan<char> head, out DateTime time )
    {
        time = Util.UtcMinValue;
        Debug.Assert( FileNameUniqueTimeUtcFormat.Replace( "\\", "" ).Length == 27 );
        if( head.Length >= 27
            && DateTime.TryParseExact( head.Slice( 0, 27 ), FileNameUniqueTimeUtcFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out time ) )
        {
            head = head.Slice( 27 );
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to match a DateTime in the <see cref="FileNameUniqueTimeUtcFormat"/> format.
    /// </summary>
    /// <param name="m">This matcher.</param>
    /// <param name="time">Result time on success; otherwise <see cref="Util.UtcMinValue"/>.</param>
    /// <returns>True on success, false otherwise.</returns>
    public static bool TryMatchFileNameUniqueTimeUtcFormat( this ref ROSpanCharMatcher m, out DateTime time )
        => m.Head.TryMatchFileNameUniqueTimeUtcFormat( out time )
            ? m.SetSuccess()
            : m.AddExpectation( "UTC time" );

    /// <summary>
    /// Tries to parse a string formatted with the <see cref="FileNameUniqueTimeUtcFormat"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="time">Result time on success.</param>
    /// <returns>True on success, false otherwise.</returns>
    public static bool TryParseFileNameUniqueTimeUtcFormat( ReadOnlySpan<char> s, out DateTime time )
        => TryMatchFileNameUniqueTimeUtcFormat( ref s, out time ) && s.IsEmpty;

    /// <summary>
    /// Finds the first character index of any characters that are invalid in a path.
    /// This method (and <see cref="IndexOfInvalidFileNameChars"/>) avoid the allocation of 
    /// the array each time <see cref="Path.GetInvalidPathChars"/> is called.
    /// </summary>
    /// <param name="path">Path to check.</param>
    /// <returns>A negative value if not found.</returns>
    public static int IndexOfInvalidPathChars( ReadOnlySpan<char> path )
    {
        return path.IndexOfAny( _invalidPathChars );
    }

    /// <summary>
    /// Finds the first character index of any characters that are invalid in a file name.
    /// This method (and <see cref="IndexOfInvalidPathChars"/>) avoid the allocation of 
    /// the array each time <see cref="Path.GetInvalidFileNameChars"/> is called.
    /// </summary>
    /// <param name="path">Path to check.</param>
    /// <returns>A negative value if not found.</returns>
    public static int IndexOfInvalidFileNameChars( ReadOnlySpan<char> path )
    {
        return path.IndexOfAny( _invalidFileNameChars );
    }

    /// <summary>
    /// Creates a new necessarily unique file and writes bytes content in a directory that must exist.
    /// The file name is based on a <see cref="DateTime"/>, with an eventual uniquifier if a file already exists with the same name.
    /// </summary>
    /// <param name="pathPrefix">The path prefix. Must not be null. Must be a valid path and may ends with a prefix for the file name itself.</param>
    /// <param name="fileSuffix">Suffix for the file name. Must not be null. Typically an extension (like ".txt").</param>
    /// <param name="time">The time that will be used to create the file name. It should be an UTC time.</param>
    /// <param name="content">The bytes to write. Can be null or empty if the file must only be created.</param>
    /// <param name="withUTF8Bom">True to write the UTF8 Byte Order Mask (the preamble).</param>
    /// <param name="maxTryBeforeGuid">Maximum value for short hexa uniquifier before using a base 64 guid suffix. Must between 0 and 15 (included).</param>
    /// <returns>The full path name of the created file.</returns>
    public static string WriteUniqueTimedFile( string pathPrefix, string fileSuffix, DateTime time, byte[] content, bool withUTF8Bom, int maxTryBeforeGuid = 3 )
    {
        string fullLogFilePath;
        using( var f = CreateAndOpenUniqueTimedFile( pathPrefix, fileSuffix, time, FileAccess.Write, FileShare.Read, 8, FileOptions.SequentialScan | FileOptions.WriteThrough, maxTryBeforeGuid ) )
        {
            Debug.Assert( Encoding.UTF8.GetPreamble().Length == 3 );
            if( withUTF8Bom ) f.Write( Encoding.UTF8.GetPreamble(), 0, 3 );
            if( content != null && content.Length > 0 ) f.Write( content, 0, content.Length );
            fullLogFilePath = f.Name;
        }
        return fullLogFilePath;
    }

    /// <summary>
    /// Creates and opens a new necessarily unique file in a directory that must exist.
    /// The file name is based on a <see cref="DateTime"/>, with an eventual uniquifier if a file already exists with the same name.
    /// You can use <see cref="FileStream.Name"/> to obtain the file name.
    /// </summary>
    /// <param name="pathPrefix">The path prefix. Must not be null. Must be a valid path and may ends with a prefix for the file name itself.</param>
    /// <param name="fileSuffix">Suffix for the file name. Must not be null. Typically an extension (like ".txt").</param>
    /// <param name="time">The time that will be used to create the file name. It must be an UTC time.</param>
    /// <param name="access">
    /// A constant that determines how the file can be accessed by the FileStream object. 
    /// It can only be <see cref="FileAccess.Write"/> or <see cref="FileAccess.ReadWrite"/> (when set to <see cref="FileAccess.Read"/> a <see cref="ArgumentException"/> is thrown).
    /// This sets the CanRead and CanWrite properties of the FileStream object. 
    /// CanSeek is true if path specifies a disk file.
    /// </param>
    /// <param name="share">
    /// A constant that determines how the file will be shared by processes.
    /// </param>
    /// <param name="bufferSize">
    /// A positive Int32 value greater than 0 indicating the buffer size. For bufferSize values between one and eight, the actual buffer size is set to eight bytes.
    /// </param>
    /// <param name="options">Specifies additional file options.</param>
    /// <param name="maxTryBeforeGuid">
    /// Maximum value for short hexadecimal uniquifier before using a base 64 guid suffix. Must be greater than 0.</param>
    /// <returns>An opened <see cref="FileStream"/>.</returns>
    public static FileStream CreateAndOpenUniqueTimedFile( string pathPrefix, string fileSuffix, DateTime time, FileAccess access, FileShare share, int bufferSize, FileOptions options, int maxTryBeforeGuid = 512 )
    {
        Throw.CheckArgument( access != FileAccess.Read );
        FileStream? f = null;
        FindUniqueTimedFileOrFolder( pathPrefix, fileSuffix, time, maxTryBeforeGuid, p => TryCreateNew( p, access, share, bufferSize, options, out f ) );
        return f!;
    }

    static bool TryCreateNew( string timedPath, FileAccess access, FileShare share, int bufferSize, FileOptions options, [NotNullWhen( true )] out FileStream? f )
    {
        f = null;
        try
        {
            if( File.Exists( timedPath ) ) return false;
            f = new FileStream( timedPath, FileMode.CreateNew, access, share, bufferSize, options );
            return true;
        }
        catch( IOException ex )
        {
            if( ex is PathTooLongException || ex is DirectoryNotFoundException ) throw;
        }
        return false;
    }

    /// <summary>
    /// Moves (renames) a file to a necessarily unique named file.
    /// The file name is based on a <see cref="DateTime"/>, with an eventual uniquifier if a file already exists with the same name.
    /// </summary>
    /// <param name="sourceFilePath">Path of the file to move.</param>
    /// <param name="pathPrefix">The path prefix. Must not be null. Must be a valid path and may ends with a prefix for the file name itself.</param>
    /// <param name="fileSuffix">Suffix for the file name. Must not be null. Typically an extension (like ".txt").</param>
    /// <param name="time">The time that will be used to create the file name. It must be an UTC time.</param>
    /// <param name="maxTryBeforeGuid">
    /// Maximum value for short hexadecimal uniquifier before using a base 64 guid suffix. Must greater than 0.
    /// </param>
    /// <returns>The path of the target file.</returns>
    public static string MoveToUniqueTimedFile( string sourceFilePath, string pathPrefix, string fileSuffix, DateTime time, int maxTryBeforeGuid = 512 )
    {
        Throw.CheckNotNullArgument( sourceFilePath );
        Throw.CheckArgument( File.Exists( sourceFilePath ) );
        return FindUniqueTimedFileOrFolder( pathPrefix, fileSuffix, time, maxTryBeforeGuid, p => TryMoveTo( sourceFilePath, p ) );
    }

    /// <summary>
    /// Gets a path to a necessarily unique named file.
    /// The file name is based on a <see cref="DateTime"/>, with an eventual uniquifier if a file already exists with the same name.
    /// </summary>
    /// <param name="pathPrefix">The path prefix. Must not be null. Must be a valid path and may ends with a prefix for the file name itself.</param>
    /// <param name="fileSuffix">Suffix for the file name. Must not be null. Typically an extension (like ".txt").</param>
    /// <param name="time">The time that will be used to create the file name. It must be an UTC time.</param>
    /// <param name="maxTryBeforeGuid">
    /// Maximum value for short hexadecimal uniquifier before using a base 64 guid suffix. Must be greater than 0.
    /// </param>
    /// <returns>A string to a necessarily unique named file path.</returns>
    public static string EnsureUniqueTimedFile( string pathPrefix, string fileSuffix, DateTime time, int maxTryBeforeGuid = 512 )
    {
        return FindUniqueTimedFileOrFolder( pathPrefix, fileSuffix, time, maxTryBeforeGuid, TryCreateFile );
    }

    static bool TryCreateFile( string path )
    {
        try
        {
            if( File.Exists( path ) ) return false;
            using( File.Create( path ) ) { } // Dispose immediately
            return true;
        }
        catch( IOException ex )
        {
            if( ex is PathTooLongException || ex is DirectoryNotFoundException ) throw;
        }
        return false;
    }

    /// <summary>
    /// Gets a path to a necessarily unique time-based named folder.
    /// The folder name is based on a <see cref="DateTime"/>, with an eventual uniquifier if a folder already exists with the same name.
    /// </summary>
    /// <param name="pathPrefix">The path prefix. Must not be null. Must be a valid path and may ends with a prefix for the file name itself.</param>
    /// <param name="folderSuffix">Suffix for the folder name.</param>
    /// <param name="time">The time that will be used to create the file name. It must be an UTC time.</param>
    /// <param name="maxTryBeforeGuid">
    /// Maximum value for short hexadecimal uniquifier before using a base 64 guid suffix. Must be greater than 0.
    /// </param>
    /// <returns>The path to a necessarily unique folder.</returns>
    public static string CreateUniqueTimedFolder( string pathPrefix, string? folderSuffix, DateTime time, int maxTryBeforeGuid = 512 )
    {
        return FindUniqueTimedFileOrFolder( pathPrefix, folderSuffix ?? String.Empty, time, maxTryBeforeGuid, TryCreateFolder );
    }

    static bool TryCreateFolder( string path )
    {
        string? origin = null;
        try
        {
            if( Directory.Exists( path ) ) return false;
            // Directory.CreateDirectory can not be used here.
            // The trick n°1 is moving an empty folder: it fails if the destination already exists.
            // The second trick is to always create the parent folder:
            //  - Move requires it to exist...
            //  - ... but since we WILL succeed to create the unique folder, we can do it safely.
            string? originParent = Path.GetTempPath();
            string rootOfPathToCreate = Path.GetPathRoot( path )!;
            var parentOfPathToCreate = Path.GetDirectoryName( path );
            if( parentOfPathToCreate == null ) return false;
            if( Path.GetPathRoot( originParent ) != rootOfPathToCreate )
            {
                // Path to create is not on the same volume as the Temporary folder.
                // We need to create our origin folder on the same volume: we try to create
                // it as close as possible to the target folder.
                originParent = parentOfPathToCreate;
                Debug.Assert( originParent != null );
                while( originParent.Length > rootOfPathToCreate.Length
                        && !Directory.Exists( originParent ) )
                {
                    originParent = Path.GetDirectoryName( originParent );
                    Debug.Assert( originParent != null );
                }
                if( originParent.Length > rootOfPathToCreate.Length )
                {
                    originParent += DirectorySeparatorString;
                }
            }
            origin = originParent + Guid.NewGuid().ToString( "N" );
            Directory.CreateDirectory( origin );
            Directory.CreateDirectory( parentOfPathToCreate );
            Directory.Move( origin, path );
            return true;
        }
        catch( IOException ex )
        {
            if( ex is PathTooLongException ) throw;
            try
            {
                if( origin != null && Directory.Exists( origin ) ) Directory.Delete( origin );
            }
            catch
            {
                // Forget the temp folder suppression.
            }
        }
        return false;
    }


    static bool TryMoveTo( string sourceFilePath, string timedPath )
    {
        try
        {
            if( File.Exists( timedPath ) ) return false;
            File.Move( sourceFilePath, timedPath );
            return true;
        }
        catch( IOException ex )
        {
            if( ex is PathTooLongException || ex is DirectoryNotFoundException ) throw;
        }
        return false;
    }

    static string FindUniqueTimedFileOrFolder( string pathPrefix, string fileSuffix, DateTime time, int maxTryBeforeGuid, Func<string, bool> tester )
    {
        Throw.CheckNotNullArgument( pathPrefix );
        Throw.CheckNotNullArgument( fileSuffix );
        if( maxTryBeforeGuid < 0 ) Throw.ArgumentOutOfRangeException( nameof( maxTryBeforeGuid ) );

        DateTimeStamp timeStamp = new DateTimeStamp( time );
        int counter = 0;
        string result = pathPrefix + timeStamp.ToString() + fileSuffix;
        for(; ; )
        {
            if( tester( result ) ) break;
            if( counter < maxTryBeforeGuid )
            {
                timeStamp = new DateTimeStamp( timeStamp, timeStamp );
                result = pathPrefix + timeStamp.ToString() + fileSuffix;
            }
            else
            {
                if( counter == maxTryBeforeGuid + 1 ) Throw.Exception( Impl.CoreResources.FileUtilUnableToCreateUniqueTimedFileOrFolder );
                if( counter == maxTryBeforeGuid )
                {
                    result = pathPrefix + FormatTimedUniqueFilePart( time ) + fileSuffix;
                }
            }
            ++counter;
        }
        return result;
    }

    /// <summary>
    /// Formats a string that is file name compatible from the given time and a <see cref="Guid.NewGuid()"/>
    /// where time uses <see cref="FileNameUniqueTimeUtcFormat"/> and the new Guid is 
    /// encoded with http://en.wikipedia.org/wiki/Base64#URL_applications.
    /// </summary>
    /// <param name="time">The date time to use.</param>
    /// <returns>A string with the time and a new guid in a file system compatible format.</returns>
    public static string FormatTimedUniqueFilePart( DateTime time )
    {
        Debug.Assert( Convert.ToBase64String( Guid.NewGuid().ToByteArray() ).Length == 24 );
        Debug.Assert( Convert.ToBase64String( Guid.NewGuid().ToByteArray() ).EndsWith( "==" ) );
        string dedup = Base64UrlHelper.ToBase64UrlString( Guid.NewGuid().ToByteArray() );
        return time.ToString( FileNameUniqueTimeUtcFormat, CultureInfo.InvariantCulture ) + "-" + dedup;
    }

    /// <summary>
    /// Recursively copy a directory, creates it if it does not already exists. 
    /// Throws an IOException, if a same file exists in the target directory.
    /// </summary>
    /// <param name="src">The source directory.</param>
    /// <param name="target">The target directory.</param>
    /// <param name="withHiddenFiles">False to skip hidden files.</param>
    /// <param name="withHiddenFolders">False to skip hidden folders.</param>
    /// <param name="fileFilter">Optional predicate for directories.</param>
    /// <param name="dirFilter">Optional predicate for files.</param>
    public static void CopyDirectory( DirectoryInfo src, DirectoryInfo target, bool withHiddenFiles = true, bool withHiddenFolders = true, Func<FileInfo, bool>? fileFilter = null, Func<DirectoryInfo, bool>? dirFilter = null )
    {
        Throw.CheckNotNullArgument( src );
        Throw.CheckNotNullArgument( target );
        if( !target.Exists ) target.Create();
        DirectoryInfo[] dirs = src.GetDirectories();
        foreach( DirectoryInfo d in dirs )
        {
            if( (withHiddenFolders || ((d.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden))
                && (dirFilter == null || dirFilter( d )) )
            {
                CopyDirectory( d, new DirectoryInfo( Path.Combine( target.FullName, d.Name ) ), withHiddenFiles, withHiddenFolders );
            }
        }
        FileInfo[] files = src.GetFiles();
        foreach( FileInfo f in files )
        {
            if( (withHiddenFiles || ((f.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden))
                && (fileFilter == null || fileFilter( f )) )
            {
                f.CopyTo( Path.Combine( target.FullName, f.Name ) );
            }
        }
    }

    /// <summary>
    /// Waits for a file to be writable or does not exist (if it does not exist, it can be created!).
    /// The file is opened and close.
    /// Waits the number of <paramref name="nbMaxMilliSecond"/> before leaving and returning false: when 0 (the default),
    /// there is no wait. A nbMaxMilliSecond below 20 ~ 30 milliseconds is not accurate: even with nbMaxMilliSecond = 1
    /// this method will return true if the file becomes writable during the next 10 or 20 milliseconds.
    /// </summary>
    /// <param name="path">The path of the file to write to.</param>
    /// <param name="nbMaxMilliSecond">Maximum number of milliseconds to wait before returning false.</param>
    /// <returns>True if the file has been correctly opened (and closed) in write mode.</returns>
    static public bool CheckForWriteAccess( string path, int nbMaxMilliSecond = 0 )
    {
        Throw.CheckNotNullArgument( path );
        DateTime start = DateTime.UtcNow;
        if( !File.Exists( path ) ) return true;
        try
        {
            using( Stream s = File.OpenWrite( path ) ) { return true; }
        }
        catch
        {
            int waitTime = nbMaxMilliSecond / 100;
            if( nbMaxMilliSecond <= 0 ) return false;
            long stop = start.AddMilliseconds( nbMaxMilliSecond ).Ticks;
            for(; ; )
            {
                if( waitTime > 0 ) Thread.Sleep( waitTime );
                if( !File.Exists( path ) ) return true;
                try
                {
                    using( Stream s = File.OpenWrite( path ) ) { return true; }
                }
                catch
                {
                    if( DateTime.UtcNow.Ticks > stop ) return false;
                    if( waitTime < 20 ) waitTime += 1;
                }
            }
        }
    }

    /// <summary>
    /// Compresses a file to another file asynchronously, using GZip at the given compression level.
    /// </summary>
    /// <param name="sourceFilePath">The source file path.</param>
    /// <param name="destinationPath">The destination path. If it doesn't exist, it will be created. If it exists, it will be replaced.</param>
    /// <param name="deleteSourceFileOnSuccess">If set to <c>true</c>, will delete source file if no error occurred during compression.</param>
    /// <param name="level">Compression level to use.</param>
    /// <param name="bufferSize">Size of the buffer, in bytes.</param>
    /// <param name="cancellationToken">Optional cancellation token for the task.</param>
    public static async Task CompressFileToGzipFileAsync( string sourceFilePath,
                                                          string destinationPath,
                                                          bool deleteSourceFileOnSuccess = true,
                                                          CompressionLevel level = CompressionLevel.Optimal,
                                                          int bufferSize = 64 * 1024,
                                                          CancellationToken cancellationToken = default )
    {
        using( FileStream source = new FileStream( sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan ) )
        {
            using( FileStream destination = new FileStream( destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan ) )
            {
                // GZipStream writes the GZipFileGeader.
                using( GZipStream gZipStream = new GZipStream( destination, level ) )
                {
                    await source.CopyToAsync( gZipStream, bufferSize, cancellationToken );
                }
            }
        }

        if( !cancellationToken.IsCancellationRequested && deleteSourceFileOnSuccess )
        {
            File.Delete( sourceFilePath );
        }
    }

    /// <summary>
    /// Compresses a file to another file, using GZip at the given compression level.
    /// </summary>
    /// <param name="sourceFilePath">The source file path.</param>
    /// <param name="destinationPath">The destination path. If it doesn't exist, it will be created. If it exists, it will be replaced.</param>
    /// <param name="deleteSourceFileOnSuccess">if set to <c>true</c>, will delete source file if no errors occured during compression.</param>
    /// <param name="level">Compression level to use.</param>
    /// <param name="bufferSize">Size of the buffer, in bytes.</param>
    public static void CompressFileToGzipFile( string sourceFilePath, string destinationPath, bool deleteSourceFileOnSuccess, CompressionLevel level = CompressionLevel.Optimal, int bufferSize = 64 * 1024 )
    {
        using( FileStream source = new FileStream( sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize, useAsync: false ) )
        {
            using( FileStream destination = new FileStream( destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: false ) )
            {
                using( GZipStream gZipStream = new GZipStream( destination, level ) )
                {
                    source.CopyTo( gZipStream, bufferSize );
                }
            }
        }
        if( deleteSourceFileOnSuccess )
        {
            File.Delete( sourceFilePath );
        }
    }

}
