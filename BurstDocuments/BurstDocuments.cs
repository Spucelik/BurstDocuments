using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Utils;
using iText.Layout;
using System.Text;
using Microsoft.VisualBasic;
using iText.IO.Source;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using iText.Kernel.Events;
using Org.BouncyCastle.Crypto.Paddings;
using System.IO.Compression;
using System.Collections.Generic;

namespace BurstDocuments
{
    public static class BurstDocument
    {
        [FunctionName("BurstDocument")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            var formdata = await req.ReadFormAsync();
            var file = req.Form.Files["file"];

            PdfReader pdfReader = new PdfReader(file.OpenReadStream());
            PdfDocument pdfDoc = new PdfDocument(pdfReader);


            byte[] newFileOutput = null;
            byte[] zipOutput = null;
            string fileNameZip = "DocumentExport_" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".zip";

            List<SourceFile> sourceFiles = new List<SourceFile>();


            int iStartPage = 1;
            int iEndPage = 1;
            Boolean bCreateFile = false;

            for (int page = 1; page <= pdfDoc.GetNumberOfPages(); page++)
            {
                Console.WriteLine(page.ToString());
                int pageSize = pdfDoc.GetPage(page).GetContentBytes().Length;

                if (pageSize < 300)
                {   
                    bCreateFile = true;
                    iEndPage--;
                }

                if (page == pdfDoc.GetNumberOfPages())
                {
                    bCreateFile = true;
                    iEndPage = pdfDoc.GetNumberOfPages();
                }

                if (bCreateFile)
                {
                    newFileOutput = CreateFile(pdfDoc, iStartPage, iEndPage);

                   string fileName = "Document_" + page;
                   
                    sourceFiles.Add ( new SourceFile { Name = fileName, Extension = ".pdf", FileBytes = newFileOutput });

                    newFileOutput = null;

                    iStartPage = page + 1;
                    iEndPage++;
                    bCreateFile = false;

                }
                else
                {
                    iEndPage++;
                    bCreateFile = false;
                }
            }

            // the output bytes of the zip
            byte[] fileBytes = null;

            // create a working memory stream
            using (System.IO.MemoryStream memoryStream = new System.IO.MemoryStream())
            {
                // create a zip
                using (System.IO.Compression.ZipArchive zip = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    // interate through the source files
                    foreach (SourceFile f in sourceFiles)
                    {
                        // add the item name to the zip
                        System.IO.Compression.ZipArchiveEntry zipItem = zip.CreateEntry(f.Name + "." + f.Extension);
                        // add the item bytes to the zip entry by opening the original file and copying the bytes
                        using (System.IO.MemoryStream originalFileMemoryStream = new System.IO.MemoryStream(f.FileBytes))
                        {
                            using (System.IO.Stream entryStream = zipItem.Open())
                            {
                                originalFileMemoryStream.CopyTo(entryStream);
                            }
                        }
                    }
                }
                fileBytes = memoryStream.ToArray();
            }
           
            return new FileContentResult(fileBytes, "application/zip")
            {
                FileDownloadName = fileNameZip
            };

            static byte[] CreateFile(PdfDocument pdfDoc,int startPage, int endPage)
            {
                byte[] fileOutput;

                var inputDocument = pdfDoc;

                using (var outputStream = new MemoryStream())
                {
                    using (var pdfWriter = new PdfWriter(outputStream))
                    {

                        using (var outputDocument = new PdfDocument(pdfWriter))
                        {
                            inputDocument.CopyPagesTo(startPage, endPage, outputDocument);
                        }
                    }
                    fileOutput = outputStream.ToArray();
                }

                return fileOutput;
            }
        }
    }

    class ImprovedSplitter : PdfSplitter
    {
        private Func<PageRange, PdfWriter> nextWriter;
        public ImprovedSplitter(PdfDocument pdfDocument, Func<PageRange, PdfWriter> nextWriter) : base(pdfDocument)
        {
            this.nextWriter = nextWriter;
        }

        protected override PdfWriter GetNextPdfWriter(PageRange documentPageRange)
        {
            return nextWriter.Invoke(documentPageRange);
        }
    }
    public class SourceFile
    {
        public string Name { get; set; }
        public string Extension { get; set; }
        public Byte[] FileBytes { get; set; }
    }
}
