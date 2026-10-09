CREATE TABLE medical_notes (
  id              INTEGER PRIMARY KEY AUTOINCREMENT,
  defendant_id    INTEGER NOT NULL,
  note_text       TEXT NOT NULL DEFAULT '',
  recorded_at_utc TEXT NOT NULL,
  recorded_by     TEXT NOT NULL DEFAULT ''
);

CREATE TABLE medical_images (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  defendant_id   INTEGER NOT NULL,
  note_id        INTEGER,
  file_name      TEXT NOT NULL,
  relative_path  TEXT NOT NULL,
  content_type   TEXT NOT NULL DEFAULT '',
  byte_size      INTEGER NOT NULL DEFAULT 0,
  caption        TEXT NOT NULL DEFAULT '',
  created_at_utc TEXT NOT NULL
);

CREATE INDEX ix_med_note_def ON medical_notes(defendant_id);
CREATE INDEX ix_med_img_def  ON medical_images(defendant_id);
CREATE INDEX ix_med_img_note ON medical_images(note_id);