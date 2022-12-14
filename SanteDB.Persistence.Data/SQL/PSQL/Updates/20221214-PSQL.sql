/** 
 * <feature scope="SanteDB.Persistence.Data" id="20221214-01" name="Update:20221214-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="npgsql">
 *	<summary>Update: Convert refresh FTI to procedures</summary>
 *	<isInstalled>select ck_patch('20221214-01')</isInstalled>
 * </feature>
 */

DROP FUNCTION IF EXISTS rfrsh_fti;
DROP FUNCTION IF EXISTS reindex_fti_ent;

CREATE OR REPLACE PROCEDURE rfrsh_fti() 
AS
$$
BEGIN
	CREATE TEMPORARY TABLE ft_ent_tmptbl AS 
	SELECT ent_id, vector FROM
		ent_tbl 
		INNER JOIN
		( 
			SELECT ent_id, SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', tel_val)), 'D') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', id_val)), 'A') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', ent_name_cmp_tbl.val)), 'B') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', ent_addr_cmp_tbl.val)), 'C') AS vector
			FROM 
				ent_tbl 
				LEFT JOIN ent_tel_tbl USING (ent_id)
				LEFT JOIN ent_name_tbl USING (ent_id)
				LEFT JOIN ent_name_cmp_tbl USING (name_id)
				LEFT JOIN ent_addr_tbl USING (ent_id)
				LEFT JOIN ent_addr_cmp_tbl USING (addr_id)
				LEFT JOIN ent_id_tbl USING (ent_id)
			WHERE 
				tel_val IS NOT NULL AND ent_tel_tbl.OBSLT_VRSN_SEQ_ID IS NULL OR 
				id_val IS NOT NULL  AND ent_id_tbl.OBSLT_VRSN_SEQ_ID IS NULL  OR 
				ent_name_cmp_tbl.VAL IS NOT NULL  AND ent_name_tbl.OBSLT_VRSN_SEQ_ID IS NULL  OR 
				ent_addr_cmp_tbl.VAL  IS NOT NULL AND ent_addr_tbl.OBSLT_VRSN_SEQ_ID IS NULL 
			GROUP BY ent_id
		) vectors USING (ent_id);
	TRUNCATE TABLE ft_ent_systbl;
	INSERT INTO ft_ent_systbl SELECT * FROM ft_ent_tmptbl ;
	
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE PROCEDURE reindex_fti_ent(ent_id_in IN UUID) 
AS 
$$
BEGIN 
	UPDATE FT_ENT_SYSTBL 
	SET terms = vector
	FROM 
		(SELECT SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', tel_val)), 'D') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', id_val)), 'A') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', ent_name_cmp_tbl.val)), 'B') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', ent_addr_cmp_tbl.val)), 'C') AS vector
			FROM 
				ent_tbl 
				LEFT JOIN ent_tel_tbl USING (ent_id)
				LEFT JOIN ent_name_tbl USING (ent_id)
				LEFT JOIN ent_name_cmp_tbl USING (name_id)
				LEFT JOIN ent_addr_tbl USING (ent_id)
				LEFT JOIN ent_addr_cmp_tbl USING (addr_id)
				LEFT JOIN ent_id_tbl USING (ent_id)
			WHERE 
				ent_id = ent_id_in AND (
				tel_val IS NOT NULL AND ent_tel_tbl.OBSLT_VRSN_SEQ_ID IS NULL OR 
				id_val IS NOT NULL  AND ent_id_tbl.OBSLT_VRSN_SEQ_ID IS NULL  OR 
				ent_name_cmp_tbl.VAL IS NOT NULL  AND ent_name_tbl.OBSLT_VRSN_SEQ_ID IS NULL  OR 
				ent_addr_cmp_tbl.VAL  IS NOT NULL AND ent_addr_tbl.OBSLT_VRSN_SEQ_ID IS NULL)) I 
	WHERE ent_id = ent_id_in;

END;
$$ LANGUAGE plpgsql;

SELECT REG_PATCH('20221214-01'); 