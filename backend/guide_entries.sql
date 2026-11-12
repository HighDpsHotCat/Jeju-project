-- Bind the same values in SQLite on the device and in the development API.
WITH page AS (
    SELECT species_id, korean_name, scientific_name, taxon_class
    FROM guide_catalog
    WHERE region=:region AND (:taxon='' OR taxon_class=:taxon)
      AND (:q='' OR instr(korean_name,:q)>0 OR instr(lower(scientific_name),lower(:q))>0
                 OR instr(lower(species_id),lower(:q))>0)
    ORDER BY species_id LIMIT :limit OFFSET :offset
)
SELECT p.*, COALESCE(NULLIF(p.korean_name,''),p.scientific_name) AS display_name,
       (cover.observation_id IS NOT NULL) AS registered, cover.photo_ref AS photo_ref,
       (SELECT json_group_array(json_object('area',area,'grade',grade,'grade_label',grade_label,
                                            'record_count',record_count))
        FROM (SELECT * FROM species_region_grade WHERE species_id=p.species_id ORDER BY area,grade)
       ) AS region_grades
FROM page p
LEFT JOIN observation cover ON cover.observation_id=(
    SELECT o.observation_id FROM observation o
    WHERE o.user_id=:user_id AND o.species_id=p.species_id
      AND o.mode='general' AND o.identification='confirmed'
    ORDER BY o.captured_at DESC, o.observation_id DESC LIMIT 1
)
ORDER BY p.species_id;
