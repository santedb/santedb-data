/** 
 * <feature scope="SanteDB.Persistence.Data" id="20260615-01" name="Update:20260615-01"   invariantName="npgsql" >
 *	<summary>Update: Fixes full-text indexing</summary>
 *	<isInstalled>select ck_patch('20260615-01')</isInstalled>
 * </feature>
 */
 
 
CREATE OR REPLACE PROCEDURE public.rfrsh_fti()
 LANGUAGE plpgsql
AS $procedure$
BEGIN
	TRUNCATE TABLE ft_ent_systbl;
	INSERT INTO ft_ent_systbl WITH tels AS (
			SELECT DISTINCT ent_id, tel_val 
			FROM ENT_TEL_TBL
			WHERE OBSLT_VRSN_SEQ_ID IS NULL AND tel_val IS NOT NULL
		), names AS (
			SELECT ent_id, STRING_AGG(DISTINCT val, ' ') AS name
			FROM ent_name_cmp_tbl 
				INNER JOIN ent_name_tbl USING (name_id)
			WHERE obslt_vrsn_seq_id IS NULL AND val IS NOT NULL
			GROUP BY ent_id
		), addrs AS (
			SELECT ent_id, STRING_AGG(DISTINCT val, ' ') AS addr
			FROM ent_addr_cmp_tbl 
				INNER JOIN ent_addr_tbl USING (addr_id)
			WHERE 
				obslt_vrsn_seq_id IS NULL 
				AND typ_cd_id <> 'A314F427-2B6D-4948-9146-A5F700973899' AND val IS NOT NULL
			GROUP BY ent_id	
		), ids AS (
			SELECT DISTINCT ent_id, id_val 
			FROM ent_id_tbl 
			WHERE obslt_vrsn_seq_id IS NULL AND id_val IS NOT NULL
		), rels AS (
			SELECT DISTINCT src_ent_id, trg_ent_id 
			FROM ent_rel_tbl 
			WHERE obslt_vrsn_seq_id IS NULL 
				AND rel_typ_cd_id <> 'D1578637-E1CB-415E-B319-4011DA033813'
		), terms AS (
			SELECT 
				ent_id, 
				STRING_AGG(DISTINCT tel_val, ' ') AS tel, 
				STRING_AGG(DISTINCT name, ' ') AS name,
				STRING_AGG(DISTINCT addr, ' ') AS addr,
				STRING_AGG(DISTINCT id_val, ' ') AS id
			FROM ent_tbl 
				LEFT JOIN tels USING(ent_id)
				LEFT JOIN names USING (ent_id)
				LEFT JOIN addrs USING (ent_id)
				LEFT JOIN ids USING (ent_id)
			GROUP BY ent_id
		), rel_terms AS (
			SELECT src_ent_id AS ent_id, 
				STRING_AGG(DISTINCT tel, ' ') AS tel, 
				STRING_AGG(DISTINCT name, ' ') AS name,
				STRING_AGG(DISTINCT addr, ' ') AS addr,
				STRING_AGG(DISTINCT id, ' ') AS id
			FROM rels
				LEFT JOIN terms ON (terms.ent_id = rels.trg_ent_id)
			GROUP BY src_ent_id
		)
		SELECT ent_id, 
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(DISTINCT tel, ' ')), 'D') || 
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(DISTINCT name, ' ')), 'B') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(DISTINCT addr, ' ')), 'C') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(DISTINCT id, ' ')), 'A') AS terms
		FROM (
			SELECT ent_id, tel, name, addr, id FROM terms
			UNION
			SELECT ent_id, tel, name, addr, id FROM rel_terms
		) I
		GROUP BY ent_id;
	
END;
$procedure$
;

CREATE OR REPLACE PROCEDURE public.reset_fti_ent() LANGUAGE plpgsql
AS $procedure$
BEGIN
	TRUNCATE TABLE ft_ent_systbl;
END;
$procedure$
;

CREATE OR REPLACE PROCEDURE public.reindex_fti_ent(IN ent_id_in uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN 
	DELETE FROM FT_ENT_SYSTBL WHERE ent_id = ent_id_in;
	INSERT INTO FT_ENT_SYSTBL (ENT_ID, TERMS) -- FOR THE FOCAL OBJECT
		WITH tels AS (
			SELECT DISTINCT ent_id, tel_val 
			FROM ENT_TEL_TBL
			WHERE OBSLT_VRSN_SEQ_ID IS NULL AND tel_val IS NOT NULL
		), names AS (
			SELECT ent_id, STRING_AGG(DISTINCT val, ' ') AS name
			FROM ent_name_cmp_tbl 
				INNER JOIN ent_name_tbl USING (name_id)
			WHERE obslt_vrsn_seq_id IS NULL AND val IS NOT NULL
			GROUP BY ent_id
		), addrs AS (
			SELECT ent_id, STRING_AGG(DISTINCT val, ' ') AS addr
			FROM ent_addr_cmp_tbl 
				INNER JOIN ent_addr_tbl USING (addr_id)
			WHERE 
				obslt_vrsn_seq_id IS NULL 
				AND typ_cd_id <> 'A314F427-2B6D-4948-9146-A5F700973899' AND val IS NOT NULL
			GROUP BY ent_id	
		), ids AS (
			SELECT DISTINCT ent_id, id_val 
			FROM ent_id_tbl 
			WHERE obslt_vrsn_seq_id IS NULL AND id_val IS NOT NULL
		), rels AS (
			SELECT DISTINCT src_ent_id, trg_ent_id 
			FROM ent_rel_tbl 
			WHERE obslt_vrsn_seq_id IS NULL 
				AND rel_typ_cd_id <> 'D1578637-E1CB-415E-B319-4011DA033813'
		), terms AS (
			SELECT 
				ent_id, 
				STRING_AGG(DISTINCT tel_val, ' ') AS tel, 
				STRING_AGG(DISTINCT name, ' ') AS name,
				STRING_AGG(DISTINCT addr, ' ') AS addr,
				STRING_AGG(DISTINCT id_val, ' ') AS id
			FROM ent_tbl 
				LEFT JOIN tels USING(ent_id)
				LEFT JOIN names USING (ent_id)
				LEFT JOIN addrs USING (ent_id)
				LEFT JOIN ids USING (ent_id)
			GROUP BY ent_id
		), rel_terms AS (
			SELECT src_ent_id AS ent_id, 
				STRING_AGG(DISTINCT tel, ' ') AS tel, 
				STRING_AGG(DISTINCT name, ' ') AS name,
				STRING_AGG(DISTINCT addr, ' ') AS addr,
				STRING_AGG(DISTINCT id, ' ') AS id
			FROM rels
				LEFT JOIN terms ON (terms.ent_id = rels.trg_ent_id)
			GROUP BY src_ent_id
		)
		SELECT ent_id, 
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(DISTINCT tel, ' ')), 'D') || 
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(DISTINCT name, ' ')), 'B') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(DISTINCT addr, ' ')), 'C') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(DISTINCT id, ' ')), 'A') AS terms
		FROM (
			SELECT ent_id, tel, name, addr, id FROM terms
			UNION
			SELECT ent_id, tel, name, addr, id FROM rel_terms
		) I
		WHERE ent_id = ent_id_in
		GROUP BY ent_id;
	
END;
$procedure$
;

 SELECT REG_PATCH('20260615-01');