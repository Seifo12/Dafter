using System;
using System.Collections.Generic;
using System.Data.SQLite;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Persistence;

namespace CourtDesk.Persistence
{
    public sealed class SqliteMedicalStore : IMedicalStore
    {
        private readonly DbGateway _db;

        public SqliteMedicalStore(DbGateway db)
        {
            _db = db;
        }

        private static string N(string s)
        {
            return s == null ? "" : s;
        }

        private static string NowIso(DbContext c)
        {
            return c.UtcNowIso;
        }

        List<MedicalNote> IMedicalStore.ListNotes(ISession s, int defendantId)
        {
            var outp = new List<MedicalNote>();
            _db.Read(delegate(ISession ses)
            {
                var c = (DbContext)ses;
                using (var cmd = new SQLiteCommand(
                    "SELECT id, defendant_id, note_text, recorded_at_utc, recorded_by " +
                    "FROM medical_notes WHERE defendant_id = @d ORDER BY id DESC;", c.Connection))
                {
                    cmd.Parameters.AddWithValue("@d", defendantId);
                    using (SQLiteDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            outp.Add(new MedicalNote
                            {
                                Id = r.GetInt64(0),
                                DefendantId = r.GetInt32(1),
                                Text = r.GetString(2),
                                RecordedAtUtc = r.GetString(3),
                                RecordedBy = r.GetString(4)
                            });
                        }
                    }
                }
                return 0;
            });
            return outp;
        }

        long IMedicalStore.AddNote(ISession s, int defendantId, string text, string recordedBy)
        {
            long id = 0;
            _db.Write(delegate(ISession ses)
            {
                var c = (DbContext)ses;
                using (var cmd = new SQLiteCommand(
                    "INSERT INTO medical_notes (defendant_id, note_text, recorded_at_utc, recorded_by) " +
                    "VALUES (@d, @t, @at, @by); SELECT last_insert_rowid();", c.Connection))
                {
                    cmd.Parameters.AddWithValue("@d", defendantId);
                    cmd.Parameters.AddWithValue("@t", N(text));
                    cmd.Parameters.AddWithValue("@at", NowIso(c));
                    cmd.Parameters.AddWithValue("@by", N(recordedBy));
                    id = Convert.ToInt64(cmd.ExecuteScalar());
                }
                return 0;
            });
            return id;
        }

        bool IMedicalStore.UpdateNoteText(ISession s, long noteId, string text)
        {
            int n = 0;
            _db.Write(delegate(ISession ses)
            {
                var c = (DbContext)ses;
                using (var cmd = new SQLiteCommand(
                    "UPDATE medical_notes SET note_text = @t WHERE id = @i;", c.Connection))
                {
                    cmd.Parameters.AddWithValue("@t", N(text));
                    cmd.Parameters.AddWithValue("@i", noteId);
                    n = cmd.ExecuteNonQuery();
                }
                return 0;
            });
            return n > 0;
        }

        List<MedicalImage> IMedicalStore.ListImages(ISession s, int defendantId)
        {
            var outp = new List<MedicalImage>();
            _db.Read(delegate(ISession ses)
            {
                var c = (DbContext)ses;
                using (var cmd = new SQLiteCommand(
                    "SELECT id, file_name, relative_path, content_type, byte_size, caption " +
                    "FROM medical_images WHERE defendant_id = @d ORDER BY id DESC;", c.Connection))
                {
                    cmd.Parameters.AddWithValue("@d", defendantId);
                    using (SQLiteDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            outp.Add(new MedicalImage
                            {
                                Id = r.GetInt64(0),
                                FileName = r.GetString(1),
                                RelativePath = r.GetString(2),
                                ContentType = r.GetString(3),
                                ByteSize = r.GetInt64(4),
                                Caption = r.GetString(5)
                            });
                        }
                    }
                }
                return 0;
            });
            return outp;
        }

        MedicalImage IMedicalStore.AddImage(ISession s, int defendantId, long? noteId, string fileName,
            string relativePath, string contentType, long byteSize, string caption)
        {
            MedicalImage img = null;
            _db.Write(delegate(ISession ses)
            {
                var c = (DbContext)ses;
                using (var cmd = new SQLiteCommand(
                    "INSERT INTO medical_images (defendant_id, note_id, file_name, relative_path, " +
                    "content_type, byte_size, caption, created_at_utc) " +
                    "VALUES (@d, @n, @fn, @rp, @ct, @bs, @cap, @at); SELECT last_insert_rowid();", c.Connection))
                {
                    cmd.Parameters.AddWithValue("@d", defendantId);
                    cmd.Parameters.AddWithValue("@n", noteId.HasValue ? (object)noteId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@fn", N(fileName));
                    cmd.Parameters.AddWithValue("@rp", N(relativePath));
                    cmd.Parameters.AddWithValue("@ct", N(contentType));
                    cmd.Parameters.AddWithValue("@bs", byteSize);
                    cmd.Parameters.AddWithValue("@cap", N(caption));
                    cmd.Parameters.AddWithValue("@at", NowIso(c));
                    long id = Convert.ToInt64(cmd.ExecuteScalar());
                    img = new MedicalImage
                    {
                        Id = id,
                        FileName = N(fileName),
                        RelativePath = N(relativePath),
                        ContentType = N(contentType),
                        ByteSize = byteSize,
                        Caption = N(caption)
                    };
                }
                return 0;
            });
            return img;
        }

        bool IMedicalStore.DeleteImage(ISession s, long imageId, out string relativePath)
        {
            string foundPath = null;
            int n = 0;
            _db.Write(delegate(ISession ses)
            {
                var c = (DbContext)ses;
                using (var sel = new SQLiteCommand(
                    "SELECT relative_path FROM medical_images WHERE id = @i;", c.Connection))
                {
                    sel.Parameters.AddWithValue("@i", imageId);
                    object v = sel.ExecuteScalar();
                    if (v != null && v != DBNull.Value)
                    {
                        foundPath = Convert.ToString(v);
                    }
                }
                using (var del = new SQLiteCommand("DELETE FROM medical_images WHERE id = @i;", c.Connection))
                {
                    del.Parameters.AddWithValue("@i", imageId);
                    n = del.ExecuteNonQuery();
                }
                return 0;
            });
            relativePath = foundPath;
            return n > 0;
        }

        bool IMedicalStore.HasMedical(int defendantId)
        {
            bool any = false;
            _db.Read(delegate(ISession ses)
            {
                var c = (DbContext)ses;
                using (var cmd = new SQLiteCommand(
                    "SELECT 1 FROM medical_notes WHERE defendant_id = @d AND note_text <> '' " +
                    "UNION ALL SELECT 1 FROM medical_images WHERE defendant_id = @d LIMIT 1;", c.Connection))
                {
                    cmd.Parameters.AddWithValue("@d", defendantId);
                    any = cmd.ExecuteScalar() != null;
                }
                return 0;
            });
            return any;
        }
    }
}