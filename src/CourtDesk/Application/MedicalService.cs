using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Application
{
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
            string dir = MedicalFiles.DefendantImageDir(_home, defendantId);
            Directory.CreateDirectory(dir);

            string ext = MedicalFiles.IsAllowedImageExt(sourcePath);
            string stored = "medical/images/" + defendantId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "/" + hash.Substring(0, 16) + ext;
            string abs = MedicalFiles.Absolute(_home, stored);
            Directory.CreateDirectory(Path.GetDirectoryName(abs));

            if (!File.Exists(abs))
            {
                using (FileStream src = File.OpenRead(sourcePath))
                using (FileStream dst = File.Create(abs))
                {
                    src.CopyTo(dst);
                }
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