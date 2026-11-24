using Shouldly;
using System;
using System.IO;
using NUnit.Framework;

namespace CK.Core.Tests;


public class TemporaryFileTests
{
    // FileAttributes.Temporary exists on Windows only: elsewhere the file system ignores it.
    static readonly FileAttributes TemporaryOnWindows = OperatingSystem.IsWindows() ? FileAttributes.Temporary : FileAttributes.None;

    [Test]
    public void TemporaryFile_has_FileAttributes_Temporary_by_default()
    {
        string path = string.Empty;
        using( TemporaryFile temporaryFile = new TemporaryFile( true, null ) )
        {
            path = temporaryFile.Path;
            File.Exists( temporaryFile.Path ).ShouldBeTrue();
            (File.GetAttributes( temporaryFile.Path ) & FileAttributes.Temporary).ShouldBe( TemporaryOnWindows );
        }
        File.Exists( path ).ShouldBeFalse();

        using( TemporaryFile temporaryFile = new TemporaryFile() )
        {
            path = temporaryFile.Path;
            File.Exists( temporaryFile.Path ).ShouldBeTrue();
            (File.GetAttributes( temporaryFile.Path ) & FileAttributes.Temporary).ShouldBe( TemporaryOnWindows );
        }
        File.Exists( path ).ShouldBeFalse();

        using( TemporaryFile temporaryFile = new TemporaryFile( true ) )
        {
            path = temporaryFile.Path;
            File.Exists( temporaryFile.Path ).ShouldBeTrue();
            (File.GetAttributes( temporaryFile.Path ) & FileAttributes.Temporary).ShouldBe( TemporaryOnWindows );
        }
        File.Exists( path ).ShouldBeFalse();
    }

    [Test]
    public void an_empty_extension_is_like_no_extension()
    {
        string path = string.Empty;
        using( TemporaryFile temporaryFile = new TemporaryFile( " " ) )
        {
            path = temporaryFile.Path;
            File.Exists( temporaryFile.Path ).ShouldBeTrue();
            (File.GetAttributes( temporaryFile.Path ) & FileAttributes.Temporary).ShouldBe( TemporaryOnWindows );
        }
        File.Exists( path ).ShouldBeFalse();
    }

    [Test]
    public void TemporaryFileExtensionTest()
    {
        string path = string.Empty;
        using( TemporaryFile temporaryFile = new TemporaryFile( " " ) )
        {
            path = temporaryFile.Path;
            File.Exists( temporaryFile.Path ).ShouldBeTrue();
            (File.GetAttributes( temporaryFile.Path ) & FileAttributes.Temporary).ShouldBe( TemporaryOnWindows );
        }
        File.Exists( path ).ShouldBeFalse();

        using( TemporaryFile temporaryFile = new TemporaryFile( true, "." ) )
        {
            path = temporaryFile.Path;
            File.Exists( temporaryFile.Path ).ShouldBeTrue();
            path.EndsWith( ".tmp." ).ShouldBeTrue();
            (File.GetAttributes( temporaryFile.Path ) & FileAttributes.Temporary).ShouldBe( TemporaryOnWindows );
        }
        File.Exists( path ).ShouldBeFalse();

        using( TemporaryFile temporaryFile = new TemporaryFile( true, "tst" ) )
        {
            path = temporaryFile.Path;
            File.Exists( temporaryFile.Path ).ShouldBeTrue();
            path.EndsWith( ".tmp.tst" ).ShouldBeTrue();
            (File.GetAttributes( temporaryFile.Path ) & FileAttributes.Temporary).ShouldBe( TemporaryOnWindows );
        }
        File.Exists( path ).ShouldBeFalse();

        using( TemporaryFile temporaryFile = new TemporaryFile( true, ".tst" ) )
        {
            path = temporaryFile.Path;
            File.Exists( temporaryFile.Path ).ShouldBeTrue();
            path.EndsWith( ".tmp.tst" ).ShouldBeTrue();
            (File.GetAttributes( temporaryFile.Path ) & FileAttributes.Temporary).ShouldBe( TemporaryOnWindows );
        }
        File.Exists( path ).ShouldBeFalse();
    }

    [Test]
    public void TemporaryFileDetachTest()
    {
        string path = string.Empty;
        using( TemporaryFile temporaryFile = new TemporaryFile( true, null ) )
        {
            path = temporaryFile.Path;
            File.Exists( temporaryFile.Path ).ShouldBeTrue();
            (File.GetAttributes( temporaryFile.Path ) & FileAttributes.Temporary).ShouldBe( TemporaryOnWindows );
            temporaryFile.Detach();
        }
        File.Exists( path ).ShouldBeTrue();
        File.Delete( path );
    }
}
