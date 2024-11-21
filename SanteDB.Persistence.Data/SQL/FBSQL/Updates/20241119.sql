/** 
 * <feature scope="SanteDB.Persistence.Data" id="20241119-01" name="Update:20241119-01"   invariantName="FirebirdSQL">
 *	<summary>Update: Updates the concept relationship tables to support flow relationships</summary>
 *	<isInstalled>select ck_patch('20241119-01') from rdb$database</isInstalled>
 * </feature>
 */

 INSERT INTO CD_REL_TYP_CDTBL (REL_TYP_ID, REL_NAME, MNEMONIC, CRT_PROV_ID) VALUES (char_to_uuid('3E1FEF9D-DD8E-4B9D-8462-CE5A52213743'), 'State Flow', 'StateFlow', char_to_uuid('fadca076-3690-4a6e-af9e-f1cd68e8c7e8'));--#!
 SELECT REG_PATCH('20241119-01') FROM RDB$DATABASE;--#! 