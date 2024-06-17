/** 
 * <feature scope="SanteDB.Persistence.Data" id="20240617-01" name="Update:20240617-01" invariantName="npgsql">
 *	<summary>Update: Fixes issue with the fulltext indexing of patients to include their related objects in the index</summary>
 *	<isInstalled>select ck_patch('20240617-01')</isInstalled>
 * </feature>
 */
 
CREATE OR REPLACE PROCEDURE public.reindex_fti_ent(IN ent_id_in uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN 
	DELETE FROM FT_ENT_SYSTBL WHERE ent_id = ent_id_in;
	INSERT INTO FT_ENT_SYSTBL (ENT_ID, TERMS) -- FOR THE FOCAL OBJECT
		SELECT ent_tbl.ent_id, 
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', ptel.tel_val)), 'D') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', pid.id_val)), 'A') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', pcmp.val)), 'B') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', paddrcmp.val)), 'C') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', rtel.tel_val)), 'D') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', rid.id_val)), 'B') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', rcmp.val)), 'C') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', raddrcmp.val)), 'D') 
				AS terms
			FROM 
				ent_tbl 
				LEFT JOIN ent_tel_tbl ptel USING (ent_id)
				LEFT JOIN ent_name_tbl pname USING (ent_id)
				LEFT JOIN ent_name_cmp_tbl pcmp USING (name_id)
				LEFT JOIN ent_addr_tbl paddr USING (ent_id)
				LEFT JOIN ent_addr_cmp_tbl paddrcmp USING (addr_id)
				LEFT JOIN ent_id_tbl pid USING (ent_id)
				LEFT JOIN ent_rel_tbl erl ON (erl.SRC_ENT_ID = ent_id)
				LEFT JOIN ent_tel_tbl rtel ON (erl.trg_ent_id = rtel.ent_id)
				LEFT JOIN ent_name_tbl rname ON (erl.trg_ent_id = rname.ent_id)
				LEFT JOIN ent_name_cmp_tbl rcmp ON (rname.name_id = rcmp.name_id)
				LEFT JOIN ent_addr_tbl raddr ON (raddr.ent_id = erl.trg_ent_id)
				LEFT JOIN ent_addr_cmp_tbl raddrcmp ON (raddr.addr_id = raddrcmp.addr_id)
				LEFT JOIN ent_id_tbl rid ON (rid.ent_id = erl.trg_ent_id)
			WHERE 
				ent_tbl.ent_id = ent_id_in AND (
				ptel.tel_val IS NOT NULL AND ptel.OBSLT_VRSN_SEQ_ID IS NULL OR 
				pid.id_val IS NOT NULL  AND pid.OBSLT_VRSN_SEQ_ID IS NULL  OR 
				pcmp.val IS NOT NULL  AND pname.OBSLT_VRSN_SEQ_ID IS NULL  OR 
				paddrcmp.val IS NOT NULL AND paddr.OBSLT_VRSN_SEQ_ID IS NULL OR 
				rtel.tel_val IS NOT NULL AND rtel.OBSLT_VRSN_SEQ_ID IS NULL OR 
				rid.id_val IS NOT NULL  AND rid.OBSLT_VRSN_SEQ_ID IS NULL  OR 
				rcmp.VAL IS NOT NULL  AND rname.OBSLT_VRSN_SEQ_ID IS NULL  OR 
				raddrcmp.VAL  IS NOT NULL AND raddr.OBSLT_VRSN_SEQ_ID IS NULL)
		GROUP BY ent_tbl.ent_id;
	
END;
$procedure$
;

CREATE OR REPLACE PROCEDURE public.rfrsh_fti()
 LANGUAGE plpgsql
AS $procedure$
BEGIN
	CREATE TEMPORARY TABLE ft_ent_tmptbl AS 
	SELECT ent_id, vector FROM
		ent_tbl 
		INNER JOIN
		( 
			SELECT ent_tbl.ent_id, 
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', ptel.tel_val)), 'D') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', pid.id_val)), 'A') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', pcmp.val)), 'B') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', paddrcmp.val)), 'C') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', rtel.tel_val)), 'D') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', rid.id_val)), 'B') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', rcmp.val)), 'C') ||
				SETWEIGHT(TO_TSVECTOR(STRING_AGG(' ', raddrcmp.val)), 'D') 
				AS vector
			FROM 
				ent_tbl 
				LEFT JOIN ent_tel_tbl ptel USING (ent_id)
				LEFT JOIN ent_name_tbl pname USING (ent_id)
				LEFT JOIN ent_name_cmp_tbl pcmp USING (name_id)
				LEFT JOIN ent_addr_tbl paddr USING (ent_id)
				LEFT JOIN ent_addr_cmp_tbl paddrcmp USING (addr_id)
				LEFT JOIN ent_id_tbl pid USING (ent_id)
				LEFT JOIN ent_rel_tbl erl ON (erl.SRC_ENT_ID = ent_id)
				LEFT JOIN ent_tel_tbl rtel ON (erl.trg_ent_id = rtel.ent_id)
				LEFT JOIN ent_name_tbl rname ON (erl.trg_ent_id = rname.ent_id)
				LEFT JOIN ent_name_cmp_tbl rcmp ON (rname.name_id = rcmp.name_id)
				LEFT JOIN ent_addr_tbl raddr ON (raddr.ent_id = erl.trg_ent_id)
				LEFT JOIN ent_addr_cmp_tbl raddrcmp ON (raddr.addr_id = raddrcmp.addr_id)
				LEFT JOIN ent_id_tbl rid ON (rid.ent_id = erl.trg_ent_id)
			WHERE 
				ptel.tel_val IS NOT NULL AND ptel.OBSLT_VRSN_SEQ_ID IS NULL OR 
				pid.id_val IS NOT NULL  AND pid.OBSLT_VRSN_SEQ_ID IS NULL  OR 
				pcmp.val IS NOT NULL  AND pname.OBSLT_VRSN_SEQ_ID IS NULL  OR 
				paddrcmp.val IS NOT NULL AND paddr.OBSLT_VRSN_SEQ_ID IS NULL OR 
				rtel.tel_val IS NOT NULL AND rtel.OBSLT_VRSN_SEQ_ID IS NULL OR 
				rid.id_val IS NOT NULL  AND rid.OBSLT_VRSN_SEQ_ID IS NULL  OR 
				rcmp.VAL IS NOT NULL  AND rname.OBSLT_VRSN_SEQ_ID IS NULL  OR 
				raddrcmp.VAL  IS NOT NULL AND raddr.OBSLT_VRSN_SEQ_ID IS NULL
		GROUP BY ent_tbl.ent_id
		) vectors USING (ent_id);
	TRUNCATE TABLE ft_ent_systbl;
	INSERT INTO ft_ent_systbl SELECT * FROM ft_ent_tmptbl ;
	
END;
$procedure$
;

CALL RFRSH_FTI();

 SELECT REG_PATCH('20240617-01'); 
