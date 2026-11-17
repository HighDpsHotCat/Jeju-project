CREATE TABLE spots (
  id TEXT PRIMARY KEY,
  owner TEXT NOT NULL,
  species TEXT NOT NULL,
  name TEXT NOT NULL,
  lat REAL NOT NULL,
  lon REAL NOT NULL,
  created INTEGER NOT NULL,
  expires INTEGER NOT NULL
);
--> statement-breakpoint
CREATE INDEX spots_expiry ON spots(expires);
--> statement-breakpoint
CREATE TABLE adoption_claims (
  owner TEXT NOT NULL,
  species TEXT NOT NULL,
  claimed INTEGER NOT NULL,
  PRIMARY KEY(owner, species)
);
