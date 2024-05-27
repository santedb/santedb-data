/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230711-01" name="Update:20230711-01"   invariantName="sqlite">
 *	<summary>Update: Adds check digit for identity domain</summary>
 *	<isInstalled>select count(*) > 0 from patch_db_systbl WHERE PATCH_ID = '20230711-01'</isInstalled>
 * </feature>
 */
 
ALTER TABLE ID_DMN_TBL ADD CHK_DGT_ALG VARCHAR(512);
 ALTER TABLE SEC_CER_TBL ADD CER_USE INT; --#!
 ALTER TABLE SEC_APP_TBL DROP SGN_KEY;

 DROP INDEX SEC_CER_X509_THB_IDX;--#!
CREATE UNIQUE INDEX SEC_CER_X509_THB_IDX ON SEC_CER_TBL(X509_THB, CER_USE) WHERE (OBSLT_UTC IS NULL);
-- FIX UNITS OF MEASURE URL 
UPDATE CD_SYS_TBL SET URL = 'http://unitsofmeasure.org' WHERE URL = 'http://hl7.org/fhir/sid/ucum';
INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20230711-01', UNIXEPOCH(), 'Add identity domain check digit'); 
