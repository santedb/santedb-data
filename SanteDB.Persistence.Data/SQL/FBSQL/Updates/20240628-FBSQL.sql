/** 
 * <feature scope="SanteDB.Persistence.Data" id="20240628-01" name="Update:20240628-01"   invariantName="FirebirdSQL">
 *	<summary>Update: Updates the certificate mapping index to allow for a certificate to be assigned to multiple uses</summary>
 *	<isInstalled>select ck_patch('20240628-01') from rdb$database</isInstalled>
 * </feature>
 */
-- OPTIONAL
DROP INDEX SEC_CER_X509_THB_IDX; --#!
CREATE UNIQUE INDEX SEC_CER_X509_THB_AUTH_IDX ON SEC_CER_TBL COMPUTED BY (CASE WHEN OBSLT_UTC IS NULL AND CER_USE = 2 THEN X509_THB ELSE NULL END);--#!

SELECT REG_PATCH('20240628-03') FROM RDB$DATABASE; --#!
