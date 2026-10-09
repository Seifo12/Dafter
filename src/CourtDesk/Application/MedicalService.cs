using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;

namespace CourtDesk.Application
{
    public sealed class MedicalImage
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

    public sealed class MedicalService
    {
        private readonly IMedicalStore _store;
        private readonly string _home;
        private readonly Func<DateTime> _utcNow;

        public MedicalService(IMedicalStore store, string home, Func<DateTime> utcNow)
        {
            _store = store;
            _home = home;
            _utcNow = utcNow;
        }

        public List<MedicalNote> Notes(int defendantId)
        {
            return _store.ListNotes(null, defendantId);
        }

        public List<MedicalImage> Images(int defendantId)
        {
            var list = _store.ListImages(null, defendantId);
            foreach (MedicalImage img in list)
            {
                img.BindHome(_home);
            }
            return list;
        }

        public bool HasMedical(int defendantId)
        {
            return _store.HasMedical(defendantId);
        }

        public long SaveNoteText(int defendantId, string text)
        {
            return _store.AddNote(null, defendantId, text ?? "", "");
        }

        public MedicalImage AddImage(int defendantId, string sourcePath, long? noteId, string caption)
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("file not found", sourcePath);
            }
            if (!MedicalFiles.IsAllowedImage(sourcePath))
            {
                throw new InvalidOperationException("unsupported image type: " + Path.GetExtension(sourcePath));
            }
            long byteSize = new FileInfo(sourcePath).Length;
            if (byteSize <= 0)
            {
                throw new InvalidOperationException("empty file");
            }

            string hash = MedicalFiles.Sha256(sourcePath);
            string dir = DbPaths.DefendantMedicalDir(_home, defendantId);
            Directory.CreateDirectory(dir);

            string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            string stored = "medical/images/" + defendantId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "/" + hash.Substring(0, 16) + ext;
            string abs = MedicalFiles.Absolute(_home, stored);
            Directory.CreateDirectory(Path.GetDirectoryName(abs));

            if (!File.Exists(abs))
            {
                File.Copy(sourcePath, abs, false);
            }

            return _store.AddImage(null, defendantId, noteId,
                Path.GetFileName(sourcePath), stored,
                MedicalFiles.ContentTypeFor(sourcePath), byteSize, caption ?? "");
        }

        public void DeleteImage(long imageId)
        {
            string rel;
            if (_store.DeleteImage(null, imageId, out rel))
            {
                string abs = MedicalFiles.Absolute(_home, rel);
                if (!string.IsNullOrEmpty(abs) && File.Exists(abs))
                {
                    try { File.Delete(abs); } catch (IOException) { }
                }
            }
        }
    }
}