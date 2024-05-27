/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230509-01" name="Update:20230509-01"   invariantName="FirebirdSQL">
 *	<summary>Update: Adds external tagging / key tracking to the database</summary>
 *	<isInstalled>select ck_patch('20230509-01') from rdb$database</isInstalled>
 * </feature>
 */
 
 ALTER TABLE ACT_PTCPT_TBL ADD EXT_ID VARCHAR(256); --#!
 ALTER TABLE ACT_REL_TBL ADD EXT_ID VARCHAR(256);--#!
 ALTER TABLE ENT_ADDR_TBL ADD EXT_ID VARCHAR(256);--#!
 ALTER TABLE ENT_NAME_TBL ADD EXT_ID VARCHAR(256);--#!
 ALTER TABLE ENT_REL_TBL ADD EXT_ID VARCHAR(256);--#!
 ALTER TABLE PSN_LNG_TBL ADD EXT_ID VARCHAR(256);--#!
 ALTER TABLE PLC_SVC_TBL ADD EXT_ID VARCHAR(256);--#!
 ALTER TABLE ENT_TEL_TBL ADD EXT_ID VARCHAR(256);--#!
 ALTER TABLE SEC_SES_TBL ADD EP VARCHAR(64);--#!
SELECT REG_PATCH('20230509-01') FROM RDB$DATABASE; 
