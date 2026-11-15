# Approved document extraction dependencies

The user explicitly approved adding these three libraries, necessary transitive dependencies and English/Arabic trained data. No proprietary parser, OCR service, new frontend framework or scanner integration was added.

| Dependency | Purpose | License/source |
| --- | --- | --- |
| PdfPig 0.1.16 | Native PDF text | Apache-2.0; https://github.com/UglyToad/PdfPig/blob/master/LICENSE |
| PDFtoImage 5.4.0 | PDF rasterization wrapper | MIT; https://github.com/sungaila/PDFtoImage/blob/master/LICENSE |
| TesseractOCR 5.5.2 | .NET OCR wrapper/native Tesseract and Leptonica assets | Apache-2.0 wrapper; https://github.com/Sicos1977/TesseractOCR |
| PDFium native assets | PDF rendering | BSD-style and included third-party notices; https://pdfium.googlesource.com/pdfium/+/refs/heads/main/LICENSE |
| SkiaSharp/native Skia | Bounded image decoding/encoding | MIT/underlying Skia BSD-style notices; https://github.com/mono/SkiaSharp/blob/main/LICENSE.md |
| Tesseract engine | OCR | Apache-2.0; https://github.com/tesseract-ocr/tesseract/blob/main/LICENSE |
| Leptonica | Image processing used by Tesseract | BSD-style; https://github.com/DanBloomberg/leptonica |
| tessdata_fast eng/ara | Local language models | Apache-2.0; https://github.com/tesseract-ocr/tessdata_fast/blob/main/LICENSE |

Language assets are pinned to official tessdata_fast commit 87416418657359cb625c412a48b6e1d6d41c29bd:

- eng.traineddata SHA-256: 7D4322BD2A7749724879683FC3912CB542F19906C83BCC1A52132556427170B2
- ara.traineddata SHA-256: E3206D3DC87FD50C24A0FB9F01838615911D25168F4E64415244B67D2BB3E729

The installer fetches the immutable official raw URLs and verifies these digests. Trained-data binaries are kept in ignored App_Data, not copied to the source repository. Native binaries are supplied by NuGet restore/build, not committed. Preserve the applicable licenses, copyrights and third-party notices if distributing a published application or those binaries separately; this inventory does not replace their license texts.
