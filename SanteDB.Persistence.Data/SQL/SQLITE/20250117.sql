/** 
 * <feature scope="SanteDB.Persistence.Data" id="20250117" name="Update:20250117"  invariantName="sqlite">
 *	<summary>Update: Adds the replaces relationship for acts</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM patch_db_systbl WHERE patch_id='20250117')</isInstalled>
 * </feature>
 */
 
 
INSERT OR IGNORE INTO REL_VRFY_SYSTBL (rel_typ_cd_id, src_cls_cd_id, trg_cls_cd_id, err_desc, rel_cls) 
SELECT x'378657D1CBE15E41B3194011DA033813', cd_id, cd_id, 'err_ReplaceOnlySameType', 2 FROM cd_set_mem_vw WHERE set_mnemonic ='ActClass';

INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20250117', unixepoch(), 'Update: Adds the replaces relationship for acts');--#!
