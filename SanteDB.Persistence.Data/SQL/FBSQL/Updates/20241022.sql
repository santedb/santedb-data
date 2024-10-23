/** 
 * <feature scope="SanteDB.Persistence.Data" id="20241022-01" name="Update:20241022-01"   invariantName="FirebirdSQL">
 *	<summary>Update: Recreates unused obsoletion reason column</summary>
 *	<isInstalled>select ck_patch('20241022-01') from rdb$database</isInstalled>
 * </feature>
 */

 ALTER TABLE ACT_VRSN_TBL DROP COLUMN OBSLT_RSN;--#!
 ALTER TABLE ACT_VRSN_TBL ADD OBSLT_RSN UUID;--#!
 SELECT REG_PATCH('20241022-01') FROM RDB$DATABASE;--#! 