/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230711-01" name="Update:20230711-01"   invariantName="npgsql">
 *	<summary>Update: Adds check digit to identity domain table</summary>
 *	<isInstalled>select ck_patch('20230711-01')</isInstalled>
 * </feature>
 */

ALTER TABLE ID_DMN_TBL ADD COLUMN IF NOT EXISTS CHK_DGT_ALG VARCHAR(512);--#!
ALTER TABLE SEC_CER_TBL ADD COLUMN IF NOT EXISTS CER_USE INT; --#!
ALTER TABLE SEC_APP_TBL DROP COLUMN IF EXISTS SGN_KEY;
DROP INDEX IF EXISTS SEC_CER_X509_THB_IDX ;--#!
CREATE UNIQUE INDEX SEC_CER_X509_THB_IDX ON SEC_CER_TBL(X509_THB, CER_USE) WHERE (OBSLT_UTC IS NULL);--#!
-- FIX UNITS OF MEASURE URL 
UPDATE CD_SYS_TBL SET URL = 'http://unitsofmeasure.org' WHERE URL = 'http://hl7.org/fhir/sid/ucum';--#!
SELECT REG_PATCH('20230711-01'); --#!
