/** 
 * <feature scope="SanteDB.Persistence.Data" id="20241119" name="Update:20241119"  invariantName="sqlite" environment="Server">
 *	<summary>Update: Updates the concept relationship tables to support flow relationships</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM patch_db_systbl WHERE patch_id='20241119')</isInstalled>
 * </feature>
 */
INSERT INTO CD_REL_TYP_CDTBL (REL_TYP_ID, REL_NAME, MNEMONIC, CRT_PROV_ID) VALUES (x'9DEF1F3E8EDD9D4B8462CE5A52213743', 'State Flow', 'StateFlow', x'76A0DCFA90366E4AAF9EF1CD68E8C7E8');
INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20241119', unixepoch(), 'Update: Updates the concept relationship tables to support flow relationships');--#!