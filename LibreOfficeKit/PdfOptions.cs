//
// PdfOptions.cs
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
// ReSharper disable UnusedMember.Global

using LibreOfficeKit.Enums;

namespace LibreOfficeKit;

/// <summary>
///     PDF export options that map to LibreOffice FilterData parameters.
///     Use this class to configure the PDF output quality, compliance, security, and layout.
/// </summary>
public class PdfOptions
{
    #region Properties
    /// <summary>
    ///     Gets or sets whether to use lossless (PNG) compression for images.
    /// </summary>
    public bool UseLosslessCompression { get; set; }

    /// <summary>
    ///     Gets or sets the JPEG compression quality (1-100).
    /// </summary>
    public int Quality { get; set; } = 90;

    /// <summary>
    ///     Gets or sets whether to reduce image resolution to <see cref="MaxImageResolutionDpi" />.
    /// </summary>
    public bool ReduceImageResolution { get; set; } = true;

    /// <summary>
    ///     Gets or sets the maximum image resolution in DPI.
    /// </summary>
    public int MaxImageResolutionDpi { get; set; } = 300;

    /// <summary>
    ///     Gets or sets whether to export bookmarks (outlines) to PDF.
    /// </summary>
    public bool ExportBookmarks { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to export document notes/comments.
    /// </summary>
    public bool ExportNotes { get; set; }

    /// <summary>
    ///     Gets or sets whether to export form fields as interactive widgets.
    /// </summary>
    public bool ExportFormFields { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to create a tagged PDF for accessibility.
    /// </summary>
    public bool UseTaggedPdf { get; set; }

    /// <summary>
    ///     Gets or sets whether to fit spreadsheet sheets on a single PDF page.
    /// </summary>
    public bool SinglePageSheets { get; set; }

    /// <summary>
    ///     Gets or sets the PDF/A compliance level.
    /// </summary>
    public PdfACompliance PdfACompliance { get; set; } = PdfACompliance.None;

    /// <summary>
    ///     Gets or sets the PDF version/compliance level.
    /// </summary>
    public PdfVersion? PdfVersion { get; set; }

    /// <summary>
    ///     Gets or sets the encryption password for the PDF document.
    /// </summary>
    public string? EncryptionPassword { get; set; }

    /// <summary>
    ///     Gets or sets the security and permission settings for PDF export.
    /// </summary>
    public PdfSecurityOptions? Security { get; set; }

    /// <summary>
    ///     Gets or sets the image compression settings for PDF export.
    /// </summary>
    public PdfCompressionOptions? Compression { get; set; }

    /// <summary>
    ///     Gets or sets watermark text for all pages.
    /// </summary>
    public string? Watermark { get; set; }

    /// <summary>
    ///     Gets or sets the page range to export (null for all pages).
    /// </summary>
    public string? PageRange { get; set; }

    /// <summary>
    ///     Gets or sets the initial viewer panel state.
    /// </summary>
    public InitialView InitialView { get; set; } = InitialView.Default;
    #endregion

    #region Presets
    /// <summary>
    ///     Gets high quality print/archival settings.
    /// </summary>
    public static PdfOptions HighQuality => new()
    {
        UseLosslessCompression = true,
        Quality = 100,
        ReduceImageResolution = false,
        ExportBookmarks = true,
        UseTaggedPdf = true
    };

    /// <summary>
    ///     Gets screen-optimized settings.
    /// </summary>
    public static PdfOptions Screen => new()
    {
        UseLosslessCompression = false,
        Quality = 85,
        ReduceImageResolution = true,
        MaxImageResolutionDpi = 150,
        ExportBookmarks = true
    };

    /// <summary>
    ///     Gets print-optimized balance of quality and size.
    /// </summary>
    public static PdfOptions Print => new()
    {
        UseLosslessCompression = false,
        Quality = 90,
        ReduceImageResolution = true,
        MaxImageResolutionDpi = 300,
        ExportBookmarks = true
    };

    /// <summary>
    ///     Gets PDF/A-2b archival settings.
    /// </summary>
    public static PdfOptions Archive => new()
    {
        UseLosslessCompression = false,
        Quality = 90,
        ReduceImageResolution = true,
        MaxImageResolutionDpi = 300,
        ExportBookmarks = true,
        UseTaggedPdf = true,
        PdfACompliance = PdfACompliance.Level2B,
        PdfVersion = Enums.PdfVersion.PdfA2B
    };
    #endregion

    #region Validate
    /// <summary>
    ///     Validates the PDF options and throws if the configuration is invalid.
    /// </summary>
    internal void Validate()
    {
        if (Quality < 1 || Quality > 100)
            throw new ArgumentOutOfRangeException(nameof(Quality), "Must be between 1 and 100.");

        if (MaxImageResolutionDpi <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxImageResolutionDpi), "Must be a positive value.");

        Compression?.Validate();
        Security?.Validate();
    }
    #endregion

    #region ToFilterOptions
    /// <summary>
    ///     Converts the PDF options to a LibreOffice filter options string suitable for the <c>saveAs</c> API.
    /// </summary>
    /// <returns>A filter options string for the LOK saveAs call.</returns>
    public string ToFilterOptions()
    {
        Validate();

        var parts = new List<string>();

        // Compression
        if (Compression != null)
        {
            if (Compression.UseLosslessCompression.HasValue)
                parts.Add($"UseLosslessCompression={BoolStr(Compression.UseLosslessCompression.Value)}");
            if (Compression.Quality.HasValue)
                parts.Add($"Quality={Compression.Quality.Value}");
            if (Compression.ReduceImageResolution.HasValue)
                parts.Add($"ReduceImageResolution={BoolStr(Compression.ReduceImageResolution.Value)}");
            if (Compression.MaxImageResolution.HasValue)
                parts.Add($"MaxImageResolution={Compression.MaxImageResolution.Value}");
        }
        else
        {
            parts.Add($"UseLosslessCompression={BoolStr(UseLosslessCompression)}");
            parts.Add($"Quality={Quality}");
            parts.Add($"ReduceImageResolution={BoolStr(ReduceImageResolution)}");
            parts.Add($"MaxImageResolution={MaxImageResolutionDpi}");
        }

        // Content
        parts.Add($"ExportBookmarks={BoolStr(ExportBookmarks)}");
        if (ExportNotes) parts.Add("ExportNotes=true");
        if (UseTaggedPdf) parts.Add("UseTaggedPDF=true");
        if (!ExportFormFields) parts.Add("ExportFormFields=false");
        if (SinglePageSheets) parts.Add("SinglePageSheets=true");

        // PDF version/compliance
        if (PdfVersion.HasValue)
        {
            if (PdfVersion.Value == Enums.PdfVersion.PdfUa1)
            {
                parts.Add("SelectPdfVersion=0");
                parts.Add("PDFUACompliance=true");
                parts.Add("UseTaggedPDF=true");
            }
            else
            {
                parts.Add($"SelectPdfVersion={(int)PdfVersion.Value}");
            }
        }
        else if (PdfACompliance != PdfACompliance.None)
        {
            parts.Add($"SelectPdfVersion={(int)PdfACompliance}");
        }

        // Security
        if (Security != null)
        {
            if (Security.EncryptFile)
            {
                parts.Add("EncryptFile=true");
                if (Security.DocumentOpenPassword != null)
                    parts.Add($"DocumentOpenPassword={Security.DocumentOpenPassword}");
            }

            if (Security.RestrictPermissions)
            {
                parts.Add("RestrictPermissions=true");
                if (Security.PermissionPassword != null)
                    parts.Add($"PermissionPassword={Security.PermissionPassword}");
            }

            if (Security.Printing.HasValue)
                parts.Add($"Printing={(int)Security.Printing.Value}");
            if (Security.Changes.HasValue)
                parts.Add($"Changes={(int)Security.Changes.Value}");
            if (Security.EnableCopyingOfContent.HasValue)
                parts.Add($"EnableCopyingOfContent={BoolStr(Security.EnableCopyingOfContent.Value)}");
            if (Security.EnableTextAccessForAccessibilityTools.HasValue)
                parts.Add($"EnableTextAccessForAccessibilityTools={BoolStr(Security.EnableTextAccessForAccessibilityTools.Value)}");
        }
        else if (!string.IsNullOrEmpty(EncryptionPassword))
        {
            parts.Add("EncryptFile=true");
            parts.Add($"DocumentOpenPassword={EncryptionPassword}");
        }

        // Watermark
        if (!string.IsNullOrEmpty(Watermark))
            parts.Add($"Watermark={Watermark}");

        // Page range
        if (!string.IsNullOrEmpty(PageRange))
            parts.Add($"PageRange={PageRange}");

        // Initial view
        if (InitialView != InitialView.Default)
            parts.Add($"InitialView={(int)InitialView}");

        return string.Join(",", parts);
    }
    #endregion

    #region BoolStr
    /// <summary>
    ///     Converts a boolean to a lowercase string for filter options.
    /// </summary>
    private static string BoolStr(bool value)
    {
        return value ? "true" : "false";
    }
    #endregion
}