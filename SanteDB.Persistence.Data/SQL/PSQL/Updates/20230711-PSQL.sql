/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230711-01" name="Update:20230711-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="npgsql">
 *	<summary>Update: Adds check digit to identity domain table</summary>
 *	<isInstalled>select ck_patch('20230711-01')</isInstalled>
 * </feature>
 */

ALTER TABLE ID_DMN_TBL ADD CHK_DGT VARCHAR(512);--#!
 ALTER TABLE SEC_CER_TBL ADD CER_USE INT; --#!
 ALTER TABLE SEC_APP_TBL DROP SGN_KEY;
SELECT REG_PATCH('20230711-01') FROM RDB$DATABASE; --#!
