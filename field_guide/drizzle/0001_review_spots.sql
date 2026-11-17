ALTER TABLE spots ADD COLUMN review_area TEXT;
--> statement-breakpoint
WITH review(id,species,name,area,lat,lon) AS (VALUES
  ('509990f93ff17305ca4d573c6fde52faf6afae709502b8cf9a578197b6e6b058','JJ-004729','남방큰돌고래','함덕해수욕장 해안',33.5398138,126.6700177),
  ('8db58230b379372ae01d55e09b3ae7d32ed15dd08865016c5caa8d021c9bf3b7','120000053293','삵','금오름 탐방로',33.3547198,126.3061556),
  ('8ca8e5c8195c82e1b9f1e9f4508397ffc2ca2f8e3d6c61b0b0bc3cc39d5d8a22','JJ-004705','장수풍뎅이','용눈이오름 탐방로',33.4582811,126.8319152)
), clock(t) AS (SELECT CAST(strftime('%s','now') AS INTEGER)*1000)
INSERT INTO spots(id,owner,species,name,lat,lon,created,expires,review_area)
SELECT id,'judging-fixture',species,name,lat,lon,t,t+86400000,area FROM review,clock;
