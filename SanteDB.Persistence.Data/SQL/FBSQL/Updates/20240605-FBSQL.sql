/** 
 * <feature scope="SanteDB.Persistence.Data" id="20240605-01" name="Update:20240605-01"   invariantName="FirebirdSQL">
 *	<summary>Update: Updates the storage of identity domains to include default classification of the identifier domain</summary>
 *	<isInstalled>select ck_patch('20240605-01') from rdb$database</isInstalled>
 * </feature>
 */
ALTER TABLE ID_DMN_TBL ADD CLS_CD_ID UUID; --#!
SELECT REG_PATCH('20240605-03') FROM RDB$DATABASE; --#!
