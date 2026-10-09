using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace CourtDesk.Application
{
    public sealed class XlsxColumn
    {
        public string Header;
        public double Width;
    }

    public static class Xlsx
    {
        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace DocRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace ContentTypes =
            "http://schemas.openxmlformats.org/package/2006/content-types";

        public static void Write(string path, string sheetName, List<XlsxColumn> columns,
            List<List<string>> rows, string headerFill, string headerText,
            string bandFill, string bandText, int repeatHeaderRows)
        {
            var sheet = new XElement(Main + "worksheet",
                new XElement(Main + "sheetViews",
                    new XElement(Main + "sheetView",
                        new XAttribute("rightToLeft", "1"),
                        new XAttribute("workbookViewId", "0"),
                        new XElement(Main + "pane",
                            new XAttribute("ySplit", "1"),
                            new XAttribute("topLeftCell", "A2"),
                            new XAttribute("activePane", "bottomLeft"),
                            new XAttribute("state", "frozen")))),
                new XElement(Main + "sheetFormatPr", new XAttribute("defaultRowHeight", "18")));

            if (columns != null && columns.Count > 0)
            {
                var cols = new XElement(Main + "cols");
                for (int i = 0; i < columns.Count; i++)
                {
                    var c = new XElement(Main + "col",
                        new XAttribute("min", i + 1),
                        new XAttribute("max", i + 1),
                        new XAttribute("width",
                            (columns[i].Width <= 0 ? 18 : columns[i].Width)
                                .ToString("0.##", CultureInfo.InvariantCulture)),
                        new XAttribute("customWidth", "1"));
                    cols.Add(c);
                }
                sheet.Add(cols);
            }

            var data = new XElement(Main + "sheetData");
            var head = new XElement(Main + "row", new XAttribute("r", "1"),
                new XAttribute("ht", "30"), new XAttribute("customHeight", "1"));
            for (int i = 0; i < columns.Count; i++)
            {
                head.Add(Cell(CellRef(i, 1), columns[i].Header, 1));
            }
            data.Add(head);

            int r = 1;
            for (int ri = 0; ri < rows.Count; ri++)
            {
                r++;
                bool band = (r - 1) % 2 == 0;
                var row = new XElement(Main + "row", new XAttribute("r", r));
                List<string> src = rows[ri];
                for (int ci = 0; ci < columns.Count; ci++)
                {
                    string v = ci < src.Count ? src[ci] : "";
                    if (string.IsNullOrEmpty(v))
                    {
                        continue;
                    }
                    row.Add(Cell(CellRef(ci, r), v, band ? 3 : 2));
                }
                data.Add(row);
            }
            sheet.Add(data);

            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                AddText(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>"
                    + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                    + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                    + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                    + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                    + "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
                    + "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"
                    + "</Types>");
                AddText(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>"
                    + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                    + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
                    + "</Relationships>");
                AddText(zip, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>"
                    + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\""
                    + " xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
                    + "<sheets><sheet name=\"" + Esc(sheetName) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets>"
                    + "</workbook>");
                AddText(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>"
                    + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                    + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>"
                    + "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>"
                    + "</Relationships>");
                AddText(zip, "xl/styles.xml", Styles(headerFill, headerText, bandFill, bandText));
                AddText(zip, "xl/worksheets/sheet1.xml", sheet.ToString(SaveOptions.DisableFormatting));
            }
        }

        private static string Styles(string headerFill, string headerText,
            string bandFill, string bandText)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>");
            sb.Append("<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"General\"/></numFmts>");
            sb.Append("<fonts count=\"3\">");
            sb.Append("<font><sz val=\"11\"/><name val=\"Calibri\"/></font>");
            sb.Append("<font><b/><sz val=\"12\"/><color rgb=\"FF" + headerText
                + "\"/><name val=\"Calibri\"/></font>");
            sb.Append("<font><b/><sz val=\"11\"/><color rgb=\"FF" + bandText
                + "\"/><name val=\"Calibri\"/></font>");
            sb.Append("</fonts>");
            sb.Append("<fills count=\"4\">");
            sb.Append("<fill><patternFill patternType=\"none\"/></fill>");
            sb.Append("<fill><patternFill patternType=\"gray125\"/></fill>");
            sb.Append("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF" + headerFill
                + "\"/><bgColor indexed=\"64\"/></patternFill></fill>");
            sb.Append("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF" + bandFill
                + "\"/><bgColor indexed=\"64\"/></patternFill></fill>");
            sb.Append("</fills>");
            sb.Append("<borders count=\"2\">");
            sb.Append("<border><left/><right/><top/><bottom/><diagonal/></border>");
            sb.Append("<border>"
                + "<left style=\"thin\"><color rgb=\"FFBFC7D1\"/></left>"
                + "<right style=\"thin\"><color rgb=\"FFBFC7D1\"/></right>"
                + "<top style=\"thin\"><color rgb=\"FFBFC7D1\"/></top>"
                + "<bottom style=\"thin\"><color rgb=\"FFBFC7D1\"/></bottom>"
                + "<diagonal/></border>");
            sb.Append("</borders>");
            sb.Append("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
            sb.Append("<cellXfs count=\"4\">");
            sb.Append("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>");
            sb.Append("<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\""
                + " applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\">"
                + "<alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>");
            sb.Append("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\""
                + " applyAlignment=\"1\">"
                + "<alignment horizontal=\"right\" vertical=\"center\" wrapText=\"1\"/></xf>");
            sb.Append("<xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFont=\"1\""
                + " applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\">"
                + "<alignment horizontal=\"right\" vertical=\"center\" wrapText=\"1\"/></xf>");
            sb.Append("</cellXfs>");
            sb.Append("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
            sb.Append("</styleSheet>");
            return sb.ToString();
        }

        private static XElement Cell(string reference, string value, int style)
        {
            return new XElement(Main + "c",
                new XAttribute("r", reference),
                new XAttribute("s", style),
                new XAttribute("t", "inlineStr"),
                new XElement(Main + "is",
                    new XElement(Main + "t",
                        new XAttribute(XNamespace.Xml + "space", "preserve"),
                        value == null ? "" : value)));
        }

        private static string CellRef(int col, int row)
        {
            string letters = "";
            int n = col;
            while (n >= 0)
            {
                letters = (char)('A' + (n % 26)) + letters;
                n = (n / 26) - 1;
            }
            return letters + row.ToString(CultureInfo.InvariantCulture);
        }

        private static void AddText(ZipArchive zip, string name, string content)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name);
            using (Stream s = entry.Open())
            using (var w = new StreamWriter(s, new UTF8Encoding(false)))
            {
                w.Write(content);
            }
        }

        private static string Esc(string s)
        {
            return (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        public static List<List<string>> Read(string path)
        {
            var result = new List<List<string>>();
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                var shared = new List<string>();
                ZipArchiveEntry ss = zip.GetEntry("xl/sharedStrings.xml");
                if (ss != null)
                {
                    XDocument doc = Load(ss);
                    foreach (XElement si in doc.Root.Elements(Main + "si"))
                    {
                        var parts = new List<string>();
                        foreach (XElement t in si.Descendants(Main + "t"))
                        {
                            parts.Add(t.Value);
                        }
                        shared.Add(string.Concat(parts.ToArray()));
                    }
                }
                ZipArchiveEntry sheet = zip.GetEntry("xl/worksheets/sheet1.xml");
                if (sheet == null)
                {
                    return result;
                }
                XDocument sd = Load(sheet);
                foreach (XElement row in sd.Root.Element(Main + "sheetData")
                    .Elements(Main + "row"))
                {
                    var cells = new List<string>();
                    int next = 0;
                    foreach (XElement c in row.Elements(Main + "c"))
                    {
                        string reference = (string)c.Attribute("r");
                        int col = ColOf(reference);
                        while (cells.Count < col)
                        {
                            cells.Add("");
                        }
                        string type = (string)c.Attribute("t");
                        string value;
                        if (type == "s")
                        {
                            string v = Value(c);
                            int index = 0;
                            int.TryParse(v, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out index);
                            value = index >= 0 && index < shared.Count ? shared[index] : "";
                        }
                        else if (type == "inlineStr")
                        {
                            var parts = new List<string>();
                            foreach (XElement t in c.Descendants(Main + "t"))
                            {
                                parts.Add(t.Value);
                            }
                            value = string.Concat(parts.ToArray());
                        }
                        else
                        {
                            value = Value(c);
                        }
                        cells.Add(value);
                        next++;
                    }
                    result.Add(cells);
                }
            }
            return result;
        }

        private static string Value(XElement c)
        {
            XElement v = c.Element(Main + "v");
            return v == null ? "" : v.Value;
        }

        private static int ColOf(string reference)
        {
            if (string.IsNullOrEmpty(reference))
            {
                return 0;
            }
            int col = 0;
            int i = 0;
            while (i < reference.Length && char.IsLetter(reference[i]))
            {
                col = (col * 26) + (char.ToUpperInvariant(reference[i]) - 'A' + 1);
                i++;
            }
            return Math.Max(0, col - 1);
        }

        private static XDocument Load(ZipArchiveEntry entry)
        {
            using (Stream s = entry.Open())
            {
                return XDocument.Load(s);
            }
        }
    }
}