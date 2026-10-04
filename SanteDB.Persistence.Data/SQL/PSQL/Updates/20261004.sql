/** 
 * <feature scope="SanteDB.Persistence.Data" id="20261004-02" name="Update:20261004-02"   invariantName="npgsql" >
 *	<summary>Update: Fixes duplicated entity extensions</summary>
 *	<isInstalled>select ck_patch('20261004-02')</isInstalled>
 * </feature>
 */
 WITH duplicate_records AS (
 SELECT ext_typ_id, ent_id, FIRST(ent_ext_id) keep_id 
	FROM ent_ext_tbl 
	WHERE obslt_vrsn_seq_id IS NULL 
  GROUP BY ent_id, ext_typ_id 
  HAVING count(ent_ext_id) > 1
)
UPDATE ent_ext_tbl
SET obslt_vrsn_seq_id = EFFT_VRSN_SEQ_ID  
FROM duplicate_records A
WHERE 
	A.ext_typ_id = ent_ext_tbl.ext_typ_id
	AND A.ent_id = ent_ext_tbl.ent_id
	AND ent_ext_id <> keep_id;


WITH duplicate_records AS (
 SELECT ext_typ_id, act_id, FIRST(act_ext_id) keep_id 
	FROM act_ext_tbl 
	WHERE obslt_vrsn_seq_id IS NULL 
  GROUP BY act_id, ext_typ_id 
  HAVING count(act_ext_id) > 1
)
UPDATE act_ext_tbl
SET obslt_vrsn_seq_id = EFFT_VRSN_SEQ_ID  
FROM duplicate_records A
WHERE 
	A.ext_typ_id = act_ext_tbl.ext_typ_id
	AND A.act_id = ACT_EXT_TBL.act_id
	AND act_ext_id <> keep_id;

CREATE UNIQUE INDEX ent_ext_uq_idx ON ent_ext_tbl(ent_id, ext_typ_id) WHERE (obslt_vrsn_seq_id IS NULL);
CREATE UNIQUE INDEX act_ext_uq_idx ON act_ext_tbl(act_id, ext_typ_id) WHERE (obslt_vrsn_seq_id IS NULL);

 SELECT REG_PATCH('20261004-02');