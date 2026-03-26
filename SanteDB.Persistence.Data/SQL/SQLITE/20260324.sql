/** 
 * <feature scope="SanteDB.Persistence.Data" id="20260324-01" name="Update:20260324-01"   invariantName="sqlite"  >
 *	<summary>Update: Add Rel Sequence</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM patch_db_systbl WHERE patch_id='20260324-01')</isInstalled>
 * </feature>
 */
ALTER TABLE ACT_REL_TBL ADD REL_SEQ_ID BIGINT NOT NULL DEFAULT 0;
UPDATE ACT_REL_TBL SET REL_SEQ_ID = ROWID;
INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20260324-01', UNIXEPOCH(), 'Add relationship sequences');--#!
