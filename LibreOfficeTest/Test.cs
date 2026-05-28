//
// Test.cs
//
// Author: Kees van Spelde <sicos2002@hotmail.com>
//
// Copyright (c) 2026 Kees van Spelde. (www.magic-sessions.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NON INFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
using LibreOfficeKit;
using LibreOfficeKit.Exceptions;
using Microsoft.Extensions.Logging;

namespace LibreOfficeTest;

[TestClass]
public class ConverterTests
{
    #region Fields
    private static Converter _converter = null!;
    private static DirectoryInfo _tempDirectory = null!;
    private static TestContext _testContext = null!;
    #endregion

    [TestMethod]
    public async Task ConvertToPdfAsync_Timeouts_WhenWorkerIsDelayed()
    {
        var workerPath = Path.Combine(AppContext.BaseDirectory, "LibreOfficeKit.Console.exe");
        var outputFile = Path.Combine(_tempDirectory.FullName, "timeout.pdf");

        var previousDelay = Environment.GetEnvironmentVariable("LOK_WORKER_STARTUP_DELAY_MS");
        Environment.SetEnvironmentVariable("LOK_WORKER_STARTUP_DELAY_MS", "5000");

        try
        {
            await using var delayedConverter = new Converter(1, 0, TimeSpan.FromMinutes(5), workerPath);

            try
            {
                await delayedConverter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "filetypenotsupported.txt"), outputFile, TimeSpan.FromMilliseconds(100));
                Assert.Fail("Expected a timeout exception.");
            }
            catch (LibreOfficeKit.Exceptions.TimeoutException)
            {
                // expected
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOK_WORKER_STARTUP_DELAY_MS", previousDelay);
        }
    }

    [TestMethod]
    public async Task FileIsCorrupt()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A corrupt compound document.pdf");

        await Assert.ThrowsAsync<ConversionFailedException>(async () => 
        {
            await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A corrupt compound document.doc"), outputFile);
        });
    }

    #region Microsoft Office Word tests
    [TestMethod]
    [Timeout(10000)]
    public async Task DocWithoutEmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A DOC word document without embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A DOC word document without embedded files.doc"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task DocWith7EmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A DOC word document with 7 embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A DOC word document with 7 embedded files.doc"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task DocWithPassword()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A DOC word document with password.pdf");
        await Assert.ThrowsAsync<ConversionFailedException>(async () => 
        {
            await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A DOC word document with password.doc"), outputFile);
        });
    }

    [TestMethod]
    public async Task DocxWithoutEmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A DOCX word document without embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A DOCX word document without embedded files.docx"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task DocxWith7EmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A DOCX word document with 7 embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A DOCX word document with 7 embedded files.docx"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task DocxWith7EmbeddedFiles10Times()
    {
        var currentDir = AppContext.BaseDirectory;

        for (var i = 0; i < 10; i++)
        {
            var outputFile = Path.Combine(_tempDirectory.FullName, $"A DOCX word document with 7 embedded files_{i}.pdf");
            await _converter.ConvertToPdfAsync(Path.Combine(currentDir, "TestFiles", "A DOCX word document with 7 embedded files.docx"), outputFile);
            Assert.IsTrue(File.Exists(outputFile));
        }
    }

    [TestMethod]
    public async Task DocxWithPassword()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A DOCX word document with password.pdf");
        await Assert.ThrowsAsync<FilePasswordProtectedException>(async () =>
        {
            await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A DOCX word document with password.docx"), outputFile);
        });
    }
    #endregion

    #region Microsoft Office Excel tests
    [TestMethod]
    [Timeout(10000)]
    public async Task XlsWithoutEmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A XLS excel document without embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A XLS excel document without embedded files.xls"), outputFile, timeout: TimeSpan.FromMinutes(2));
        Assert.IsTrue(File.Exists(outputFile));
    }


    [TestMethod]
    public async Task XlsWith2EmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A XLS excel document with 2 embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A XLS excel document with 2 embedded files.xls"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task XlsWith2EmbeddedFiles10Times()
    {
        var currentDir = AppContext.BaseDirectory;

        for (var i = 0; i < 10; i++)
        {
            var outputFile = Path.Combine(_tempDirectory.FullName, $"A XLS excel document with 2 embedded files_{i}.pdf");
            await _converter.ConvertToPdfAsync(Path.Combine(currentDir, "TestFiles", "A XLS excel document with 2 embedded files.xls"), outputFile);
            Assert.IsTrue(File.Exists(outputFile));
        }
    }

    [TestMethod]
    public async Task XlsWithPassword()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A XLS excel document with password.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A XLS excel document with password.xls"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task XlsxWithoutEmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A XLSX excel document without embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A XLSX excel document without embedded files.xlsx"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task XlsxWith2EmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A XLSX excel document with 2 embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A XLSX excel document with 2 embedded files.xlsx"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task XlsxWithPassword()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A XLSX excel document with password.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A XLSX excel document with password.xlsx"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task CsvSemicolonSeparated()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "Semicolon separated csv.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "Semicolon separated csv.csv"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task CsvCommaSeparated()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "Comma separated csv.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "Comma separated csv.csv"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task CsvSpaceSeparated()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "Space separated csv.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "Space separated csv.csv"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task CsvTabSeparated()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "Tab separated csv.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "Tab separated csv.csv"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }
    #endregion

    #region Microsoft Office PowerPoint tests
    [TestMethod]
    public async Task PptWithoutEmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A PPT PowerPoint document without embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A PPT PowerPoint document without embedded files.ppt"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task PptWith3EmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A PPT PowerPoint document with 3 embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A PPT powerpoint document with 3 embedded files.ppt"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task PptWithPassword()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A PPT PowerPoint document with password.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A PPT PowerPoint document with password.ppt"), outputFile);
    }

    [TestMethod]
    public async Task PptxWithoutEmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A PPTX PowerPoint document without embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A PPTX PowerPoint document without embedded files.pptx"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task PptxWith3EmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A PPTX PowerPoint document with 3 embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A PPTX powerpoint document with 3 embedded files.pptx"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task PptxWithPassword()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "A PPTX PowerPoint document with password.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "A PPTX PowerPoint document with password.pptx"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }
    #endregion

    #region Open Office Writer tests
    [TestMethod]
    public async Task OdtWithoutEmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "An ODT document without embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "An ODT document without embedded files.odt"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task OdtWith8EmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "An ODT document with 8 embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "An ODT document with 8 embedded files.odt"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task OdtWithPassword()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "An ODT document with password.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "An ODT document with password.odt"), outputFile);
    }
    #endregion
    
    #region Open Office Impress tests
    [TestMethod]
    public async Task OdpWithoutEmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "An ODP document without embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "An ODP document without embedded files.odp"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task OdpWith3EmbeddedFiles()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "An ODP document with 3 embedded files.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "An ODP document with 3 embedded files.odp"), outputFile);
        Assert.IsTrue(File.Exists(outputFile));
    }

    [TestMethod]
    public async Task OdpWithPassword()
    {
        var outputFile = Path.Combine(_tempDirectory.FullName, "OdpWithPassword.pdf");
        await _converter.ConvertToPdfAsync(Path.Combine(AppContext.BaseDirectory, "TestFiles", "An ODP document with password.odp"), outputFile);
    }
    #endregion

    #region Helper methods
    [ClassInitialize]
    public static void TestInitialize(TestContext context)
    {
        _testContext = context;
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _tempDirectory = new DirectoryInfo(tempDirectory);
        _tempDirectory.Create();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new TestContextLoggerProvider()).AddFilter(null, LogLevel.Trace));
        var logger = loggerFactory.CreateLogger<Converter>();
        logger.LogInformation("Logger initialized, creating Converter...");

        //_converter = new Converter(1, 1, new TimeSpan(0, 5, 0), logger: logger);
        _converter = new Converter(1, 1, new TimeSpan(0, 5, 0), @"..\..\..\..\LibreOfficeKit.Console\bin\Debug\net10.0\LibreOfficeKit.Console.exe", logger);
    }

    [ClassCleanup]
    public static async Task TestCleanup()
    {
        if (_tempDirectory.Exists)
            _tempDirectory.Delete(true);

        await _converter.DisposeAsync();
    }
    #endregion
}