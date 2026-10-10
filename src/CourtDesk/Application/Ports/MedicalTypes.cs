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

        public static string PhotosRoot(string home)
        {
            return Path.Combine(home, "photos");
        }

        public static string StorePhoto(string home, string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                throw new FileNotFoundException("file not found", sourcePath);
            }
            string ext = IsAllowedImageExt(sourcePath);
            if (string.IsNullOrEmpty(ext))
            {
                throw new InvalidOperationException("unsupported image type: " + Path.GetExtension(sourcePath));
            }
            if (new FileInfo(sourcePath).Length <= 0)
            {
                throw new InvalidOperationException("empty file");
            }
            string hash = Sha256(sourcePath);
            string stored = "photos/" + hash.Substring(0, 16) + ext;
            string abs = Absolute(home, stored);
            Directory.CreateDirectory(Path.GetDirectoryName(abs));
            if (!File.Exists(abs))
            {
                using (FileStream src = File.OpenRead(sourcePath))
                using (FileStream dst = File.Create(abs))
                {
                    src.CopyTo(dst);
                }
            }
            return stored;
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