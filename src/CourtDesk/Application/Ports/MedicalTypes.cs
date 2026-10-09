using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace CourtDesk.Application.Ports
{public sealed class MedicalImage
    {
        private string _home;

        public long Id { get; set; }
        public string FileName { get; set; }
        public string RelativePath { get; set; }
        public string ContentType { get; set; }
        public long ByteSize { get; set; }
        public string Caption { get; set; }

        public string AbsolutePath
        {
            get { return MedicalFiles.Absolute(_home, RelativePath); }
        }

        internal void BindHome(string home)
        {
            _home = home;
        }
    }

    public static class MedicalFiles
    {
        private static readonly string[] Allowed = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp" };

        public static string MedicalFilesRoot(string home)
        {
            return Path.Combine(home, "medical", "images");
        }

        public static string DefendantImageDir(string home, long defendantId)
        {
            return Path.Combine(MedicalFilesRoot(home),
                defendantId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public static string MedicalDir(string home)
        {
            return Path.Combine(home, "medical");
        }

        public static string IsAllowedImageExt(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return Array.IndexOf(Allowed, ext) >= 0 ? ext : "";
        }

        public static bool IsAllowedImage(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return Array.IndexOf(Allowed, ext) >= 0;
        }

        public static string Absolute(string home, string relative)
        {
            if (string.IsNullOrEmpty(relative))
            {
                return null;
            }
            return Path.Combine(home, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        public static string ContentTypeFor(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                case ".bmp": return "image/bmp";
                case ".gif": return "image/gif";
                case ".tif":
                case ".tiff": return "image/tiff";
                case ".webp": return "image/webp";
                default: return "application/octet-stream";
            }
        }

        public static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    public sealed class MedicalNote
    {
        public long Id { get; set; }
        public int DefendantId { get; set; }
        public string Text { get; set; }
        public string RecordedAtUtc { get; set; }
        public string RecordedBy { get; set; }
    }

    public interface IMedicalStore
    {
        List<MedicalNote> ListNotes(ISession s, int defendantId);
        long AddNote(ISession s, int defendantId, string text, string recordedBy);
        bool UpdateNoteText(ISession s, long noteId, string text);
        List<MedicalImage> ListImages(ISession s, int defendantId);
        MedicalImage AddImage(ISession s, int defendantId, long? noteId, string fileName,
            string relativePath, string contentType, long byteSize, string caption);
        bool DeleteImage(ISession s, long imageId, out string relativePath);
        bool HasMedical(int defendantId);
    }

    }