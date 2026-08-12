/** 
 * <feature scope="SanteDB.Persistence.Data" id="20260812-01" name="Update:20260812-01"   invariantName="sqlite"  >
 *	<summary>Update: Add security policy concept classification</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM patch_db_systbl WHERE patch_id='20260812-01')</isInstalled>
 * </feature>
 */

ALTER TABLE SEC_POL_TBL ADD CLS_CD_ID BIGINT(16);
CREATE UNIQUE INDEX uq_ent_pol_assoc_idx ON ent_pol_assoc_tbl (ent_id, pol_id) WHERE obslt_vrsn_seq_id IS NULL;
CREATE UNIQUE INDEX uq_act_pol_assoc_idx ON act_pol_assoc_tbl (act_id, pol_id) WHERE obslt_vrsn_seq_id IS NULL;
INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20260812-01', UNIXEPOCH(), 'Add security policy classification code mapping');--#!
