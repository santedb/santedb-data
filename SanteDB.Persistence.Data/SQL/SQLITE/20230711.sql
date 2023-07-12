/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230711-01" name="Update:20230711-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="sqlite">
 *	<summary>Update: Adds check digit for identity domain</summary>
 *	<isInstalled>select count(*) > 0 from patch_db_systbl WHERE PATCH_ID = '20230711-01'</isInstalled>
 * </feature>
 */
 
ALTER TABLE ID_DMN_TBL ADD CHK_DGT VARCHAR(512);
 ALTER TABLE SEC_CER_TBL ADD CER_USE INT; --#!
 ALTER TABLE SEC_APP_TBL DROP SGN_KEY;
INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20230711-01', UNIXEPOCH(), 'Add identity domain check digit'); 
