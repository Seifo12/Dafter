ALTER TABLE defendants ADD COLUMN photo_path TEXT NOT NULL DEFAULT '';
CREATE INDEX ix_def_photo ON defendants(photo_path) WHERE photo_path <> '';