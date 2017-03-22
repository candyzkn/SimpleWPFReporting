﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using Microsoft.Win32;
using PdfSharp.Xps;

namespace SimpleWPFReporting
{
    public static class Report
    {
        const int DIUPerInch = 96;
        public static void ExportVisualAsXps(Visual visual)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                DefaultExt = ".xps",
                Filter = "XPS Documents (.xps)|*.xps"
            };

            bool? result = saveFileDialog.ShowDialog();

            if (result != true) return;

            XpsDocument xpsDocument = new XpsDocument(saveFileDialog.FileName, FileAccess.Write);
            XpsDocumentWriter xpsDocumentWriter = XpsDocument.CreateXpsDocumentWriter(xpsDocument);

            xpsDocumentWriter.Write(visual);
            xpsDocument.Close();
        }

        public static void ExportVisualAsPdf(Visual visual)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                DefaultExt = ".pdf",
                Filter = "PDF Documents (.pdf)|*.pdf"
            };

            bool? result = saveFileDialog.ShowDialog();

            if (result != true) return;

            using (MemoryStream memoryStream = new MemoryStream())
            {
                System.IO.Packaging.Package package = System.IO.Packaging.Package.Open(memoryStream, FileMode.Create);
                XpsDocument xpsDocument = new XpsDocument(package);
                XpsDocumentWriter xpsDocumentWriter = XpsDocument.CreateXpsDocumentWriter(xpsDocument);

                xpsDocumentWriter.Write(visual);
                xpsDocument.Close();
                package.Close();

                var pdfXpsDoc = PdfSharp.Xps.XpsModel.XpsDocument.Open(memoryStream);
                XpsConverter.Convert(pdfXpsDoc, saveFileDialog.FileName, 0);
            }
        }

        private static readonly Lazy<int> dpiX = new Lazy<int>(() =>
        {
            PropertyInfo dpiXProperty = typeof(SystemParameters).GetProperty("DpiX", BindingFlags.NonPublic | BindingFlags.Static);
            return (int)dpiXProperty.GetValue(null, null);
        });

        private static readonly Lazy<int> dpiY = new Lazy<int>(() =>
        {
            var dpiYProperty = typeof(SystemParameters).GetProperty("Dpi", BindingFlags.NonPublic | BindingFlags.Static);
            return (int)dpiYProperty.GetValue(null, null);
        });

        private static void SaveFrameworkElementAsImage(FrameworkElement element, string filePath, BitmapEncoder bitmapEncoder)
        {
            RenderTargetBitmap bitmap = new RenderTargetBitmap(
                pixelWidth: Convert.ToInt32((element.ActualWidth / DIUPerInch) * dpiX.Value), 
                pixelHeight: Convert.ToInt32((element.ActualHeight / DIUPerInch) * dpiY.Value), 
                dpiX: dpiX.Value, 
                dpiY: dpiY.Value, 
                pixelFormat: PixelFormats.Pbgra32);
            bitmap.Render(element);

            bitmapEncoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (Stream fs = File.Create(filePath))
            {
                bitmapEncoder.Save(fs);
            }
        }

        public static void ExportFrameworkElementAsJpg(FrameworkElement element)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                DefaultExt = ".jpg",
                Filter = "JPG Images (.jpg)|*.jpg"
            };

            bool? result = saveFileDialog.ShowDialog();

            if (result != true) return;

            SaveFrameworkElementAsImage(element, saveFileDialog.FileName, new JpegBitmapEncoder {QualityLevel = 100});
        }

        public static void ExportFrameworkElementAsBmp(FrameworkElement element)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                DefaultExt = ".bmp",
                Filter = "BMP Images (.bmp)|*.bmp"
            };

            bool? result = saveFileDialog.ShowDialog();

            if (result != true) return;

            SaveFrameworkElementAsImage(element, saveFileDialog.FileName, new BmpBitmapEncoder());
        }

        public static void ExportFrameworkElementAsPng(FrameworkElement element)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                DefaultExt = ".png",
                Filter = "PNG Images (.png)|*.png"
            };

            bool? result = saveFileDialog.ShowDialog();

            if (result != true) return;

            SaveFrameworkElementAsImage(element, saveFileDialog.FileName, new PngBitmapEncoder());
        }

        /// <summary>
        /// Divides elements of reportContainer into pages and prints them
        /// </summary>
        /// <param name="reportContainer">StackPanel containing report elements</param>
        /// <param name="dataContext">Data Context used in the report</param>
        /// <param name="margin">Margin of a report page</param>
        /// <param name="orientation">Landscape or Portrait orientation</param>
        /// <param name="reportHeaderDataTemplate">Optional header for each page</param>
        /// <param name="reportFooterDataTemplate">Optional footer for each page</param>
        public static void PrintReport(
            StackPanel reportContainer, 
            object dataContext, 
            Thickness margin, 
            ReportOrientation orientation, 
            DataTemplate reportHeaderDataTemplate = null, 
            DataTemplate reportFooterDataTemplate = null)
        {
            PrintDialog printDialog = new PrintDialog();

            bool? result = printDialog.ShowDialog();

            if (result != true) return;

            Size reportSize = GetReportSize(reportContainer, margin, orientation, printDialog);

            List<FrameworkElement> ReportElements = new List<FrameworkElement>(reportContainer.Children.Cast<FrameworkElement>());
            reportContainer.Children.Clear(); //to avoid exception "Specified element is already the logical child of another element."

            List<ReportPage> ReportPages = GetReportPages(reportContainer, ReportElements, dataContext, margin, reportSize, reportHeaderDataTemplate, reportFooterDataTemplate);

            try
            {
                ReportPages.ForEach((reportPage, index) => printDialog.PrintVisual(reportPage.LayoutRoot, $"Карточка Точки {index + 1}"));
            }
            finally
            {
                ReportPages.ForEach(reportPage => reportPage.ClearChildren());
                ReportElements.ForEach(elm => reportContainer.Children.Add(elm));
                reportContainer.UpdateLayout();
            }
        }

        /// <summary>
        /// Divides elements of reportContainer into pages and exports them as PDF
        /// </summary>
        /// <param name="reportContainer">StackPanel containing report elements</param>
        /// <param name="dataContext">Data Context used in the report</param> 
        /// <param name="margin">Margin of a report page</param>
        /// <param name="orientation">Landscape or Portrait orientation</param>
        /// <param name="reportHeaderDataTemplate">Optional header for each page</param>
        /// <param name="reportFooterDataTemplate">Optional footer for each page</param> 
        public static void ExportReportAsPdf(
            StackPanel reportContainer, 
            object dataContext, 
            Thickness margin, 
            ReportOrientation orientation, 
            DataTemplate reportHeaderDataTemplate = null, 
            DataTemplate reportFooterDataTemplate = null)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                DefaultExt = ".pdf",
                Filter = "PDF Documents (.pdf)|*.pdf"
            };

            bool? result = saveFileDialog.ShowDialog();

            if (result != true) return;

            Size reportSize = GetReportSize(reportContainer, margin, orientation);

            List<FrameworkElement> ReportElements = new List<FrameworkElement>(reportContainer.Children.Cast<FrameworkElement>());
            reportContainer.Children.Clear(); //to avoid exception "Specified element is already the logical child of another element."

            List<ReportPage> ReportPages = GetReportPages(reportContainer, ReportElements, dataContext, margin, reportSize, reportHeaderDataTemplate, reportFooterDataTemplate);

            FixedDocument fixedDocument = new FixedDocument();

            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    System.IO.Packaging.Package package = System.IO.Packaging.Package.Open(memoryStream, FileMode.Create);
                    XpsDocument xpsDocument = new XpsDocument(package);
                    XpsDocumentWriter xpsDocumentWriter = XpsDocument.CreateXpsDocumentWriter(xpsDocument);

                    foreach (Grid reportPage in ReportPages.Select(reportPage => reportPage.LayoutRoot))
                    {
                        reportPage.Width = reportPage.ActualWidth;
                        reportPage.Height = reportPage.ActualHeight;

                        FixedPage newFixedPage = new FixedPage();
                        newFixedPage.Children.Add(reportPage);
                        newFixedPage.Measure(reportSize);
                        newFixedPage.Arrange(new Rect(reportSize));
                        newFixedPage.Width = newFixedPage.ActualWidth;
                        newFixedPage.Height = newFixedPage.ActualHeight;
                        newFixedPage.UpdateLayout();

                        PageContent pageContent = new PageContent();
                        ((IAddChild)pageContent).AddChild(newFixedPage);

                        fixedDocument.Pages.Add(pageContent);
                    }

                    xpsDocumentWriter.Write(fixedDocument);
                    xpsDocument.Close();
                    package.Close();

                    var pdfXpsDoc = PdfSharp.Xps.XpsModel.XpsDocument.Open(memoryStream);
                    XpsConverter.Convert(pdfXpsDoc, saveFileDialog.FileName, 0);
                }
            }
            finally
            {
                ReportPages.ForEach(reportPage => reportPage.ClearChildren());
                ReportElements.ForEach(elm => reportContainer.Children.Add(elm));
                reportContainer.UpdateLayout();
            }
        }

        private static List<ReportPage> GetReportPages(
            StackPanel reportContainer, 
            List<FrameworkElement> ReportElements, 
            object dataContext, 
            Thickness margin, 
            Size reportSize, 
            DataTemplate reportHeaderDataTemplate, 
            DataTemplate reportFooterDataTemplate)
        {
            int pageNumber = 1;

            List<ReportPage> ReportPages = 
                new List<ReportPage>
                {
                    new ReportPage(reportSize, reportContainer, margin, dataContext, reportHeaderDataTemplate, reportFooterDataTemplate, pageNumber)
                };

            foreach (FrameworkElement reportVisualElement in ReportElements)
            {
                if (ReportPages.Last().GetChildrenActualHeight() + GetActualHeightPlusMargin(reportVisualElement) > reportSize.Height - margin.Top - margin.Bottom)
                {
                    pageNumber++;

                    ReportPages.Add(new ReportPage(reportSize, reportContainer, margin, dataContext, reportHeaderDataTemplate, reportFooterDataTemplate, pageNumber));
                }

                ReportPages.Last().AddElement(reportVisualElement);
            }

            foreach (ReportPage reportPage in ReportPages)
            {
                reportPage.LayoutRoot.Measure(reportSize);
                reportPage.LayoutRoot.Arrange(new Rect(reportSize));
                reportPage.LayoutRoot.UpdateLayout();
            }

            return ReportPages;
        }

        private static double GetActualHeightPlusMargin(FrameworkElement elm)
        {
            return elm.ActualHeight + elm.Margin.Top + elm.Margin.Bottom;
        }

        private static Size GetReportSize(StackPanel reportContainer, Thickness margin, ReportOrientation orientation, PrintDialog printDialog = null)
        {
            if (printDialog == null)
                printDialog = new PrintDialog();

            double reportWidth = reportContainer.ActualWidth + margin.Left + margin.Right;

            double reportHeight;
            if (orientation == ReportOrientation.Portrait)
                reportHeight = (reportWidth / printDialog.PrintableAreaWidth) * printDialog.PrintableAreaHeight;
            else
                reportHeight = (reportWidth / printDialog.PrintableAreaHeight) * printDialog.PrintableAreaWidth;

            return new Size(reportWidth, reportHeight);
        }

        private static void ForEach<T>(this IEnumerable<T> enumeration, Action<T, int> action)
        {
            if (enumeration == null)
                return;

            int index = 0;
            foreach (T item in enumeration)
            {
                action(item, index);
                index++;
            }
        }
    }
}

