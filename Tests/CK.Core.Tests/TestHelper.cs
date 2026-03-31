using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CK.Core.Tests;

static partial class TestHelper
{
    static string? _testFolder;
    static string? _solutionFolder;

    static TestHelper()
    {
    }

    public static string TestFolder
    {
        get
        {
            if( _testFolder == null ) InitializePaths();
            return _testFolder;
        }
    }

    public static string SolutionFolder
    {
        get
        {
            if( _solutionFolder == null ) InitializePaths();
            return _solutionFolder;
        }
    }

    public static void CleanupTestFolder()
    {
        DeleteFolder( TestFolder, true );
    }

    public static void DeleteFolder( string directoryPath, bool recreate = false )
    {
        int tryCount = 0;
        for(; ; )
        {
            try
            {
                if( Directory.Exists( directoryPath ) ) Directory.Delete( directoryPath, true );
                if( recreate )
                {
                    Directory.CreateDirectory( directoryPath );
                    File.WriteAllText( Path.Combine( directoryPath, "TestWrite.txt" ), "Test write works." );
                    File.Delete( Path.Combine( directoryPath, "TestWrite.txt" ) );
                }
                return;
            }
            catch( Exception ex )
            {
                if( ++tryCount == 20 ) throw;
                Console.WriteLine( "{1} - While cleaning up directory '{0}'. Retrying.", directoryPath, ex.Message );
                System.Threading.Thread.Sleep( 100 );
            }
        }
    }

    [MemberNotNull( nameof( _solutionFolder ), nameof( _testFolder ) )]
    static void InitializePaths()
    {
        NormalizedPath path = AppContext.BaseDirectory;
        var s = path.PathsToFirstPart( null!, ["CK-Core.slnx", "CK-Core.sln"] ).FirstOrDefault( p => File.Exists( p ) );
        if( s.IsEmptyPath ) throw new InvalidOperationException( $"Unable to find CK-Core.sln above '{AppContext.BaseDirectory}'." );
        _solutionFolder = s.RemoveLastPart();
        _testFolder = Path.Combine( _solutionFolder, "Tests", "CK.Core.Tests", "TestDir" );
        Console.WriteLine( $"SolutionFolder is: {_solutionFolder}." );
        Console.WriteLine( $"TestFolder is: {_testFolder}." );
        Console.WriteLine( $"Core path: {typeof( string ).GetTypeInfo().Assembly.Location}." );
        CleanupTestFolder();
    }

}
