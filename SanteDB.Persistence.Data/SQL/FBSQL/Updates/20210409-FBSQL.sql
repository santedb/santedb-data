/** 
 * <feature scope="SanteDB.Persistence.Data" id="20210409-01" name="Update:20210409-01"   invariantName="FirebirdSQL">
 *	<summary>Refactored persistence layer patch</summary>
 *	<isInstalled>select ck_patch('20210409-01') FROM RDB$DATABASE</isInstalled>
 * </feature>
 */

-- SECURITY STAMP DATA
ALTER TABLE SEC_APP_TBL ADD SGN_KEY  BLOB;--#!
SELECT REG_PATCH('20210409-01') FROM RDB$DATABASE;--#!
