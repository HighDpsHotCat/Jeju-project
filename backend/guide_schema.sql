-- Idempotent upgrade for device catalogs that already contain national_species.
BEGIN;
CREATE INDEX IF NOT EXISTS species_matched_name ON species(matched_scientific_name, korean_name);
CREATE INDEX IF NOT EXISTS observation_cover ON observation(user_id, species_id, captured_at DESC, observation_id DESC)
    WHERE mode='general' AND identification='confirmed';

CREATE VIEW IF NOT EXISTS guide_catalog AS
    SELECT 'jeju' AS region, species_id, korean_name, scientific_name,
           class AS taxon_class
    FROM species
    UNION ALL
    SELECT 'national', COALESCE(link.species_id, 'KTSN-' || n.KTSN), n.대표국명, n.학명, n.Class_Name
    FROM national_species n
    LEFT JOIN (
        SELECT matched_scientific_name, korean_name, MIN(species_id) AS species_id
        FROM species
        WHERE matched_scientific_name <> '' AND korean_name <> ''
          AND name_match_status IN ('학명 일치','GBIF 이명 일치')
        GROUP BY matched_scientific_name, korean_name
        HAVING count(*) = 1
    ) link ON link.matched_scientific_name=n.학명 AND link.korean_name=n.대표국명
          AND (SELECT count(*) FROM national_species candidate
               WHERE candidate.학명=n.학명 AND candidate.대표국명=n.대표국명) = 1
    WHERE n.관리분류군 IN ('포유류','조류','곤충류','파충류','양서류','어류','미삭동물','무척추동물류(곤충제외)');
COMMIT;
