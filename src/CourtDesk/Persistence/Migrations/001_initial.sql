PRAGMA application_id = 0x43444B31;

CREATE TABLE app_meta (
  key   TEXT PRIMARY KEY,
  value TEXT NOT NULL
) WITHOUT ROWID;

INSERT INTO app_meta(key, value) VALUES
  ('schema_created_utc', ''),
  ('change_seq', '0'),
  ('max_seen_utc', ''),
  ('last_exit_backup_seq', ''),
  ('restore_id', ''),
  ('last_run_app_version', ''),
  ('clean_shutdown', '1');

CREATE TABLE settings (
  key            TEXT PRIMARY KEY,
  value          TEXT NOT NULL,
  updated_at_utc TEXT NOT NULL
) WITHOUT ROWID;

CREATE TABLE courts (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  name           TEXT NOT NULL CHECK (length(trim(name)) BETWEEN 1 AND 120),
  name_norm      TEXT NOT NULL,
  sort_order     INTEGER NOT NULL DEFAULT 100,
  is_active      INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0,1)),
  created_at_utc TEXT NOT NULL
);
CREATE UNIQUE INDEX ux_courts_name ON courts(name_norm);

CREATE TABLE circuits (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  court_id       INTEGER NOT NULL REFERENCES courts(id) ON DELETE RESTRICT,
  name           TEXT NOT NULL CHECK (length(trim(name)) BETWEEN 1 AND 120),
  name_norm      TEXT NOT NULL,
  is_active      INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0,1)),
  created_at_utc TEXT NOT NULL,
  UNIQUE (id, court_id),
  UNIQUE (court_id, name_norm)
);

CREATE TABLE custody_locations (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  name           TEXT NOT NULL CHECK (length(trim(name)) BETWEEN 1 AND 120),
  name_norm      TEXT NOT NULL,
  sort_order     INTEGER NOT NULL DEFAULT 100,
  is_active      INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0,1)),
  created_at_utc TEXT NOT NULL
);
CREATE UNIQUE INDEX ux_custody_name ON custody_locations(name_norm);

CREATE TABLE postponement_reasons (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  label      TEXT NOT NULL CHECK (length(trim(label)) BETWEEN 1 AND 120),
  label_norm TEXT NOT NULL UNIQUE,
  sort_order INTEGER NOT NULL DEFAULT 100,
  is_active  INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0,1))
);

CREATE TABLE charge_suggestions (
  id               INTEGER PRIMARY KEY AUTOINCREMENT,
  text             TEXT NOT NULL,
  text_norm        TEXT NOT NULL UNIQUE,
  use_count        INTEGER NOT NULL DEFAULT 1,
  last_used_at_utc TEXT NOT NULL
);

CREATE TABLE defendants (
  id                  INTEGER PRIMARY KEY AUTOINCREMENT,
  full_name           TEXT NOT NULL CHECK (length(trim(full_name)) BETWEEN 2 AND 200),
  name_norm           TEXT NOT NULL,
  national_id         TEXT CHECK (national_id IS NULL OR (length(national_id) = 14 AND national_id NOT GLOB '*[^0-9]*')),
  case_number         TEXT NOT NULL CHECK (length(case_number) BETWEEN 1 AND 12 AND case_number NOT GLOB '*[^0-9]*'),
  case_year           INTEGER NOT NULL CHECK (case_year BETWEEN 1950 AND 2100),
  case_type           TEXT NOT NULL DEFAULT '' CHECK (length(case_type) <= 40),
  charges             TEXT NOT NULL DEFAULT '' CHECK (length(charges) <= 1000),
  court_id            INTEGER REFERENCES courts(id) ON DELETE RESTRICT,
  circuit_id          INTEGER,
  custody_location_id INTEGER REFERENCES custody_locations(id) ON DELETE RESTRICT,
  session_date        TEXT CHECK (session_date IS NULL OR session_date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
  status              TEXT NOT NULL DEFAULT 'DETAINED' CHECK (status IN ('DETAINED','RELEASED','JUDGED')),
  detained_since      TEXT CHECK (detained_since IS NULL OR detained_since GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
  notes               TEXT NOT NULL DEFAULT '' CHECK (length(notes) <= 2000),
  is_deleted          INTEGER NOT NULL DEFAULT 0 CHECK (is_deleted IN (0,1)),
  row_version         INTEGER NOT NULL DEFAULT 1,
  created_at_utc      TEXT NOT NULL,
  updated_at_utc      TEXT NOT NULL,
  FOREIGN KEY (circuit_id, court_id) REFERENCES circuits(id, court_id) ON DELETE RESTRICT,
  CHECK (status = 'DETAINED' OR session_date IS NULL),
  CHECK (circuit_id IS NULL OR court_id IS NOT NULL)
);
CREATE INDEX ix_def_session ON defendants(session_date) WHERE is_deleted = 0 AND status = 'DETAINED';
CREATE INDEX ix_def_status  ON defendants(status, is_deleted);
CREATE INDEX ix_def_case    ON defendants(case_year, case_number);
CREATE INDEX ix_def_name    ON defendants(name_norm);
CREATE TRIGGER trg_def_no_hard_delete BEFORE DELETE ON defendants
BEGIN SELECT RAISE(ABORT, 'defendants are soft-deleted only'); END;

CREATE TABLE session_events (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  request_uuid          TEXT,
  defendant_id          INTEGER NOT NULL REFERENCES defendants(id) ON DELETE RESTRICT,
  event_type            TEXT NOT NULL CHECK (event_type IN ('CREATED','EDITED','DECISION','UNDO','DELETED','RESTORED','IMPORTED')),
  decision_code         TEXT CHECK (decision_code IS NULL OR decision_code IN
                          ('RENEW_DETENTION','POSTPONE','RELEASE','RELEASE_BAIL','JUDGMENT','REOPEN')),
  session_date_held     TEXT,
  next_session_date     TEXT,
  reason_text           TEXT CHECK (reason_text IS NULL OR length(reason_text) <= 200),
  bail_amount           INTEGER CHECK (bail_amount IS NULL OR bail_amount >= 0),
  note                  TEXT CHECK (note IS NULL OR length(note) <= 500),
  court_name_at_event   TEXT,
  circuit_name_at_event TEXT,
  before_json           TEXT,
  after_json            TEXT NOT NULL,
  undoes_event_id       INTEGER REFERENCES session_events(id),
  undone_by_event_id    INTEGER REFERENCES session_events(id),
  operator_name         TEXT NOT NULL,
  occurred_at_utc       TEXT NOT NULL,
  occurred_local_date   TEXT NOT NULL,
  CHECK ((event_type = 'DECISION') = (decision_code IS NOT NULL)),
  CHECK (event_type <> 'DECISION' OR session_date_held IS NOT NULL)
);
CREATE UNIQUE INDEX ux_ev_request ON session_events(request_uuid) WHERE request_uuid IS NOT NULL;
CREATE INDEX ix_ev_def  ON session_events(defendant_id, id);
CREATE INDEX ix_ev_held ON session_events(session_date_held)
  WHERE event_type = 'DECISION' AND undone_by_event_id IS NULL;
CREATE INDEX ix_ev_time ON session_events(occurred_at_utc);

CREATE TRIGGER trg_ev_no_delete BEFORE DELETE ON session_events
BEGIN SELECT RAISE(ABORT, 'audit rows are immutable'); END;

CREATE TRIGGER trg_ev_immutable BEFORE UPDATE ON session_events
WHEN NOT (
      OLD.undone_by_event_id IS NULL AND NEW.undone_by_event_id IS NOT NULL
  AND NEW.id IS OLD.id AND NEW.request_uuid IS OLD.request_uuid AND NEW.defendant_id IS OLD.defendant_id
  AND NEW.event_type IS OLD.event_type AND NEW.decision_code IS OLD.decision_code
  AND NEW.session_date_held IS OLD.session_date_held AND NEW.next_session_date IS OLD.next_session_date
  AND NEW.reason_text IS OLD.reason_text AND NEW.bail_amount IS OLD.bail_amount AND NEW.note IS OLD.note
  AND NEW.court_name_at_event IS OLD.court_name_at_event AND NEW.circuit_name_at_event IS OLD.circuit_name_at_event
  AND NEW.before_json IS OLD.before_json AND NEW.after_json IS OLD.after_json
  AND NEW.undoes_event_id IS OLD.undoes_event_id AND NEW.operator_name IS OLD.operator_name
  AND NEW.occurred_at_utc IS OLD.occurred_at_utc AND NEW.occurred_local_date IS OLD.occurred_local_date)
BEGIN SELECT RAISE(ABORT, 'audit rows are immutable'); END;

CREATE TABLE roster_prints (
  id                 INTEGER PRIMARY KEY AUTOINCREMENT,
  roster_date        TEXT NOT NULL,
  printed_at_utc     TEXT NOT NULL,
  operator_name      TEXT NOT NULL,
  defendant_count    INTEGER NOT NULL,
  defendant_ids_json TEXT NOT NULL
);
CREATE TRIGGER trg_rp_no_update BEFORE UPDATE ON roster_prints BEGIN SELECT RAISE(ABORT,'immutable'); END;
CREATE TRIGGER trg_rp_no_delete BEFORE DELETE ON roster_prints BEGIN SELECT RAISE(ABORT,'immutable'); END;
