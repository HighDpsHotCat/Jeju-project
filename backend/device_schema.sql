-- Apply once when opening a new device database. Enable foreign_keys on each connection.
PRAGMA foreign_keys = ON;
CREATE TABLE device_profile (
    user_id TEXT PRIMARY KEY NOT NULL,
    display_name TEXT
);
CREATE TABLE observation (
    observation_id TEXT PRIMARY KEY NOT NULL,
    user_id TEXT NOT NULL REFERENCES device_profile,
    photo_id TEXT NOT NULL UNIQUE,
    photo_ref TEXT NOT NULL,
    captured_at TEXT NOT NULL,
    gps_at TEXT NOT NULL,
    latitude REAL NOT NULL CHECK(latitude BETWEEN -90 AND 90),
    longitude REAL NOT NULL CHECK(longitude BETWEEN -180 AND 180),
    accuracy_m REAL NOT NULL CHECK(accuracy_m >= 0),
    boundary_version TEXT NOT NULL,
    capture_source TEXT NOT NULL CHECK(capture_source = 'camera'),
    location_verdict TEXT NOT NULL CHECK(location_verdict = 'inside'),
    mode TEXT NOT NULL CHECK(mode IN ('general', 'research_only')),
    identification TEXT NOT NULL CHECK(identification IN ('unknown', 'confirmed')),
    species_id TEXT,
    CHECK((identification = 'unknown' AND species_id IS NULL) OR
          (identification = 'confirmed' AND species_id IS NOT NULL AND length(trim(species_id)) > 0))
);
CREATE INDEX observation_by_user_species ON observation(user_id, mode, species_id);
CREATE VIEW species_card AS
    SELECT user_id, species_id, count(*) AS observation_count
    FROM observation WHERE mode = 'general' AND identification = 'confirmed'
    GROUP BY user_id, species_id;
CREATE VIEW collection_progress AS
    SELECT p.user_id, count(DISTINCT o.species_id) AS species_count,
           count(o.observation_id) AS observation_count
    FROM device_profile p LEFT JOIN observation o
      ON o.user_id = p.user_id AND o.mode = 'general'
    GROUP BY p.user_id;
