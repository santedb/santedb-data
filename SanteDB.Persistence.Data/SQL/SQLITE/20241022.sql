/** 
 * <feature scope="SanteDB.Persistence.Data" id="20241022" name="Update:20241022"  invariantName="sqlite">
 *	<summary>Update: Recreate the obsoletion reason</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM patch_db_systbl WHERE patch_id='20241022')</isInstalled>
 * </feature>
 */
 ALTER TABLE ACT_VRSN_TBL DROP OBSLT_RSN;
 ALTER TABLE ACT_VRSN_TBL ADD OBSLT_RSN BLOB(16);
 INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20241022', unixepoch(), 'Recreate the obsoletion reason');