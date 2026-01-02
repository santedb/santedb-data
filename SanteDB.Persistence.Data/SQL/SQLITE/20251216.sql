/** 
 * <feature scope="SanteDB.Persistence.Data" id="20251216" name="Update:20251216"  invariantName="sqlite">
 *	<summary>Update: Add not before not after to protocol steps</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM patch_db_systbl WHERE patch_id='20251216')</isInstalled>
 * </feature>
 */
  
 ALTER TABLE ACT_PROTO_ASSOC_TBL ADD NAF BIGINT;
 ALTER TABLE ACT_PROTO_ASSOC_TBL ADD NBF BIGINT;
 INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20251216', unixepoch(), 'Update: Add time bounds to protocol');--#!
