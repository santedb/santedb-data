/** 
 * <feature scope="SanteDB.Persistence.Data" id="20251216-01" name="Update:20251216-01"   invariantName="FirebirdSQL" >
 *	<summary>Update: Add not after and not before on proto assoc</summary>
 *	<isInstalled>select ck_patch('20251216-01') from rdb$database</isInstalled>
 * </feature>
 */
 
 ALTER TABLE ACT_PROTO_ASSOC_TBL ADD NAF DATE;
--#!
 ALTER TABLE ACT_PROTO_ASSOC_TBL ADD NBF DATE;
--#! 
 SELECT REG_PATCH('20251216-01') FROM RDB$DATABASE;