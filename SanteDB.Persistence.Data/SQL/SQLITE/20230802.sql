/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230802-01" name="Update:20230802-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="sqlite">
 *	<summary>Update: Adds application encryption master key configuration functions</summary>
 *	<isInstalled>select count(*) > 0 from patch_db_systbl WHERE PATCH_ID = '20230802-01'</isInstalled>
 * </feature>
 */
 
CREATE TABLE ALE_SYSTBL (
	ALE_SMK BLOB NOT NULL -- MASTER KEY FOR THIS DATABASE
);

INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20230802-01', UNIXEPOCH(), 'Adds foreign data staging table'); 
