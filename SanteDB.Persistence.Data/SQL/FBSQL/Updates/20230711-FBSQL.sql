/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230711-01" name="Update:20230711-01"   invariantName="FirebirdSQL">
 *	<summary>Update: Added check digit algorithm to identity, use to certificate mapping and drops sign key from app security table</summary>
 *	<isInstalled>select ck_patch('20230711-01') from rdb$database</isInstalled>
 * </feature>
 */
 
ALTER TABLE ID_DMN_TBL ADD CHK_DGT_ALG VARCHAR(512);--#!
ALTER TABLE SEC_CER_TBL ADD CER_USE INT; --#!
ALTER TABLE SEC_APP_TBL DROP SGN_KEY;--#!
DROP INDEX SEC_CER_X509_THB_IDX;--#!
CREATE UNIQUE INDEX SEC_CER_X509_THB_SIG_IDX ON SEC_CER_TBL COMPUTED BY (CASE WHEN OBSLT_UTC IS NULL AND CER_USE = 1 THEN X509_THB ELSE NULL END);--#!
CREATE UNIQUE INDEX SEC_CER_X509_THB_AUT_IDX ON SEC_CER_TBL COMPUTED BY (CASE WHEN OBSLT_UTC IS NULL AND CER_USE = 2 THEN X509_THB ELSE NULL END);--#!
-- FIX UNITS OF MEASURE URL 
UPDATE CD_SYS_TBL SET URL = 'http://unitsofmeasure.org' WHERE URL = 'http://hl7.org/fhir/sid/ucum';--#!
SELECT REG_PATCH('20230711-01') FROM RDB$DATABASE; --#!
