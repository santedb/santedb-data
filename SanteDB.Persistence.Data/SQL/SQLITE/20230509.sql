/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230509-01" name="Update:20230509-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="sqlite">
 *	<summary>Update: Adds external tagging / key tracking to the database</summary>
 *	<isInstalled>select count(*) > 0 from patch_db_systbl WHERE PATCH_ID = '20230509-01'</isInstalled>
 * </feature>
 */
 
 ALTER TABLE ACT_PTCPT_TBL ADD EXT_ID VARCHAR(256);
 ALTER TABLE ACT_REL_TBL ADD EXT_ID VARCHAR(256);
 ALTER TABLE ENT_ADDR_TBL ADD EXT_ID VARCHAR(256);
 ALTER TABLE ENT_NAME_TBL ADD EXT_ID VARCHAR(256);
 ALTER TABLE ENT_REL_TBL ADD EXT_ID VARCHAR(256);
 ALTER TABLE PSN_LNG_TBL ADD EXT_ID VARCHAR(256);
 ALTER TABLE PLC_SVC_TBL ADD EXT_ID VARCHAR(256);
 ALTER TABLE ENT_TEL_TBL ADD EXT_ID VARCHAR(256);

INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20230509-01', UNIXEPOCH(), 'Add external tagging metadata to tables'); 
