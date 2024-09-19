/** 
 * <feature scope="SanteDB.Persistence.Data" id="20240628-03" name="Update:20240628-03" invariantName="npgsql">
 *	<summary>Update: Fixes the indexing constraints on the certificates table allowing dsig certs to have multiple entries and registers the ent_rel_tbl trigger</summary>
 *	<isInstalled>select ck_patch('20240628-03')</isInstalled>
 * </feature>
 */

 DROP INDEX IF EXISTS SEC_CER_X509_THB_IDX;
 CREATE UNIQUE INDEX IF NOT EXISTS SEC_CER_X509_THB_AUT_IDX ON SEC_CER_TBL(X509_THB) WHERE (OBSLT_UTC IS NULL AND CER_USE = 2);--#!
 CREATE UNIQUE INDEX IF NOT EXISTS SEC_CER_X509_THB_DSIG_IDX ON SEC_CER_TBL(X509_THB) WHERE (OBSLT_UTC IS NULL AND CER_USE = 1 AND USR_ID NOT IN ('fadca076-3690-4a6e-af9e-f1cd68e8c7e8','c96859f0-043c-4480-8dab-f69d6e86696c'));-- ONLY REQUIRED TO BE UNIQUE FOR REGULAR USERS
 ALTER TABLE ID_DMN_TBL ALTER COLUMN VAL_RGX TYPE VARCHAR(256);


 SELECT REG_PATCH('20240628-03'); 
